using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MlbbOverlay.Models;
using MlbbOverlay.ViewModels;

namespace MlbbOverlay.Services;

/// <summary>One thing a hotkey can do. RequiredState = it only works in that game state.</summary>
public record HotkeyAction(string Id, string Group, string Label, string Default, GameState? RequiredState = null);

/// <summary>Reads the keyboard while the Controller is open and runs the matching action.</summary>
public partial class HotkeyService : ObservableObject
{
    // ---------- All actions and their default keys ----------
    public static readonly IReadOnlyList<HotkeyAction> Actions = BuildActions();
    private static readonly Dictionary<string, HotkeyAction> ActionById = Actions.ToDictionary(a => a.Id);

    private static IReadOnlyList<HotkeyAction> BuildActions()
    {
        var list = new List<HotkeyAction>
        {
            new("panic", "General", "Hide all overlays (panic)", "Escape"),
            new("undo", "General", "Cancel last overlay", "Ctrl+Z"),
            new("restore", "General", "Restore the hidden overlay", "Ctrl+R"),

            new("state.draft", "State", "Switch to Draft Pick", "F1"),
            new("state.ingame", "State", "Switch to Ingame", "F2"),
            new("state.postgame", "State", "Switch to Postgame", "F3"),
            new("state.neiljan", "State", "Switch to Neiljan", "F4"),

            new("ingame.turtleSpawn", "Ingame", "Turtle spawn", "F5", GameState.Ingame),
            new("ingame.lordSpawn", "Ingame", "Lord spawn", "F6", GameState.Ingame),
            new("ingame.turtleSlain", "Ingame", "Turtle slain (then a player key)", "F7", GameState.Ingame),
            new("ingame.lordSlain", "Ingame", "Lord slain (then a player key)", "F8", GameState.Ingame),
            new("ingame.kill", "Ingame", "Kill (opens the window)", "F9", GameState.Ingame),
            new("ingame.victory", "Ingame", "Victory (opens the window)", "F11", GameState.Ingame),

            new("postgame.result", "Postgame", "Result", "Ctrl+D1", GameState.Postgame),
            new("postgame.mvp", "Postgame", "MVP", "Ctrl+D2", GameState.Postgame),
            new("postgame.status", "Postgame", "Status (score)", "Ctrl+D3", GameState.Postgame),

            new("neiljan.ad1", "Neiljan", "Ad 1 (timer)", "Ctrl+D4", GameState.Neiljan),
            new("neiljan.ad2", "Neiljan", "Ad 2 (timer)", "Ctrl+D5", GameState.Neiljan),
            new("neiljan.ad3", "Neiljan", "Ad 3 (loop)", "Ctrl+D6", GameState.Neiljan),
        };

        // Player keys: press one, then 2-5 (kill), or after Turtle/Lord slain
        var blueKeys = new[] { "Q", "W", "E", "R", "T" };
        var redKeys = new[] { "Y", "U", "I", "O", "P" };
        for (var i = 0; i < 5; i++)
            list.Add(new($"player.blue{i + 1}", "Players", $"Blue player {i + 1}", blueKeys[i], GameState.Ingame));
        for (var i = 0; i < 5; i++)
            list.Add(new($"player.red{i + 1}", "Players", $"Red player {i + 1}", redKeys[i], GameState.Ingame));

        return list;
    }

    // ---------- Fields ----------
    private readonly Navigator _navigator;
    private readonly SettingsService _settings;
    private readonly MatchStateService _match;
    private readonly GameActionService _actions;

    private readonly Dictionary<string, HotkeyGesture?> _bindings = new();
    private readonly Dictionary<string, Action> _handlers = new();
    private readonly DispatcherTimer _armTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    private readonly DispatcherTimer _hintTimer = new() { Interval = TimeSpan.FromSeconds(3) };

    private enum Armed { None, Turtle, Lord, Kill }
    private Armed _armed = Armed.None;
    private int _armedTeam;
    private int _armedPlayer;

    /// <summary>The message shown under the Controller's top bar.</summary>
    [ObservableProperty]
    private string _hintText = "";

    public HotkeyService(Navigator navigator, SettingsService settings,
                         MatchStateService match, GameActionService actions)
    {
        _navigator = navigator;
        _settings = settings;
        _match = match;
        _actions = actions;

        LoadBindings();

        _armTimer.Tick += (_, _) =>
        {
            Disarm();
            Flash("Quick flow timed out.");
        };
        _hintTimer.Tick += (_, _) =>
        {
            _hintTimer.Stop();
            if (_armed == Armed.None) HintText = "";
        };
    }

    /// <summary>Starts listening to the keyboard of this window.</summary>
    public void Attach(Window window) => window.PreviewKeyDown += OnPreviewKeyDown;

    // ---------- Handlers registered by the Controller screen ----------
    public void SetHandlers(IDictionary<string, Action> handlers)
    {
        foreach (var (id, handler) in handlers) _handlers[id] = handler;
    }

    // ---------- Bindings (used by the Settings window) ----------
    private void LoadBindings()
    {
        _bindings.Clear();
        foreach (var action in Actions)
        {
            var text = _settings.Current.Hotkeys.TryGetValue(action.Id, out var saved) ? saved : action.Default;
            _bindings[action.Id] = HotkeyGesture.TryParse(text, out var gesture) ? (HotkeyGesture?)gesture : null;
        }
    }

    public HotkeyGesture? GetBinding(string actionId) =>
        _bindings.TryGetValue(actionId, out var gesture) ? gesture : null;

    public void SetBinding(string actionId, HotkeyGesture? gesture)
    {
        _bindings[actionId] = gesture;
        _settings.Current.Hotkeys[actionId] = gesture?.ToString() ?? "";
        _settings.Save();
    }

    public void ResetAll()
    {
        _settings.Current.Hotkeys.Clear();
        _settings.Save();
        LoadBindings();
    }

    /// <summary>The id of the action using this key combination, or null.</summary>
    public string? FindAction(HotkeyGesture gesture) =>
        _bindings.FirstOrDefault(b => b.Value == gesture).Key;

    // ---------- Key helpers ----------
    public static bool IsModifierKey(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin
            or Key.System or Key.None or Key.ImeProcessed;

    /// <summary>2, 3, 4 or 5 (main keyboard or numpad). Returns 0 for any other key.</summary>
    public static int KillCountOf(Key key) => key switch
    {
        Key.D2 or Key.NumPad2 => 2,
        Key.D3 or Key.NumPad3 => 3,
        Key.D4 or Key.NumPad4 => 4,
        Key.D5 or Key.NumPad5 => 5,
        _ => 0
    };

    public static bool IsKillCountKey(HotkeyGesture gesture) =>
        gesture.Modifiers == ModifierKeys.None && KillCountOf(gesture.Key) != 0;

    // Don't react while you are typing or a drop-down list is open
    private static bool ShouldIgnore(IInputElement? focused) =>
        focused is TextBoxBase or PasswordBox or ComboBoxItem
        || focused is ComboBox { IsEditable: true }
        || focused is ComboBox { IsDropDownOpen: true };

    // ---------- Keyboard ----------
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_navigator.CurrentViewModel is not ControllerViewModel) return;
        if (e.IsRepeat) return;
        if (ShouldIgnore(Keyboard.FocusedElement)) return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (IsModifierKey(key)) return;

        var modifiers = Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift);
        if (Handle(new HotkeyGesture(key, modifiers))) e.Handled = true;
    }

    private bool Handle(HotkeyGesture gesture)
    {
        var id = FindAction(gesture);

        // Not a bound key: maybe the number that finishes a quick kill
        if (id == null) return HandleKillCount(gesture);

        // Escape: first cancel a pending quick flow, otherwise it is the panic button
        if (id == "panic" && _armed != Armed.None)
        {
            Disarm();
            Flash("Quick flow cancelled.");
            return true;
        }

        var action = ActionById[id];
        if (action.RequiredState is { } required && _match.State.CurrentState != required)
        {
            Flash($"{action.Label}: switch to {required} first.");
            return true;
        }

        if (id.StartsWith("player.")) return HandlePlayerKey(id);

        switch (id)
        {
            case "panic":
                Disarm();
                Fire(() => _actions.PanicAsync(), "All overlays hidden.");
                return true;
            case "undo":
                Disarm();
                Fire(() => _actions.CancelLastAsync(), "Cancelled the last overlay.");
                return true;
            case "restore":
                Disarm();
                Fire(() => _actions.RestoreAsync(), "Overlay restored.");
                return true;
            case "ingame.turtleSlain":
                Arm(Armed.Turtle, "Turtle slain: press the player key.   " + PlayerKeysHint() + "   (Esc cancels)");
                return true;
            case "ingame.lordSlain":
                Arm(Armed.Lord, "Lord slain: press the player key.   " + PlayerKeysHint() + "   (Esc cancels)");
                return true;
        }

        // Everything else is run by the Controller screen (same as clicking the button)
        if (_handlers.TryGetValue(id, out var handler))
        {
            Disarm();
            handler();
            return true;
        }
        return false;
    }

    private bool HandlePlayerKey(string id)
    {
        var side = id.StartsWith("player.blue") ? "blue" : "red";
        var index = int.Parse(id[^1].ToString()) - 1;
        var blueTeam = _match.State.BlueIsTeam1 ? 1 : 2;
        var team = side == "blue" ? blueTeam : 3 - blueTeam;
        var name = PlayerName(team, index);

        switch (_armed)
        {
            case Armed.Turtle:
                Disarm();
                Fire(() => _actions.TurtleSlainAsync(team, index), $"Turtle slain: {name}");
                return true;

            case Armed.Lord:
                Disarm();
                Fire(() => _actions.LordSlainAsync(team, index), $"Lord slain: {name}");
                return true;

            default:
                // First key of a quick kill: remember the player and wait for 2, 3, 4 or 5
                _armedTeam = team;
                _armedPlayer = index;
                Arm(Armed.Kill, $"Kill: {name} ({side}).   Press 2, 3, 4 or 5   (Esc cancels)");
                return true;
        }
    }

    private bool HandleKillCount(HotkeyGesture gesture)
    {
        if (_armed != Armed.Kill || gesture.Modifiers != ModifierKeys.None) return false;

        var count = KillCountOf(gesture.Key);
        if (count == 0) return false;

        var team = _armedTeam;
        var player = _armedPlayer;
        var name = PlayerName(team, player);

        Disarm();
        Fire(() => _actions.KillAsync(team, player, count), $"Kill x{count}: {name}");
        return true;
    }

    // ---------- Quick-flow state and messages ----------
    private void Arm(Armed kind, string hint)
    {
        _armed = kind;
        _armTimer.Stop();
        _armTimer.Start();
        HintText = hint;
    }

    private void Disarm()
    {
        _armTimer.Stop();
        _armed = Armed.None;
        HintText = "";
    }

    /// <summary>Shows a short message under the Controller's top bar.</summary>
    public void Flash(string text)
    {
        HintText = text;
        _hintTimer.Stop();
        _hintTimer.Start();
    }

    private async void Fire(Func<Task> action, string success)
    {
        try
        {
            await action();
            Flash(success);
        }
        catch (Exception ex)
        {
            Flash("Error: " + ex.Message);
        }
    }

    private string PlayerName(int teamIndex, int playerIndex)
    {
        var team = _match.State.TeamOf(teamIndex);
        return playerIndex < team.Players.Count && !string.IsNullOrWhiteSpace(team.Players[playerIndex])
            ? team.Players[playerIndex]
            : $"Player {playerIndex + 1}";
    }

    private string PlayerKeysHint()
    {
        string Keys(string side) => string.Join(" ",
            Enumerable.Range(1, 5).Select(i => GetBinding($"player.{side}{i}")?.Display ?? "-"));

        return $"Blue: {Keys("blue")}   Red: {Keys("red")}";
    }
}