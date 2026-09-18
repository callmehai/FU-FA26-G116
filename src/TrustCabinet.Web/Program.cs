var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// Trang tĩnh trong wwwroot: "/" -> wwwroot/index.html
app.UseDefaultFiles();
app.UseStaticFiles();

// Endpoint kiểm tra sức khoẻ — trang chủ gọi vào đây để xác nhận backend sống.
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    service = "TrustCabinet.Web",
    version = "0.1.0",
    timeUtc = DateTimeOffset.UtcNow
}));

app.Run();
