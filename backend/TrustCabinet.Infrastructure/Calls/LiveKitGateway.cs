using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Google.Protobuf;
using Livekit.Server.Sdk.Dotnet;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Calls;

namespace TrustCabinet.Infrastructure.Calls;

/// <summary>The <c>LiveKit</c> configuration section. The secret only ever comes from user-secrets or the environment.</summary>
public sealed class LiveKitOptions
{
    public const string SectionName = "LiveKit";

    /// <summary>What the browser connects to — ws:// or wss://.</summary>
    public string PublicUrl { get; set; } = "";

    /// <summary>What the backend calls the server API on — http:// or https://, same port.</summary>
    public string ApiUrl { get; set; } = "";

    public string ApiKey { get; set; } = "";

    public string ApiSecret { get; set; } = "";

    public int TokenTtlMinutes { get; set; } = 10;

    /// <summary>Remove room members who are not allowed in. Off only to load test with synthetic identities.</summary>
    public bool EnforceRoster { get; set; } = true;

    /// <summary>The SDK refuses a secret shorter than 32 bytes, so a short one counts as not configured.</summary>
    public bool IsConfigured =>
        PublicUrl.Length > 0 && ApiUrl.Length > 0 && ApiKey.Length > 0
        && Encoding.UTF8.GetByteCount(ApiSecret) >= 32;
}

/// <summary>
/// The only code that talks to LiveKit (ADR-0010). A new RoomServiceClient per call on purpose:
/// the SDK rewrites its HttpClient's Authorization header on every request, so one shared instance
/// would race between concurrent requests.
/// </summary>
public sealed class LiveKitGateway(
    IOptions<LiveKitOptions> options,
    IHttpClientFactory httpClients) : ILiveKitGateway
{
    public const string HttpClientName = "livekit";

    private LiveKitOptions O => options.Value;

    public bool IsConfigured => O.IsConfigured;

    public CallJoinTicket CreateJoinTicket(
        string roomName, Guid userId, string displayName, string metadataJson, TimeSpan ttl)
    {
        EnsureConfigured();
        var identity = userId.ToString("D");
        var token = new AccessToken(O.ApiKey, O.ApiSecret)
            .WithIdentity(identity)
            .WithName(displayName)
            .WithMetadata(metadataJson)
            .WithTtl(ttl)
            .WithGrants(new VideoGrants
            {
                RoomJoin = true,
                Room = roomName,
                CanPublish = true,
                CanSubscribe = true,
                CanPublishData = true,
                // Lower-case names; honoured only together with CanPublish.
                CanPublishSources = ["camera", "microphone", "screen_share", "screen_share_audio"],
                CanUpdateOwnMetadata = false,
                RoomAdmin = false,
            })
            .ToJwt();

        return new CallJoinTicket(O.PublicUrl, token, roomName, identity, DateTimeOffset.UtcNow.Add(ttl));
    }

    public async Task<IReadOnlyList<RoomParticipant>> ListParticipantsAsync(string roomName, CancellationToken ct)
    {
        var response = await CallAsync(
            rooms => rooms.ListParticipants(new ListParticipantsRequest { Room = roomName }), ct);
        if (response is null)
        {
            return [];
        }

        return response.Participants
            .Select(p => new RoomParticipant(
                p.Identity,
                p.Name,
                DateTimeOffset.FromUnixTimeSeconds(p.JoinedAt),
                p.Tracks.Select(t => new RoomTrack(t.Sid, Map(t.Source), t.Muted)).ToArray()))
            .ToArray();
    }

    public async Task<IReadOnlyList<string>> ListRoomNamesAsync(CancellationToken ct)
    {
        var response = await CallAsync(rooms => rooms.ListRooms(new ListRoomsRequest()), ct);
        return response?.Rooms.Select(r => r.Name).ToArray() ?? [];
    }

    public Task MuteTrackAsync(string roomName, string identity, string trackSid, CancellationToken ct)
        => CallAsync(rooms => rooms.MutePublishedTrack(new MuteRoomTrackRequest
        {
            Room = roomName,
            Identity = identity,
            TrackSid = trackSid,
            Muted = true,
        }), ct);

    public Task RemoveParticipantAsync(string roomName, string identity, CancellationToken ct)
        => CallAsync(rooms => rooms.RemoveParticipant(new RoomParticipantIdentity
        {
            Room = roomName,
            Identity = identity,
        }), ct);

    public Task DeleteRoomAsync(string roomName, CancellationToken ct)
        => CallAsync(rooms => rooms.DeleteRoom(new DeleteRoomRequest { Room = roomName }), ct);

    public CallWebhookEvent? ReadWebhook(byte[] body, string? authorizationHeader)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return null;
        }

        // Not WebhookReceiver.Receive: it throws on any field newer than the SDK's protocol
        // (server 1.13 sends roomEndReason), and its ignoreUnknownFields switch has no effect.
        string? signedHash;
        try
        {
            signedHash = new TokenVerifier(O.ApiKey, O.ApiSecret).Verify(authorizationHeader).Sha256;
        }
        catch (Exception)
        {
            return null;
        }

        var actualHash = Convert.ToBase64String(SHA256.HashData(body));
        if (signedHash is null || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(signedHash), Encoding.UTF8.GetBytes(actualHash)))
        {
            return null;
        }

        WebhookEvent parsed;
        try
        {
            parsed = new JsonParser(JsonParser.Settings.Default.WithIgnoreUnknownFields(true))
                .Parse<WebhookEvent>(Encoding.UTF8.GetString(body));
        }
        catch (InvalidProtocolBufferException)
        {
            return null;
        }

        return new CallWebhookEvent(
            parsed.Id,
            parsed.Event,
            parsed.Room?.Name,
            parsed.Participant?.Identity,
            parsed.Participant is { JoinedAt: > 0 } p ? DateTimeOffset.FromUnixTimeSeconds(p.JoinedAt) : null,
            parsed.Track?.Sid,
            parsed.Track is null ? CallTrackSource.Unknown : Map(parsed.Track.Source),
            parsed.CreatedAt > 0 ? DateTimeOffset.FromUnixTimeSeconds(parsed.CreatedAt) : DateTimeOffset.UtcNow);
    }

    private static CallTrackSource Map(TrackSource source) => source switch
    {
        TrackSource.Camera => CallTrackSource.Camera,
        TrackSource.Microphone => CallTrackSource.Microphone,
        TrackSource.ScreenShare => CallTrackSource.ScreenShare,
        TrackSource.ScreenShareAudio => CallTrackSource.ScreenShareAudio,
        _ => CallTrackSource.Unknown,
    };

    /// <summary>
    /// Runs one server API call. Not_Found means the room or participant is already gone, which
    /// every caller treats as "nothing to do", so it returns null rather than throwing.
    /// </summary>
    private async Task<T?> CallAsync<T>(Func<RoomServiceClient, Task<T>> call, CancellationToken ct)
        where T : class
    {
        EnsureConfigured();
        var rooms = new RoomServiceClient(O.ApiUrl, O.ApiKey, O.ApiSecret, httpClients.CreateClient(HttpClientName));
        try
        {
            // The SDK takes no CancellationToken.
            return await call(rooms).WaitAsync(ct);
        }
        catch (Twirp.Exception error) when (error.Type == Twirp.ErrorCode.Not_Found)
        {
            return null;
        }
        catch (Twirp.Exception error)
        {
            throw new LiveKitUnavailableException($"LiveKit refused the call: {error.Type}", error);
        }
        catch (HttpRequestException error)
        {
            throw new LiveKitUnavailableException("LiveKit could not be reached.", error);
        }
        catch (TaskCanceledException error) when (!ct.IsCancellationRequested)
        {
            throw new LiveKitUnavailableException("LiveKit did not answer in time.", error);
        }
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "LiveKit is not configured. Check IsConfigured before calling the gateway.");
        }
    }
}
