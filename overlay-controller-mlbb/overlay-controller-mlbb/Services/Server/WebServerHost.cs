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