using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Infrastructure;

namespace TrustCabinet.Web.Tests;

/// <summary>
/// SCRUM-143: the dependency rules of the four backend projects, A1–A7 of
/// <c>docs/02-design/11-quy-chuan-clean-architecture.md</c> (rule R13), checked by reflection over the built
/// assemblies.
///
/// The backend is being moved to these rules module by module, so each rule has a list of the types that
/// still break it, filled at CA-00 with every violation there was. The lists only ever shrink: a subtask
/// deletes the entries of the module it moves and never adds one, and <see cref="Listed_exceptions_still_break_their_rule"/>
/// fails when an entry no longer breaks its rule, so a fixed type cannot stay listed and quietly break it again.
/// A new violation is fixed, not listed.
///
/// Entries are full type names in ordinal order. A nested or compiler-generated type (a lambda, an async
/// method) counts as the top-level type it is written in. Reflection reads signatures and fields, not method
/// bodies, so the half of A6 that R1 words as "no <c>using TrustCabinet.Infrastructure</c>" reads the source
/// files instead and lists paths.
/// </summary>
public sealed class ArchitectureTests
{
    private const BindingFlags Declared =
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly Assembly Domain = typeof(Meeting).Assembly;
    private static readonly Assembly Application = typeof(IUnitOfWork).Assembly;
    private static readonly Assembly Infrastructure = typeof(TrustCabinetDbContext).Assembly;
    private static readonly Assembly Web = typeof(Program).Assembly;

    /// <summary>The only packages Application may reference (rule R1).</summary>
    private static readonly string[] ApplicationPackages =
    [
        "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.Logging.Abstractions",
        "Microsoft.Extensions.Options",
    ];

    /// <summary>The only Web files that may name Infrastructure: the composition root and <c>--migrate</c>.</summary>
    private static readonly string[] CompositionRoots = ["DatabaseMigrationCommand.cs", "Program.cs"];

    // A3 — outside Infrastructure, nothing has a field or a parameter of type TrustCabinetDbContext.
    private static readonly string[] DbContextOutsideInfrastructure =
    [
        "Program",
        "TrustCabinet.Web.AgendaChangePublisher",
        "TrustCabinet.Web.AttendanceChangePublisher",
        "TrustCabinet.Web.MeetingDocumentChangePublisher",
        "TrustCabinet.Web.NotesChangePublisher",
        "TrustCabinet.Web.SpeakingChangePublisher",
        "TrustCabinet.Web.SpeechChangePublisher",
        "TrustCabinet.Web.VoteChangePublisher",
    ];

    // A4 — no class of Infrastructure implements an I*Service declared in Domain or Application: a use case lives in Application.
    private static readonly string[] ServicesInInfrastructure =
    [
        "TrustCabinet.Infrastructure.Ai.AiSettingsService",
        "TrustCabinet.Infrastructure.Summaries.MinutesService",
        "TrustCabinet.Infrastructure.Summaries.SummaryService",
        "TrustCabinet.Infrastructure.Transcription.TranscriptionService",
        "TrustCabinet.Infrastructure.VoiceId.VoiceCollectionService",
        "TrustCabinet.Infrastructure.VoiceId.VoiceIdService",
    ];

    // A5 — no interface but IUnitOfWork has SaveChangesAsync or InTransactionAsync.
    private static readonly string[] InterfacesExposingTheUnitOfWork =
    [
        "TrustCabinet.Application.Meetings.IMeetingRepository",
        "TrustCabinet.Application.Transcription.ITranscriptRepository",
        "TrustCabinet.Application.VoiceId.IVoiceIdRepository",
    ];

    // A6 — a Web type has no field or parameter of an I*Repository type. CapabilityGate and NotificationWorker
    // resolve theirs with GetRequiredService inside a method: reflection sees them only because the local lives
    // across an await and so becomes a field. CA-18 and CA-20 confirm with git grep that none is left.
    private static readonly string[] WebTypesTakingRepositories =
    [
        "TrustCabinet.Web.AgendaChangePublisher",
        "TrustCabinet.Web.AttendanceChangePublisher",
        "TrustCabinet.Web.Endpoints.AuthEndpoints",
        "TrustCabinet.Web.Endpoints.CapabilityGate",
        "TrustCabinet.Web.MeetingDocumentChangePublisher",
        "TrustCabinet.Web.MeetingHub",
        "TrustCabinet.Web.NotesChangePublisher",
        "TrustCabinet.Web.NotificationHub",
        "TrustCabinet.Web.NotificationWorker",
        "TrustCabinet.Web.SpeakingChangePublisher",
        "TrustCabinet.Web.SpeechChangePublisher",
        "TrustCabinet.Web.VoteChangePublisher",
    ];

    // A6 — a Web source file names nothing of TrustCabinet.Infrastructure, CompositionRoots apart. Paths under
    // backend/TrustCabinet.Web. Read from the source, because a `new`, a constant or a static call leaves no trace
    // in a signature.
    private static readonly string[] WebFilesNamingInfrastructure =
    [
        "AttendanceChangePublisher.cs",
        "Endpoints/AuthEndpoints.cs",
        "Endpoints/EmailEndpoints.cs",
        "Endpoints/UserEndpoints.cs",
        "MeetingGuestIdentity.cs",
        "MeetingHub.cs",
        "NotesChangePublisher.cs",
        "PdfPageCounter.cs",
        "SpeakingChangePublisher.cs",
        "SpeechChangePublisher.cs",
        "VoteChangePublisher.cs",
    ];

    // A7 — a class of Infrastructure that takes TrustCabinetDbContext is a *Repository or a *Query, or sits in
    // Persistence/ or Seed/, or is TrustCabinetDbContext* or DependencyInjection. The classes below are neither yet:
    // each moves its use case to Application, or becomes one of those, in the subtask of its module. Empty at CA-20.
    private static readonly string[] PendingDbContextReaders =
    [
        "TrustCabinet.Infrastructure.Ai.AiSettingsService",
        "TrustCabinet.Infrastructure.Ai.LanguageModelProvider",
        "TrustCabinet.Infrastructure.Email.EmailQueueProcessor",
        "TrustCabinet.Infrastructure.Email.MeetingInvitationDispatchProcessor",
        "TrustCabinet.Infrastructure.Email.MeetingInvitationEvents",
        "TrustCabinet.Infrastructure.Email.ResendWebhookReceiver",
        "TrustCabinet.Infrastructure.Summaries.MeetingTranscriptReader",
        "TrustCabinet.Infrastructure.Summaries.MinutesService",
        "TrustCabinet.Infrastructure.Summaries.SummaryService",
        "TrustCabinet.Infrastructure.VoiceId.VoiceCollectionService",
    ];

    // A7 — for good: a class that implements one port of Application over the context and has no business branch.
    // Each entry carries, on its own line, the reason it cannot be a repository or a query.
    private static readonly string[] AllowedDbContextAdapters =
    [
        // Stages the roster notice through the Email helper, which CA-17 owns.
        "TrustCabinet.Infrastructure.Email.MeetingInvitationStager",
        // The live guest-session check behind every guest read (agenda, roster, attendance, hub, call room,
        // documents): it only composes the current-session query with the meeting, with no branch of its own.
        "TrustCabinet.Infrastructure.GuestAccess.GuestSessionAccess",
    ];

    [Fact]
    public void A1_Domain_references_only_the_framework()
        => Assert.Empty(ReferencedOutsideTheFramework(Domain));

    /// <summary>
    /// From CA-02 every service, repository and port is declared in Application. <c>IPdfInspector</c> stays because
    /// <c>DocumentFileRules</c> takes it as a parameter, and Domain cannot reference Application (rule R3).
    /// </summary>
    [Fact]
    public void A1_Domain_declares_no_port_service_or_repository()
    {
        var breaking = OwnTypes(Domain)
            .Where(type => (type.IsInterface && type != typeof(TrustCabinet.Domain.Documents.IPdfInspector))
                // A generic type's name ends in `n (FooRepository`1); judge the name without it.
                || type.Name.Split('`')[0].EndsWith("Service", StringComparison.Ordinal)
                || type.Name.Split('`')[0].EndsWith("Repository", StringComparison.Ordinal))
            .Select(type => type.FullName!).Order(StringComparer.Ordinal).ToArray();

        Assert.True(breaking.Length == 0,
            "Domain holds entities, enums, rules and policies only (rule R3). Move these to Application:\n"
            + string.Join("\n", breaking));
    }

    [Fact]
    public void A2_Application_references_only_Domain_and_its_three_packages()
    {
        Assert.Empty(ReferencedOutsideTheFramework(Application).Except([Domain.GetName().Name!, .. ApplicationPackages]));

        // The assembly lists only what the code already uses; the project file is where a package first appears.
        var project = XDocument.Load(Path.Combine(
            RepositoryRoot(), "backend", "TrustCabinet.Application", "TrustCabinet.Application.csproj"));
        Assert.Empty(Includes(project, "PackageReference").Except(ApplicationPackages));
        Assert.Equal(["TrustCabinet.Domain.csproj"], Includes(project, "ProjectReference").Select(path => path.Split('\\', '/')[^1]));
    }

    [Fact]
    public void A3_Only_Infrastructure_takes_the_DbContext()
        => AssertOnlyListed(DbContextOutsideInfrastructure, BreakingA3());

    [Fact]
    public void A4_No_use_case_service_is_implemented_in_Infrastructure()
        => AssertOnlyListed(ServicesInInfrastructure, BreakingA4());

    [Fact]
    public void A5_Only_the_unit_of_work_saves_and_opens_transactions()
        => AssertOnlyListed(InterfacesExposingTheUnitOfWork, BreakingA5());

    [Fact]
    public void A6_Web_takes_no_repository()
        => AssertOnlyListed(WebTypesTakingRepositories, BreakingA6Repositories());

    [Fact]
    public void A6_Web_names_Infrastructure_only_in_the_composition_root()
        => AssertOnlyListed(WebFilesNamingInfrastructure, BreakingA6Infrastructure());

    [Fact]
    public void A7_In_Infrastructure_only_repositories_queries_and_persistence_take_the_DbContext()
        => AssertOnlyListed([.. PendingDbContextReaders, .. AllowedDbContextAdapters], BreakingA7());

    [Fact]
    public void Listed_exceptions_still_break_their_rule()
    {
        var fixedSince = new (string Rule, string[] Listed, string[] Breaking)[]
        {
            ("A3", DbContextOutsideInfrastructure, BreakingA3()),
            ("A4", ServicesInInfrastructure, BreakingA4()),
            ("A5", InterfacesExposingTheUnitOfWork, BreakingA5()),
            ("A6 repositories", WebTypesTakingRepositories, BreakingA6Repositories()),
            ("A6 Infrastructure", WebFilesNamingInfrastructure, BreakingA6Infrastructure()),
            ("A7 pending", PendingDbContextReaders, BreakingA7()),
            ("A7 allowed", AllowedDbContextAdapters, BreakingA7()),
        }.SelectMany(rule => rule.Listed.Except(rule.Breaking).Select(entry => $"{rule.Rule}: {entry}")).ToArray();

        Assert.True(fixedSince.Length == 0,
            "These no longer break the rule they are listed under. Delete them from the list:\n"
            + string.Join("\n", fixedSince));
    }

    [Fact]
    public void Exception_lists_are_sorted_and_name_each_type_once()
    {
        foreach (var list in new[]
        {
            DbContextOutsideInfrastructure, ServicesInInfrastructure, InterfacesExposingTheUnitOfWork, WebTypesTakingRepositories,
            WebFilesNamingInfrastructure, PendingDbContextReaders, AllowedDbContextAdapters,
        })
        {
            Assert.Equal(list.Distinct().Order(StringComparer.Ordinal), list);
        }

        Assert.Empty(PendingDbContextReaders.Intersect(AllowedDbContextAdapters));
    }

    private static string[] BreakingA3()
        => new[] { Domain, Application, Web }.SelectMany(OwnTypes)
            .Where(type => Takes(type).Contains(typeof(TrustCabinetDbContext)))
            .Select(Owner).Distinct().Order(StringComparer.Ordinal).ToArray();

    private static string[] BreakingA4()
        => OwnTypes(Infrastructure)
            .Where(type => type.IsClass && type.GetInterfaces().Any(i => IsContract(i, "Service")))
            .Select(Owner).Distinct().Order(StringComparer.Ordinal).ToArray();

    private static string[] BreakingA5()
        => new[] { Domain, Application }.SelectMany(OwnTypes)
            .Where(type => type.IsInterface && type != typeof(IUnitOfWork)
                && type.GetMethods().Any(method => method.Name is "SaveChangesAsync" or "InTransactionAsync"))
            .Select(Owner).Distinct().Order(StringComparer.Ordinal).ToArray();

    private static string[] BreakingA6Repositories()
        => OwnTypes(Web)
            .Where(type => Takes(type).Any(taken => IsContract(taken, "Repository")))
            .Select(Owner).Distinct().Order(StringComparer.Ordinal).ToArray();

    private static string[] BreakingA6Infrastructure()
    {
        var web = Path.Combine(RepositoryRoot(), "backend", "TrustCabinet.Web");
        return Directory.EnumerateFiles(web, "*.cs", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(web, file).Replace('\\', '/'))
            .Where(file => !file.StartsWith("bin/", StringComparison.Ordinal) && !file.StartsWith("obj/", StringComparison.Ordinal))
            .Except(CompositionRoots)
            // A using directive or a qualified name; a sentence in a comment that mentions the project is neither.
            .Where(file => Regex.IsMatch(File.ReadAllText(Path.Combine(web, file)), @"TrustCabinet\.Infrastructure(;|\.\w)"))
            .Order(StringComparer.Ordinal).ToArray();
    }

    private static string[] BreakingA7()
        => OwnTypes(Infrastructure)
            .Where(type => Takes(type).Contains(typeof(TrustCabinetDbContext)))
            .Select(TopLevel).Distinct()
            .Where(type => !(type.Name.EndsWith("Repository", StringComparison.Ordinal)
                || type.Name.EndsWith("Query", StringComparison.Ordinal)
                || type.Name.StartsWith(nameof(TrustCabinetDbContext), StringComparison.Ordinal)
                || type == typeof(DependencyInjection)
                || InNamespace(type, "TrustCabinet.Infrastructure.Persistence")
                || InNamespace(type, "TrustCabinet.Infrastructure.Seed")))
            .Select(type => type.FullName!).Order(StringComparer.Ordinal).ToArray();

    private static void AssertOnlyListed(string[] listed, string[] breaking)
    {
        var unlisted = breaking.Except(listed).ToArray();

        Assert.True(unlisted.Length == 0,
            "These break a Clean Architecture rule (docs/02-design/11-quy-chuan-clean-architecture.md, R13). "
            + "Fix them; the exception lists only shrink:\n" + string.Join("\n", unlisted));
    }

    /// <summary>The types written in this repository: not what a source generator or the compiler adds to the assembly.</summary>
    private static IEnumerable<Type> OwnTypes(Assembly assembly)
        => assembly.GetTypes().Where(type =>
        {
            var top = TopLevel(type);
            return !top.Name.StartsWith('<') && (top.Namespace is null || InNamespace(top, "TrustCabinet"));
        });

    private static Type TopLevel(Type type)
    {
        while (type.DeclaringType is { } outer) type = outer;
        return type;
    }

    private static string Owner(Type type) => TopLevel(type).FullName!;

    private static bool InNamespace(Type type, string root)
        => type.Namespace == root || type.Namespace?.StartsWith(root + ".", StringComparison.Ordinal) == true;

    /// <summary>An <c>I…Service</c> or <c>I…Repository</c> interface declared in Domain or Application.</summary>
    private static bool IsContract(Type type, string suffix)
        => type.IsInterface && (type.Assembly == Domain || type.Assembly == Application)
            && type.Name.StartsWith('I') && type.Name.EndsWith(suffix, StringComparison.Ordinal);

    /// <summary>
    /// What a type is given: the types of its fields and of the parameters of its constructors and methods, the
    /// arguments of generic ones included. A lambda's captures and an async method's locals are fields of a nested
    /// compiler-generated type, which is how the handler of an endpoint is seen.
    /// </summary>
    private static IEnumerable<Type> Takes(Type type)
        => type.GetFields(Declared).Select(field => field.FieldType)
            .Concat(type.GetConstructors(Declared).SelectMany(constructor => constructor.GetParameters()).Select(p => p.ParameterType))
            .Concat(type.GetMethods(Declared).SelectMany(method => method.GetParameters()).Select(p => p.ParameterType))
            .SelectMany(Unwrap);

    private static IEnumerable<Type> Unwrap(Type type)
    {
        if (type.HasElementType) return Unwrap(type.GetElementType()!);
        return type.IsGenericType ? type.GetGenericArguments().SelectMany(Unwrap).Prepend(type) : [type];
    }

    /// <summary>Judged by where the assembly loads from, not its name: a NuGet package may be called System.* too.</summary>
    private static IEnumerable<string> ReferencedOutsideTheFramework(Assembly assembly)
    {
        var runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        return assembly.GetReferencedAssemblies()
            .Where(reference => Path.GetDirectoryName(Assembly.Load(reference).Location) != runtime)
            .Select(reference => reference.Name!);
    }

    private static IEnumerable<string> Includes(XDocument project, string element)
        => project.Descendants().Where(e => e.Name.LocalName == element).Select(e => (string)e.Attribute("Include")!);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TrustCabinet.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("TrustCabinet.sln not found above the test output.");
    }
}
