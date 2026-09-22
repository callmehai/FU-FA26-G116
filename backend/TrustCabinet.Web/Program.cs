using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Access;
using TrustCabinet.Infrastructure.Authentication;
using TrustCabinet.Infrastructure.Users;
using TrustCabinet.Web;
using TrustCabinet.Web.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTrustCabinetDatabase(builder.Configuration);
builder.Services.AddTrustCabinetAuthentication(builder.Configuration);
builder.Services.AddTrustCabinetUserManagement();
builder.Services.AddTrustCabinetRolePermissions();

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
    });

builder.Services.AddAuthorization(options =>
    // User management is gated on the capability, not on the role name: a capability moved to
    // another role in role_capabilities then takes effect without touching this code
    // (AccessEnums — "never checked by comparing against the role name in business code").
    options.AddPolicy(
        UserEndpoints.AdminUsersPolicy,
        policy => policy.RequireClaim(
            JwtTokenIssuer.CapabilityClaim, Capability.AdminUsers.ToString())));

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
app.UseDefaultFiles();
app.UseStaticFiles();

// Order matters: authentication reads the bearer token into HttpContext.User, authorization then
// decides whether that principal may proceed. Reversed, every RequireAuthorization endpoint would
// see an anonymous caller.
app.UseAuthentication();
app.UseAuthorization();

// POST /api/auth/login, /refresh, /logout and GET /api/auth/me — UC-01.
app.MapAuthEndpoints();

// /api/users — UC-04 … UC-07 (SCRUM-13). Every route requires the AdminUsers capability.
app.MapUserEndpoints();

// /api/roles/capabilities — UC-09 and UC-10 (SCRUM-60). Same gate as /api/users.
app.MapRolePermissionEndpoints();

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
