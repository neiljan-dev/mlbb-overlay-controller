namespace MlbbOverlay.Models;

/// <summary>One player button in the "choose a player" window.</summary>
public record PlayerChoiceItem(int TeamIndex, int PlayerIndex, string Ign, string? IconFile, string HeroName);

/// <summary>The result of the "choose a player" window.</summary>
public record PlayerChoice(int TeamIndex, int PlayerIndex, int KillCount);

public record TeamChoiceItem(int TeamIndex, string Name, string? LogoFile, string SideText, string SideColor);

/// <summary>Left = Blue side, Right = Red side (like the stream).</summary>
public record TeamChoiceModel(string Title, TeamChoiceItem Left, TeamChoiceItem Right);

/// <summary>One entry of the state switch.</summary>
public record StateOption(GameState Value, string Label);