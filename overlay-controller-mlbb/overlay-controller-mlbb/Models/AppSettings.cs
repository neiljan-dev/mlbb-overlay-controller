namespace MlbbOverlay.Models;

public class AppSettings
{
    public string ObsHost { get; set; } = "127.0.0.1";
    public int ObsPort { get; set; } = 4455;
    public string ObsPassword { get; set; } = "";
    public bool AutoConnectObs { get; set; } = true;
    public int ServerPort { get; set; } = 5055;
}