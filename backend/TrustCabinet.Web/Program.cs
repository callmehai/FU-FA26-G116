using TrustCabinet.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTrustCabinetDatabase(builder.Configuration);

var app = builder.Build();

// In development the app brings its own database up to date on boot, so nobody has to remember
// `dotnet ef database update` after pulling a migration, and a fresh clone gets demo rows to look
// at. Set Database:MigrateAndSeedOnStartup to false to take that over yourself.
//
// Never outside Development: a web app that migrates on boot will do it from every instance at
// once, and a deployment wants that step deliberate and reviewable.
if (app.Environment.IsDevelopment()
    && app.Configuration.GetValue<bool>("Database:MigrateAndSeedOnStartup"))
{
    await app.Services.MigrateAndSeedDevelopmentAsync();
}

// wwwroot holds the frontend build output (`npm run build` writes here) and is not committed.
// During development the frontend runs on Vite at http://localhost:5173 and proxies /api here,
// so wwwroot may be empty — UseStaticFiles then simply matches nothing.
app.UseDefaultFiles();
app.UseStaticFiles();

// Health endpoint — the frontend calls this to confirm the backend is alive.
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    service = "TrustCabinet.Web",
    version = "0.1.0",
    timeUtc = DateTimeOffset.UtcNow
}));

// The SPA uses client-side routing: any path that matches neither an API endpoint nor a static
// file falls back to index.html so React Router can handle it (e.g. loading /meetings directly).
// The fallback has the lowest priority, so it never swallows /api/* routes.
app.MapFallbackToFile("index.html");

app.Run();

// Program is implicitly internal when the file uses top-level statements. WebApplicationFactory
// in the test project needs a public entry-point type to spin the app up in memory.
public partial class Program;
