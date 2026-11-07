namespace MlbbOverlay.Models;

public static class OverlayLayers
{
    public const string Base = "base";    //long appear overlys
    public const string Event = "event";   //short appear overlays
}


public class ShowMessage
{
    public string Id { get; set; } = "";
    public string Key { get; set; } = "";                     
    public string Layer { get; set; } = OverlayLayers.Event;
    public int DurationMs { get; set; }                    
    public Dictionary<string, string> Data { get; set; } = new();  
}


public class SnapshotMessage
{
    public ShowMessage? Base { get; set; }
}