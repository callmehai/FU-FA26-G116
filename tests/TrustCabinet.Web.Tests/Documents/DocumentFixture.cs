using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;
using TrustCabinet.Web.Tests.Agenda;

namespace TrustCabinet.Web.Tests.Documents;

/// <summary>A real PostgreSQL database and the app, with the object store replaced by <see cref="MemoryFileStorage"/>.</summary>
public sealed class DocumentFixture : IAsyncLifetime
{
    internal readonly PostgreSqlTestDatabase Database = new(PostgresTestDatabase.ConnectionString("trustcabinet_document_tests"));
    internal readonly MemoryFileStorage Storage = new();
    internal readonly SaveFailureInterceptor SaveFailure = new();
    internal readonly ReadFailureInterceptor ReadFailure = new();
    internal Factory Host = null!;

    internal TrustCabinetDbContext Db() => new(new DbContextOptionsBuilder<TrustCabinetDbContext>()
        .UseNpgsql(Database.DataSource, o => o.MigrationsHistoryTable("__ef_migrations_history"))
        .UseSnakeCaseNamingConvention().Options);

    public async Task InitializeAsync()
    {
        await using var db = Db();
        await db.Database.MigrateAsync();
        Host = new Factory(this);
        _ = Host.Server;
    }

    public async Task DisposeAsync() { await Host.DisposeAsync(); await Database.DisposeAsync(); }

    internal sealed class Factory(DocumentFixture fixture) : TrustCabinetApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TrustCabinetDbContext>();
                services.RemoveAll<DbContextOptions<TrustCabinetDbContext>>();
                services.AddDbContext<TrustCabinetDbContext>(o => o.UseNpgsql(fixture.Database.DataSource,
                    p => p.MigrationsHistoryTable("__ef_migrations_history")).UseSnakeCaseNamingConvention()
                    .AddInterceptors(fixture.SaveFailure, fixture.ReadFailure));
                services.RemoveAll<IFileStorage>();
                services.AddSingleton<IFileStorage>(fixture.Storage);
            });
        }
    }

    /// <summary>Fails the save that adds meeting documents, i.e. the transaction after the objects are up.</summary>
    internal sealed class SaveFailureInterceptor : SaveChangesInterceptor
    {
        internal bool Enabled;
        /// <summary>Fails the save that writes an audit row instead.</summary>
        internal bool FailAudit;
        /// <summary>Fails any save this says yes to (SCRUM-58, SCRUM-23).</summary>
        internal Func<DbContext, bool>? When;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => (Enabled && eventData.Context!.ChangeTracker.Entries<MeetingDocument>().Any(e => e.State == EntityState.Added))
                || (FailAudit && eventData.Context!.ChangeTracker.Entries<AuditLogEntry>().Any(e => e.State == EntityState.Added))
                || (When is { } when && when(eventData.Context!))
                ? throw new DbUpdateException("Injected persistence failure.") : ValueTask.FromResult(result);
    }

    /// <summary>Fails every query that reads the given table, and every bulk delete or update of it.</summary>
    internal sealed class ReadFailureInterceptor : DbCommandInterceptor
    {
        internal string? Table;
        public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command, CommandEventData eventData,
            InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
            => Table is { } table && command.CommandText.Contains(table, StringComparison.Ordinal)
                ? throw new Npgsql.NpgsqlException("Injected read failure.") : ValueTask.FromResult(result);

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(System.Data.Common.DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => Table is { } table && command.CommandText.Contains(table, StringComparison.Ordinal)
                ? throw new Npgsql.NpgsqlException("Injected write failure.") : ValueTask.FromResult(result);
    }

    internal sealed record Scenario(Guid MeetingId, Guid Secretary, Guid Chair, Guid Member, Guid Organizer, Guid Outsider)
    {
        internal string Url => $"/api/meetings/{MeetingId}/documents/upload";
        internal string ListUrl => $"/api/meetings/{MeetingId}/documents";
        internal string ContentUrl(Guid documentId, bool download = false)
            => $"/api/meetings/{MeetingId}/documents/{documentId}/content" + (download ? "?download=true" : "");
    }

    /// <summary>Planned start 18:30 UTC on 23/10 is 01:30 on 24/10 in Vietnam: the folder must say 24/10/2026.</summary>
    internal static readonly DateTimeOffset PlannedStart = new(2026, 10, 23, 18, 30, 0, TimeSpan.Zero);

    internal async Task<Scenario> Seed(MeetingStatus status = MeetingStatus.InProgress,
        Sensitivity sensitivity = Sensitivity.Normal)
    {
        await using var db = Db();
        var users = Enumerable.Range(0, 5).Select(i => new User { FullName = $"Document test {i}",
            Email = $"{Guid.NewGuid():N}@documents.test", PasswordHash = "test-only-non-login-hash" }).ToArray();
        db.Users.AddRange(users);
        var meeting = new Meeting { Title = "Họp giao ban", CreatedById = users[3].Id, Status = status,
            Sensitivity = sensitivity, PlannedStartTime = PlannedStart, PlannedEndTime = PlannedStart.AddHours(1) };
        db.Meetings.Add(meeting);
        db.MeetingParticipants.AddRange(
            new MeetingParticipant { MeetingId = meeting.Id, UserId = users[0].Id, Role = MeetingRole.Secretary },
            new MeetingParticipant { MeetingId = meeting.Id, UserId = users[1].Id, Role = MeetingRole.Chair },
            new MeetingParticipant { MeetingId = meeting.Id, UserId = users[2].Id, Role = MeetingRole.Member });
        await db.SaveChangesAsync();
        return new(meeting.Id, users[0].Id, users[1].Id, users[2].Id, users[3].Id, users[4].Id);
    }

    internal HttpClient Client(Guid actor)
    {
        var client = Host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AgendaFixture.Token(actor));
        return client;
    }

    internal HubConnection Hub(Guid actor) => new HubConnectionBuilder()
        .WithUrl("http://localhost/hubs/meetings", options =>
        {
            options.Transports = HttpTransportType.LongPolling;
            options.HttpMessageHandlerFactory = _ => Host.Server.CreateHandler();
            options.AccessTokenProvider = () => Task.FromResult<string?>(AgendaFixture.Token(actor));
        }).Build();

    internal static MultipartFormDataContent Form(params (string Name, byte[] Bytes, string? PageCount)[] files)
    {
        var form = new MultipartFormDataContent();
        foreach (var (name, bytes, pages) in files)
        {
            var part = new ByteArrayContent(bytes);
            // Deliberately wrong: the server must decide the type from the bytes (BR-215).
            part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(part, "files", name);
            form.Add(new StringContent(pages ?? ""), "pageCounts");
        }
        return form;
    }
}

[CollectionDefinition("Documents")]
public sealed class DocumentCollection : ICollectionFixture<DocumentFixture>;

/// <summary>Sample files built in memory, so no binary fixture has to be committed.</summary>
internal static class SampleFiles
{
    internal static byte[] Pdf(string text = "TrustCabinet")
        => Encoding.ASCII.GetBytes($"%PDF-1.7\n1 0 obj << /Type /Catalog >> endobj\n% {text}\n%%EOF\n");

    internal const string DocxMain = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";

    /// <param name="mainPart">Where the package relationship points; Word uses word/document.xml.</param>
    /// <param name="byExtension">Declare the main type as the Default for .xml rather than an Override,
    /// and start the XML parts with a UTF-8 BOM — what System.IO.Packaging and the Open XML SDK write.</param>
    /// <param name="strict">Use the ISO 29500 Strict relationship type, as Word's "Strict Open XML Document" does.</param>
    internal static byte[] Docx(string mainContentType = DocxMain, bool withDocument = true, string extraTypes = "",
        int padding = 0, string mainPart = "word/document.xml", bool byExtension = false, bool withRelationship = true,
        bool strict = false)
    {
        var relationshipType = strict ? "http://purl.oclc.org/ooxml/officeDocument/relationships/officeDocument"
            : "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument";
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            var declaration = byExtension ? $"<Default Extension=\"xml\" ContentType=\"{mainContentType}\"/>"
                : $"<Override PartName=\"/{mainPart}\" ContentType=\"{mainContentType}\"/>";
            Write(zip, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"utf-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
                + $"{extraTypes}{declaration}</Types>", byExtension);
            if (withRelationship)
                Write(zip, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"utf-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                    + $"<Relationship Id=\"rId1\" Type=\"{relationshipType}\" Target=\"{mainPart}\"/></Relationships>", byExtension);
            if (withDocument) Write(zip, mainPart, "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"/>");
            for (var i = 0; i < padding; i++) zip.CreateEntry($"pad/{i}");
        }
        return buffer.ToArray();
    }

    internal static byte[] Png() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52];

    private static void Write(ZipArchive zip, string name, string content, bool bom = false)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(bom));
        writer.Write(content);
    }
}

/// <summary>An object store in memory, with hooks to make a write fail or to act while one is in flight.</summary>
internal sealed class MemoryFileStorage : IFileStorage
{
    internal readonly ConcurrentDictionary<string, byte[]> Objects = new();
    internal bool Configured = true;
    internal Func<int, Task>? BeforeWrite;
    internal bool FailDelete;
    /// <summary>Keys whose delete fails, as if the store kept refusing just those.</summary>
    internal readonly ConcurrentDictionary<string, bool> FailDeleteFor = new();
    private int _writes;

    public bool IsConfigured => Configured;

    public async Task WriteAsync(string key, Stream content, long length, string contentType, CancellationToken ct)
    {
        if (BeforeWrite is { } hook) await hook(Interlocked.Increment(ref _writes));
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, ct);
        Assert.Equal(length, copy.Length);
        Objects[key] = copy.ToArray();
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken ct)
        => Objects.TryGetValue(key, out var bytes) ? Task.FromResult<Stream>(new MemoryStream(bytes))
            : throw new FileNotFoundException(key);

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        if (FailDelete || FailDeleteFor.ContainsKey(key)) throw new IOException("Injected delete failure.");
        Objects.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    internal void Reset()
    {
        BeforeWrite = null;
        FailDelete = false;
        FailDeleteFor.Clear();
        Configured = true;
        _writes = 0;
    }
}
