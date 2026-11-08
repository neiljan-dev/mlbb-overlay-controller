using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MlbbOverlay.Models;
using MlbbOverlay.Services;

namespace MlbbOverlay.ViewModels;

public record QueueModeOption(QueueMode Value, string Label);

/// <summary>One line of the hotkey list.</summary>
public partial class HotkeyRowViewModel : ObservableObject
{
    public string Id { get; }
    public string Group { get; }
    public string Label { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Shown))]
    private string _gestureText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Shown))]
    private bool _isCapturing;

    public string Shown => IsCapturing
        ? "Press a key..."
        : string.IsNullOrEmpty(GestureText) ? "(none)" : GestureText;

    public HotkeyRowViewModel(string id, string group, string label)
    {
        Id = id;
        Group = group;
        Label = label;
    }
}

/// <summary>The Controller's Settings window: queue behavior and hotkeys.</summary>
public partial class ControllerSettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly HotkeyService _hotkeys;
    private HotkeyRowViewModel? _capturing;

    public IReadOnlyList<QueueModeOption> QueueModes { get; } = new[]
    {
        new QueueModeOption(QueueMode.Sequential, "One after another (queue)"),
        new QueueModeOption(QueueMode.Replace, "The newest replaces the one on screen")
    };

    [ObservableProperty] private QueueMode _selectedQueueMode;
    [ObservableProperty] private string _eventSeconds;
    [ObservableProperty] private string _maxQueue;
    [ObservableProperty] private string _message = "";

    public ObservableCollection<HotkeyRowViewModel> Rows { get; } = new();

    public ControllerSettingsViewModel(SettingsService settings, HotkeyService hotkeys)
    {
        _settings = settings;
        _hotkeys = hotkeys;

        var s = settings.Current;
        _selectedQueueMode = s.QueueMode;
        _eventSeconds = (s.EventDurationMs / 1000.0).ToString(CultureInfo.InvariantCulture);
        _maxQueue = s.MaxQueueLength.ToString();

        foreach (var action in HotkeyService.Actions)
            Rows.Add(new HotkeyRowViewModel(action.Id, action.Group, action.Label));
        RefreshRows();
    }

    private void RefreshRows()
    {
        foreach (var row in Rows)
            row.GestureText = _hotkeys.GetBinding(row.Id)?.Display ?? "";
    }

    // ---------- Queue ----------

    [RelayCommand]
    private void ApplyQueue()
    {
        if (!double.TryParse(EventSeconds.Trim().Replace(',', '.'), NumberStyles.Float,
                             CultureInfo.InvariantCulture, out var seconds) || seconds < 0.5 || seconds > 30)
        {
            Message = "Seconds on screen must be a number from 0.5 to 30.";
            return;
        }
        if (!int.TryParse(MaxQueue.Trim(), out var max) || max < 1 || max > 50)
        {
            Message = "Max waiting must be a whole number from 1 to 50.";
            return;
        }

        var s = _settings.Current;
        s.QueueMode = SelectedQueueMode;
        s.EventDurationMs = (int)Math.Round(seconds * 1000);
        s.MaxQueueLength = max;
        _settings.Save();
        Message = "Queue settings saved.";
    }

    // ---------- Hotkeys ----------

    private void StopCapture()
    {
        if (_capturing != null) _capturing.IsCapturing = false;
        _capturing = null;
    }

    [RelayCommand]
    private void StartCapture(HotkeyRowViewModel? row)
    {
        if (row == null) return;
        StopCapture();
        _capturing = row;
        row.IsCapturing = true;
        Message = "Press the new key (Esc cancels).";
    }

    [RelayCommand]
    private void ClearHotkey(HotkeyRowViewModel? row)
    {
        if (row == null) return;
        StopCapture();
        _hotkeys.SetBinding(row.Id, null);
        row.GestureText = "";
        Message = $"\"{row.Label}\" has no key now.";
    }

    [RelayCommand]
    private void ResetHotkeys()
    {
        StopCapture();
        _hotkeys.ResetAll();
        RefreshRows();
        Message = "Hotkeys are back to the defaults.";
    }

    /// <summary>Called by the window for every key press. Returns true if the key was used to set a hotkey.</summary>
    public bool HandleKey(Key key, ModifierKeys modifiers)
    {
        var row = _capturing;
        if (row == null) return false;

        if (key == Key.Escape && modifiers == ModifierKeys.None)
        {
            StopCapture();
            Message = "Cancelled.";
            return true;
        }

        var gesture = new HotkeyGesture(key, modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift));

        if (HotkeyService.IsKillCountKey(gesture))
        {
            Message = "The keys 2, 3, 4 and 5 are reserved for kill counts. Try another key.";
            return true;
        }

        var other = _hotkeys.FindAction(gesture);
        if (other != null && other != row.Id)
        {
            var otherLabel = HotkeyService.Actions.First(a => a.Id == other).Label;
            Message = $"{gesture.Display} is already used by \"{otherLabel}\".";
            return true;
        }

        _hotkeys.SetBinding(row.Id, gesture);
        row.GestureText = gesture.Display;
        StopCapture();
        Message = $"\"{row.Label}\" is now {gesture.Display}.";
        return true;
    }
}