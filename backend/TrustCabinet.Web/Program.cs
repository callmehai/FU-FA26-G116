using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Infrastructure.Agenda;
using TrustCabinet.Infrastructure.Attendance;
using TrustCabinet.Infrastructure.Summaries;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Access;
using TrustCabinet.Infrastructure.Authentication;
using TrustCabinet.Infrastructure.Email;
using TrustCabinet.Infrastructure.GuestAccess;
using TrustCabinet.Infrastructure.Calls;
using TrustCabinet.Infrastructure.Documents;
using TrustCabinet.Infrastructure.Meetings;
using TrustCabinet.Infrastructure.Notifications;
using TrustCabinet.Infrastructure.Users;
using TrustCabinet.Infrastructure.Participants;
using TrustCabinet.Infrastructure.Recordings;
using TrustCabinet.Infrastructure.Transcription;
using TrustCabinet.Infrastructure.VoiceId;
using TrustCabinet.Infrastructure.Votes;
using TrustCabinet.Web;
using TrustCabinet.Web.Endpoints;

// ADR-0044: this same app, started by PdfPageCounter to parse one uploaded PDF, so a hostile file can only end the child.
if (args is [PdfPageCounter.ChildArgument, var pdfPath]) Environment.Exit(await PdfPageCounter.RunAsChildAsync(pdfPath));

// ADR-0047: the deploy's deliberate migration step — migrate, seed reference data, exit; no web server.
if (args is [DatabaseMigrationCommand.Argument, .. var migrationArguments])
{
    Environment.ExitCode = await DatabaseMigrationCommand.RunAsync(migrationArguments);
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTrustCabinetDatabase(builder.Configuration);
builder.Services.AddTrustCabinetAuthentication(builder.Configuration);
builder.Services.AddTrustCabinetUserManagement();
builder.Services.AddTrustCabinetRolePermissions();
builder.Services.AddTrustCabinetMeetings(builder.Configuration);
builder.Services.AddTrustCabinetAgenda();
builder.Services.AddTrustCabinetParticipants();
builder.Services.AddTrustCabinetAttendance();
builder.Services.AddTrustCabinetVotes();
// SCRUM-116: UC-04 guest access; replaces the fail-closed guest adapters registered above.
builder.Services.AddTrustCabinetGuestAccess(builder.Configuration);
builder.Services.AddSingleton<IMeetingGuestIdentityResolver, GuestTokenIdentityResolver>();
builder.Services.AddScoped<IAgendaChangePublisher, AgendaChangePublisher>();
builder.Services.AddScoped<MeetingDocumentChangePublisher>();
builder.Services.AddScoped<TrustCabinet.Domain.Attendance.IAttendanceChangePublisher, AttendanceChangePublisher>();
builder.Services.AddScoped<TrustCabinet.Domain.Votes.IVoteChangePublisher, VoteChangePublisher>();
builder.Services.AddScoped<TrustCabinet.Domain.Recordings.ISpeechChangePublisher, SpeechChangePublisher>();
builder.Services.AddScoped<TrustCabinet.Domain.Participants.IParticipantsChangePublisher, ParticipantsChangePublisher>();
builder.Services.AddScoped<TrustCabinet.Domain.Access.ICapabilitiesChangePublisher, CapabilitiesChangePublisher>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, NotificationUserIdProvider>();
builder.Services.AddTrustCabinetNotifications();
builder.Services.AddHostedService<NotificationWorker>();
builder.Services.AddTrustCabinetEmailDelivery(builder.Configuration);

// SCRUM-39: meeting call room on LiveKit (ADR-0010). Starts without LiveKit configured; the
// call endpoints then answer 503.
builder.Services.AddTrustCabinetCalls(builder.Configuration);
builder.Services.AddHostedService<CallRoomJanitor>();

// SCRUM-107: meeting documents, bytes on Cloudflare R2 (ADR-0024). Starts without a store
// configured; uploads then answer 503/MSG166.
builder.Services.AddTrustCabinetDocuments(builder.Configuration);

// SCRUM-29: recordings of a sitting, on local disk, erased when their retention period runs out.
builder.Services.AddTrustCabinetRecordings(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddHostedService<RecordingRetentionWorker>();
// SCRUM-99: cancels meetings nobody started a day after their planned start (BR-301).
builder.Services.AddHostedService<MeetingOverdueWorker>();

// SCRUM-31: speech to text on the in-house speech worker (workers/speech), one recording at a time.
builder.Services.AddTrustCabinetTranscription(builder.Configuration);
builder.Services.AddHostedService<TranscriptionWorker>();
builder.Services.AddTrustCabinetSummaries(builder.Configuration);
builder.Services.AddHostedService<SummaryWorker>();

// SCRUM-35: Voice ID — consent, encrypted enrolment samples, speaker suggestions in transcripts.
builder.Services.AddTrustCabinetVoiceId(builder.Configuration);

// The audit log records where a request came from, and only the web layer can see that.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICallerContext, HttpCallerContext>();

var authenticationOptions = builder.Configuration
    .GetSection(AuthenticationOptions.SectionName)
    .Get<AuthenticationOptions>() ?? new AuthenticationOptions();

// The bearer scheme below builds the signing key immediately, and SymmetricSecurityKey throws an
// unhelpful "key too small" on a missing one. Checked here so a fresh clone gets told what to set.
if (authenticationOptions.SigningKey.Length < 32)
{
    throw new InvalidOperationException(
        "Authentication:SigningKey is missing or shorter than 32 characters, so access tokens "
        + "cannot be signed. Set one with: dotnet user-secrets set "
        + "\"Authentication:SigningKey\" \"<at least 32 random characters>\" --project "
        + "backend/TrustCabinet.Web. See docs/03-api/01-xac-thuc.md.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            // Every one of these is on by default except the key; they are spelled out because
            // switching any of them off is how a JWT ends up accepted from the wrong issuer, or
            // long after it expired.
            ValidateIssuer = true,
            ValidIssuer = authenticationOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = authenticationOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(authenticationOptions.SigningKey)),
            // Default is five minutes, which would keep an expired access token working for five
            // minutes past its expiry — the opposite of why the lifetime is short.
            ClockSkew = TimeSpan.Zero,
        };

        // ASP.NET Core rewrites "sub" and "email" into long WS-Federation URIs by default, so
        // FindFirstValue("sub") would return null. Off, so the claims read back as written.
        options.MapInboundClaims = false;
        // Browser WebSocket clients cannot set Authorization headers. Accept a query token only on
        // this hub path; all API endpoints still require the normal bearer header.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if ((context.Request.Path.StartsWithSegments("/hubs/notifications")
                    || context.Request.Path.StartsWithSegments("/hubs/meetings"))
                    && context.Request.Query.TryGetValue("access_token", out var token)
                    && token.Count == 1)
                {
                    context.Token = token[0];
                }

                return Task.CompletedTask;
            },
        };
    });

// One authorization policy per capability, so any endpoint group can ask for the one it needs with
// RequireCapability(...) — SCRUM-12's "every business endpoint checks permission". Each is gated on
// the capability, not on the role name: a capability moved to another role in role_capabilities
// then takes effect without touching this code (AccessEnums — "never checked by comparing against
// the role name in business code").
builder.Services.AddCapabilityPolicies();

var app = builder.Build();

// In development the app brings its own database up to date on boot, so nobody has to remember
// `dotnet ef database update` after pulling a migration, and a fresh clone gets demo rows to look
// at. Set Database:MigrateAndSeedOnStartup to false to take that over yourself.
//
// Never outside Development: a web app that migrates on boot will do it from every instance at
// once, and a deployment wants that step deliberate and reviewable — the `--migrate` switch above,
// which the test server's deploy runs before it starts the new version (ADR-0047).
if (app.Environment.IsDevelopment()
    && app.Configuration.GetValue<bool>("Database:MigrateAndSeedOnStartup"))
{
    await app.Services.MigrateAndSeedDevelopmentAsync();
}
else if (app.Configuration.GetValue<bool>("Database:SeedReferenceDataOnStartup"))
{
    // A safety net for the role ↔ capability matrix (ADR-0007). The model no longer declares it
    // via HasData, because SCRUM-60 lets an administrator edit it and HasData would overwrite
    // their changes on the next deploy. InitialCreate still inserts the same five rows and is
    // immutable, so a database migrated from scratch already has them and this does nothing —
    // it matters for one restored or provisioned without that migration's data, where an empty
    // role_capabilities would mean nobody holds AdminUsers and nobody could sign in to repair it.
    //
    // Opt-in rather than on by default, like the migrate step above: this opens a connection at
    // startup, and an application that does so unasked cannot be booted without a live database —
    // which is exactly what the in-memory endpoint tests do.
    //
    // It only inserts into a completely empty table, so it never touches an administrator's edits.
    await app.Services.SeedReferenceDataAsync();
}

// wwwroot holds the frontend build output (`npm run build` writes here) and is not committed.
// During development the frontend runs on Vite at http://localhost:5173 and proxies /api here,
// so wwwroot may be empty — UseStaticFiles then simply matches nothing.
//
// index.html names the bundles of the build it came with, so after a deploy the browser must ask for it again:
// without a Cache-Control it guessed a lifetime from Last-Modified and kept showing the previous release. no-cache
// lets it keep a copy but revalidate each time (a 304 when nothing changed). The bundles under /assets carry their
// content hash in their names, so they are kept for a year.
var frontendFiles = new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        var headers = context.Context.Response.Headers;
        if (context.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            headers.CacheControl = "no-cache";
        else if (context.Context.Request.Path.StartsWithSegments("/assets"))
            headers.CacheControl = "public, max-age=31536000, immutable";
    },
};
app.UseDefaultFiles();
app.UseStaticFiles(frontendFiles);

// Order matters: authentication reads the bearer token into HttpContext.User, authorization then
// decides whether that principal may proceed. Reversed, every RequireAuthorization endpoint would
// see an anonymous caller.
app.UseAuthentication();
app.UseAuthorization();

// POST /api/auth/login, /refresh, /logout and GET /api/auth/me — UC-01.
app.MapAuthEndpoints();

// /api/users — UC-05 and UC-06 (SCRUM-13). Every route requires the AdminUsers capability.
app.MapUserEndpoints();

// /api/roles/capabilities — UC-10 and UC-11 (SCRUM-60). Same gate as /api/users.
app.MapRolePermissionEndpoints();

// /api/roles — UC-12a … UC-12c, create / rename / delete a role (SCRUM-63). Same gate.
app.MapRoleEndpoints();

// /api/meetings — UC-14/UC-15 mutations (SCRUM-17) and UC-17 list (SCRUM-18).
app.MapMeetingEndpoints();
app.MapAgendaEndpoints();
app.MapParticipantEndpoints();
app.MapAttendanceEndpoints();
// SCRUM-28: votes during the meeting — drafts, open, ballots, close, conclusion.
app.MapVoteEndpoints();
// SCRUM-116: UC-04 — invitation link, one-time code, guest session.
app.MapGuestAccessEndpoints();
app.MapMeetingDocumentEndpoints();
// SCRUM-58: the personal library and its folders; SCRUM-23: comments on a meeting document.
app.MapPersonalLibraryEndpoints();
app.MapDocumentCommentEndpoints();
app.MapHub<MeetingHub>("/hubs/meetings", options => options.CloseOnAuthenticationExpiration = true);

// SCRUM-14: user-scoped notification list/read APIs and realtime delivery.
app.MapNotificationEndpoints();
app.MapHub<NotificationHub>("/hubs/notifications", options =>
    options.CloseOnAuthenticationExpiration = true);

// SCRUM-39/41: meeting call room — state, join token, chair mute, LiveKit webhook.
app.MapCallEndpoints();

// SCRUM-29: recordings — list, upload, signed playback, erase.
app.MapRecordingEndpoints();
app.MapSpeechClipEndpoints();

// SCRUM-31: transcript of a recording — read, re-run, correct a segment.
app.MapTranscriptEndpoints();
app.MapSummaryEndpoints();
app.MapMinutesEndpoints();

// SCRUM-35: the caller's own Voice ID.
app.MapVoiceIdEndpoints();

// SCRUM-15: Resend's delivery webhook (ADR-0031).
app.MapEmailEndpoints();

// Health endpoint. It answers 503 when the database cannot be reached, so the deploy's health check
// (ADR-0047) rolls a version back that started but cannot serve anything.
app.MapGet("/api/health", async (TrustCabinetDbContext db, CancellationToken ct) =>
{
    var databaseUp = await db.Database.CanConnectAsync(ct);
    return Results.Json(new
    {
        status = databaseUp ? "ok" : "unavailable",
        service = "TrustCabinet.Web",
        version = "0.1.0",
        database = databaseUp ? "ok" : "unreachable",
        timeUtc = DateTimeOffset.UtcNow
    }, statusCode: databaseUp ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
});

// The SPA uses client-side routing: any path that matches neither an API endpoint nor a static
// file falls back to index.html so React Router can handle it (e.g. loading /meetings directly).
//
// A bare catch-all would also answer a mistyped /api/* call — including one that fails a route
// constraint, like /api/users/notaguid — with 200 and the SPA's HTML (SCRUM-62). So the prefixes
// the Vite proxy forwards are excluded here, and an unmatched API or hub path gets routing's
// plain 404. Excluding beats mapping an /api/{**path} → 404 endpoint: that one would stay a
// candidate for real routes and win whenever the matcher discards them, e.g. a POST without a
// Content-Type to an endpoint that reads a JSON body would get 404 instead of 401.
app.MapFallbackToFile("{*path:nonfile:regex(^(?!(api|hubs)(/|$)))}", "index.html", frontendFiles);

app.Run();

// Program is implicitly internal when the file uses top-level statements. WebApplicationFactory
// in the test project needs a public entry-point type to spin the app up in memory.
public partial class Program;
