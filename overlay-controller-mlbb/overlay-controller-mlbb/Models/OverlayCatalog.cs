namespace MlbbOverlay.Models;

/// <summary>One overlay in the Overlay Animation list.</summary>
public record OverlayInfo(string Key, string Label, string Layer);

/// <summary>Every overlay the app can show. The editor (Milestone 9) will add the ones you design.</summary>
public static class OverlayCatalog
{
    public static readonly IReadOnlyList<OverlayInfo> All = new List<OverlayInfo>
    {
        new("draftpick.ov", "Draft pick (stays)", OverlayLayers.Base),
        new("ingame.ov", "Ingame (stays)", OverlayLayers.Base),

        new("1.turtle", "Turtle slain: Blue", OverlayLayers.Event),
        new("2.turtle", "Turtle slain: Red", OverlayLayers.Event),
        new("1.lord", "Lord slain: Blue", OverlayLayers.Event),
        new("2.lord", "Lord slain: Red", OverlayLayers.Event),
        new("turtle.spawn", "Turtle spawn", OverlayLayers.Event),
        new("lord.spawn", "Lord spawn", OverlayLayers.Event),

        new("kill.2", "Kill x2", OverlayLayers.Event),
        new("kill.3", "Kill x3", OverlayLayers.Event),
        new("kill.4", "Kill x4", OverlayLayers.Event),
        new("kill.5", "Kill x5", OverlayLayers.Event),

        new("1.win", "Victory: Blue", OverlayLayers.Event),
        new("2.win", "Victory: Red", OverlayLayers.Event),

        new("result.ov", "Result (stays)", OverlayLayers.Base),
        new("mvp.ov", "MVP (stays)", OverlayLayers.Base),
        new("status.ov", "Status (stays)", OverlayLayers.Base),

        new("ad1", "Ad 1, 2 min timer (stays)", OverlayLayers.Base),
        new("ad2", "Ad 2, 2 min timer (stays)", OverlayLayers.Base),
        new("ad3", "Ad 3 (stays)", OverlayLayers.Base),
    };
}