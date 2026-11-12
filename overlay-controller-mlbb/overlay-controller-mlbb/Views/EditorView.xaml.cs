using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Wpf;
using MlbbOverlay.Services;
using MlbbOverlay.ViewModels;

namespace MlbbOverlay.Views;

public partial class EditorView : UserControl, IEditorBridge
{
    private bool _started;

    public EditorView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_started || DataContext is not EditorViewModel viewModel) return;
        _started = true;
        viewModel.Bridge = this;

        try
        {
            // WebView2 keeps its files in %AppData%\MlbbOverlay\webview2
            Browser.CreationProperties = new CoreWebView2CreationProperties
            {
                UserDataFolder = Path.Combine(AppPaths.Root, "webview2")
            };
            await Browser.EnsureCoreWebView2Async();
            Browser.CoreWebView2.Navigate(viewModel.EditorUrl);
        }
        catch (Exception ex)
        {
            viewModel.Message = "The editor could not start: " + ex.Message + " (is the WebView2 Runtime installed?)";
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is EditorViewModel viewModel) viewModel.Bridge = null;
        Browser.Dispose();
    }

    // ---- IEditorBridge: talk to the editor page ----

    public async Task<string?> GetDesignJsonAsync()
    {
        if (Browser.CoreWebView2 == null) return null;

        var raw = await Browser.CoreWebView2.ExecuteScriptAsync(
            "window.editor ? JSON.stringify(window.editor.getDesign()) : null");

        // The answer is a JSON string that contains our JSON text
        return string.IsNullOrEmpty(raw) || raw == "null" ? null : JsonSerializer.Deserialize<string>(raw);
    }

    public async Task<bool> IsDirtyAsync()
    {
        if (Browser.CoreWebView2 == null) return false;
        var raw = await Browser.CoreWebView2.ExecuteScriptAsync("window.editor ? window.editor.isDirty() : false");
        return raw == "true";
    }

    public async Task MarkCleanAsync()
    {
        if (Browser.CoreWebView2 == null) return;
        await Browser.CoreWebView2.ExecuteScriptAsync("window.editor && window.editor.markClean()");
    }
}