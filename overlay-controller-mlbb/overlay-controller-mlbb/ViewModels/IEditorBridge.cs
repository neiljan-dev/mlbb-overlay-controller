namespace MlbbOverlay.ViewModels;

/// <summary>What the editor view can do for the ViewModel (talk to the editor page inside WebView2).</summary>
public interface IEditorBridge
{
    Task<string?> GetDesignJsonAsync();
    Task<bool> IsDirtyAsync();
    Task MarkCleanAsync();
}