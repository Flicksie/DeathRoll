using System.Text.RegularExpressions;

using DeathRoll.Data;

namespace DeathRoll.Logic;

public class RollManager
{
    private readonly Plugin Plugin;
    public readonly SimpleTournament SimpleTournament;

    public RollManager(Plugin plugin)
    {
        Plugin = plugin;
        SimpleTournament = new SimpleTournament(plugin);
    }

    public void ParseRoll(Roll roll)
    {
        try
        {
            switch (Plugin.Configuration.GameMode)
            {
                case GameModes.Venue:
                    NormalGameMode(roll);
                    break;
                case GameModes.DeathRoll:
                    DeathRollGameMode(roll);
                    break;
                case GameModes.Tournament:
                    SimpleTournament.Parser(roll);
                    break;
                case GameModes.Blackjack:
                    Plugin.Blackjack.Parser(roll);
                    break;
                default:
                    return;
            }
        }
        catch (FormatException e)
        {
            Plugin.Chat.PrintError("Unable to parse roll.");
            Plugin.Log.Error(e.ToString());
        }
    }

    private void NormalGameMode(Roll roll)
    {
        var exists = Plugin.Participants.PList.Exists(x => x.Name == roll.PlayerName);
        switch (Plugin.Configuration.RerollAllowed)
        {
            case false when exists:
            {
                Plugin.Log.Information("Player already rolled, no reroll allowed.");
                return;
            }
            case true when exists:
                Plugin.Participants.DeleteEntry(roll.PlayerName);
                break;
        }

        Highlight? highlight = null;
        if (Plugin.Configuration is { ActiveHighlighting: true, SavedHighlights.Count: > 0 })
            highlight = Plugin.Configuration.SavedHighlights.FirstOrDefault(hl => hl.Matches(roll.Result));

        Plugin.Participants.Add(new Participant(roll, highlight));
        Plugin.Participants.UpdateSorting();
        Plugin.Participants.IsOutOfUsed = Plugin.Participants.PList.Any(x => x.OutOf > -1);
    }

    private void DeathRollGameMode(Roll roll)
    {
        // check that player not in playerList and new players not get accepted
        if (Plugin.Participants.PlayerNameList.All(x => x != roll.PlayerName) && !Plugin.Configuration.AcceptNewPlayers)
            return;

        // check if rolls exists, if not, add first roll with OutOf 1000
        if (Plugin.Participants.PList.Count == 0)
        {
            if (roll.OutOf == -1) roll.OutOf = 1000;
            Plugin.Participants.Add(new Participant(roll));
            return;
        }

        // check if same player and check if OutOf is correct
        // deathroll rules: first roll /random all following /random {PreviousNumber}
        if (roll.PlayerName == Plugin.Participants.Last.Name || Plugin.Participants.Last.Roll != roll.OutOf)
            return;

        Plugin.Participants.Add(new Participant(roll));
        if (roll.Result >= 2)
            return;

        // player lost this round
        Plugin.Configuration.AcceptNewPlayers = false;
        Plugin.Configuration.Save();
        Plugin.SwitchState(GameState.Done);
    }
}

public class Roll
{
    public int Result = 1000;
    public int OutOf = 1000;
    public string PlayerName;

    private Roll(string name)
    {
        PlayerName = name;
    }

    public Roll(string fullName, int roll, int outOf)
    {
        Result = roll;
        OutOf = outOf != 0 ? outOf : -1;
        PlayerName = fullName;
    }

    public static Roll Dummy(string name = "Unknown") => new(name);
}