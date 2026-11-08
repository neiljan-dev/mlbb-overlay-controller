namespace MlbbOverlay.Models;

public enum QueueMode { Sequential, Replace }

/// <summary>Saved in %AppData%\MlbbOverlay\settings.json.</summary>
public class AppSettings
{
    public string ObsHost { get; set; } = "127.0.0.1";
    public int ObsPort { get; set; } = 4455;
    public string ObsPassword { get; set; } = "";
    public bool AutoConnectObs { get; set; } = true;
    public int ServerPort { get; set; } = 5055;

    // Queue for the short overlays (turtle, lord, kills, win)
    public QueueMode QueueMode { get; set; } = QueueMode.Sequential;
    public int EventDurationMs { get; set; } = 2000;
    public int MaxQueueLength { get; set; } = 10;

    // Hotkeys: action id -> key text such as "Ctrl+Z". Empty text = no key. Missing = the default.
    public Dictionary<string, string> Hotkeys { get; set; } = new();
}