using System.IO;
using System.Net;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using MlbbOverlay.Models;

namespace MlbbOverlay.Services;

/// <summary>Runs the small web server inside the app: serves the overlay page, images and the SignalR hub.</summary>
public partial class WebServerHost : ObservableObject
{
    private WebApplication? _app;
    private IHubContext<OverlayHub>? _hub;

    public ClientTracker Clients { get; } = new();
    public int Port { get; private set; }
    public string OverlayUrl => $"http://127.0.0.1:{Port}/overlay";

    /// <summary>Set by the app: returns the saved design (JSON text) of an overlay, or null.</summary>
    public Func<string, string?>? DesignProvider { get; set; }

    /// <summary>Set by the app: sample values for every placeholder (used by the editor).</summary>
    public Func<Dictionary<string, string>>? SampleDataProvider { get; set; }

    [ObservableProperty] private string _statusText = "Overlay server: starting...";
    [ObservableProperty] private bool _isRunning;

    private static readonly FileExtensionContentTypeProvider ContentTypes = CreateContentTypes();

    private static FileExtensionContentTypeProvider CreateContentTypes()
    {
        var provider = new FileExtensionContentTypeProvider();
        provider.Mappings[".webp"] = "image/webp";
        provider.Mappings[".webm"] = "video/webm";
        provider.Mappings[".woff2"] = "font/woff2";
        return provider;
    }

    // No caching, so edits to overlay files show up as soon as the page reloads
    private static StaticFileOptions Options(string? folder = null, string? requestPath = null)
    {
        var options = new StaticFileOptions
        {
            ContentTypeProvider = ContentTypes,
            OnPrepareResponse = ctx => ctx.Context.Response.Headers["Cache-Control"] = "no-store"
        };
        if (folder != null)
        {
            options.FileProvider = new PhysicalFileProvider(folder);
            options.RequestPath = requestPath ?? "";
        }
        return options;
    }

    public async Task StartAsync(int port, Func<SnapshotMessage> snapshotProvider)
    {
        Port = port;
        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ContentRootPath = AppContext.BaseDirectory,
                WebRootPath = AppPaths.WebRoot
            });

            builder.Logging.ClearProviders();

            // Listen only on this PC. For the phone remote (later) this becomes IPAddress.Any.
            builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, port));

            builder.Services.AddSingleton<SnapshotSource>(new SnapshotSource(snapshotProvider));
            builder.Services.AddSingleton<ClientTracker>(Clients);
            builder.Services.AddSignalR().AddJsonProtocol(o =>
            {
                o.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                o.PayloadSerializerOptions.DictionaryKeyPolicy = null;   // keep "blue.name" as it is
            });

            var app = builder.Build();

            app.UseStaticFiles(Options());                                         // wwwroot
            app.UseStaticFiles(Options(AppPaths.HeroImages, "/media/heroes"));     // hero pictures
            app.UseStaticFiles(Options(AppPaths.LogoImages, "/media/logos"));      // team logos
            app.UseStaticFiles(Options(AppPaths.OverlayAssets, "/media/assets"));  // images used by overlays

            app.MapGet("/overlay", async (HttpContext context) =>
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.Headers["Cache-Control"] = "no-store";
                await context.Response.SendFileAsync(Path.Combine(AppPaths.WebRoot, "overlay", "index.html"));
            });


            // ---- Editor ----
            app.MapGet("/editor", async (HttpContext context) =>
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.Headers["Cache-Control"] = "no-store";
                await context.Response.SendFileAsync(Path.Combine(AppPaths.WebRoot, "editor", "index.html"));
            });

            // The saved design of one overlay (404 = never designed)
            app.MapGet("/api/overlays/{key}", async (HttpContext context, string key) =>
            {
                var json = DesignProvider?.Invoke(key);
                if (json == null)
                {
                    context.Response.StatusCode = 404;
                    return;
                }
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.Headers["Cache-Control"] = "no-store";
                await context.Response.WriteAsync(json);
            });

            // Sample values for every placeholder
            app.MapGet("/api/dummydata", async (HttpContext context) =>
            {
                var data = SampleDataProvider?.Invoke() ?? new Dictionary<string, string>();
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.Headers["Cache-Control"] = "no-store";
                await context.Response.WriteAsync(JsonSerializer.Serialize(data));
            });

            // The images already uploaded for overlays
            app.MapGet("/api/assets", async (HttpContext context) =>
            {
                string[] images = [".png", ".jpg", ".jpeg", ".webp", ".gif", ".svg"];
                var files = new DirectoryInfo(AppPaths.OverlayAssets).GetFiles()
                    .Where(f => images.Contains(f.Extension.ToLowerInvariant()))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .Select(f => new { name = f.Name, url = "/media/assets/" + Uri.EscapeDataString(f.Name) });

                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.Headers["Cache-Control"] = "no-store";
                await context.Response.WriteAsync(JsonSerializer.Serialize(files));
            });

            // Upload an image (the file is the request body, the name is in ?name=)
            app.MapPost("/api/assets", async (HttpContext context) =>
            {
                string[] allowed = [".png", ".jpg", ".jpeg", ".webp", ".gif", ".svg", ".webm"];
                var rawName = context.Request.Query["name"].ToString();
                var extension = Path.GetExtension(rawName).ToLowerInvariant();

                if (!allowed.Contains(extension))
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("Unsupported file type.");
                    return;
                }

                var safe = string.Concat(Path.GetFileNameWithoutExtension(rawName)
                    .Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_'));
                if (safe.Length == 0) safe = "image";

                var fileName = $"{Guid.NewGuid().ToString("N")[..6]}_{safe}{extension}";
                await using (var file = File.Create(Path.Combine(AppPaths.OverlayAssets, fileName)))
                {
                    await context.Request.Body.CopyToAsync(file);
                }

                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(JsonSerializer.Serialize(
                    new { name = fileName, url = "/media/assets/" + Uri.EscapeDataString(fileName) }));
            });

            app.MapHub<OverlayHub>("/hub");

            await app.StartAsync();

            _app = app;
            _hub = app.Services.GetRequiredService<IHubContext<OverlayHub>>();
            IsRunning = true;
            StatusText = $"Overlay server: running on port {port}";
        }
        catch (Exception ex)
        {
            IsRunning = false;
            StatusText = "Overlay server: FAILED - " + ex.Message.Split('\n')[0];
        }
    }

    public async Task StopAsync()
    {
        var app = _app;
        _app = null;
        _hub = null;
        IsRunning = false;

        if (app == null) return;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await app.StopAsync(cts.Token);
            await app.DisposeAsync();
        }
        catch
        {
            // shutting down anyway
        }
    }

    /// <summary>Sends a command to every connected overlay page.</summary>
    public Task SendAsync(string method, object? payload = null)
    {
        var hub = _hub;
        if (hub == null) return Task.CompletedTask;

        return payload == null
            ? hub.Clients.All.SendAsync(method)
            : hub.Clients.All.SendAsync(method, payload);
    }
}