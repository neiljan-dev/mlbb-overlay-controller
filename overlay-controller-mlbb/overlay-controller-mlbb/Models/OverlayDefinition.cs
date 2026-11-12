namespace MlbbOverlay.Models;

/// <summary>An overlay designed in the editor. DesignJson holds the elements (see wwwroot/overlay/renderer.js).</summary>
public class OverlayDefinition
{
    public int Id { get; set; }
    public string Key { get; set; } = "";            // for example "1.turtle"
    public string DisplayName { get; set; } = "";
    public string Layer { get; set; } = OverlayLayers.Event;
    public string DesignJson { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
}