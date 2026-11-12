using MlbbOverlay.Models;

namespace MlbbOverlay.Services;

/// <summary>Holds the live match. Everything else in the app reads and changes the match through this.</summary>
public class MatchStateService
{
    public MatchState State { get; private set; } = new();

    public void ApplyTeamSetup(int? profileId, SeriesFormat format, TeamSnapshot team1, TeamSnapshot team2)
    {
        State.ProfileId = profileId;
        State.Format = format;
        State.Team1 = team1;
        State.Team2 = team2;
    }

    /// <summary>Used at startup to restore a saved match.</summary>
    public void Replace(MatchState state)
    {
        state.Normalize();
        State = state;
    }

    /// <summary>Starts a completely new match (keeps only the selected profile).</summary>
    public void Reset()
    {
        State = new MatchState { ProfileId = State.ProfileId };
    }
}