using Zero.Server.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes.Concat(
        ["application/octet-stream"]);
});

var app = builder.Build();

app.UseResponseCompression();
app.UseStaticFiles();
app.UseBlazorFrameworkFiles();

app.MapHub<GameHub>("/gamehub");
app.MapFallbackToFile("index.html");

app.Run();
