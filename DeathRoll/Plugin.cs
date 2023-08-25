﻿using System.Reflection;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using DeathRoll.Attributes;
using DeathRoll.Data;
using DeathRoll.Logic;
using DeathRoll.Windows.Bracket;
using DeathRoll.Windows.CardField;
using DeathRoll.Windows.Config;
using DeathRoll.Windows.Main;
using DeathRoll.Windows.Match;

namespace DeathRoll;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] public static Framework Framework { get; private set; } = null!;
    [PluginService] public static CommandManager Commands { get; private set; } = null!;
    [PluginService] public static DalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static ClientState ClientState { get; private set; } = null!;
    [PluginService] public static ChatGui Chat { get; private set; } = null!;
    [PluginService] public static TargetManager TargetManager { get; private set; } = null!;

    public string Name => "Death Roll Helper";

    public const string Authors = "Infi";
    public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";

    private readonly WindowSystem WindowSystem = new("DeathRoll Helper");
    public MainWindow MainWindow { get; init; }
    public ConfigWindow ConfigWindow { get; init; }
    private MatchWindow MatchWindow { get; init; }
    private BracketWindow BracketWindow { get; init; }
    private CardFieldWindow CardFieldWindow { get; init; }

    public readonly Configuration Configuration;
    public readonly RollManager RollManager;
    public readonly FontManager FontManager;

    public string LocalPlayer = string.Empty;
    public readonly Participants Participants;
    public GameState State = GameState.NotRunning;

    private readonly PluginCommandManager<Plugin> CommandManager;

    public Plugin()
    {
        FontManager = new FontManager();

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

        Participants = new Participants(Configuration);
        RollManager = new RollManager(this);

        MainWindow = new MainWindow(this);
        ConfigWindow = new ConfigWindow(this);
        MatchWindow = new MatchWindow(this);
        BracketWindow = new BracketWindow(this);
        CardFieldWindow = new CardFieldWindow(this);

        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MatchWindow);
        WindowSystem.AddWindow(BracketWindow);
        WindowSystem.AddWindow(CardFieldWindow);

        CommandManager = new PluginCommandManager<Plugin>(this, Commands);

        Chat.ChatMessage += OnChatMessage;
        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += OpenConfig;
        PluginInterface.UiBuilder.BuildFonts += FontManager.BuildFonts;
        PluginInterface.UiBuilder.RebuildFonts();
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        PluginInterface.UiBuilder.Draw -= DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;
        Chat.ChatMessage -= OnChatMessage;

        CommandManager.Dispose();

        PluginInterface.UiBuilder.BuildFonts -= FontManager.BuildFonts;
        PluginInterface.UiBuilder.RebuildFonts();
    }

    [Command("/drh")]
    [Aliases("/deathroll")]
    [HelpMessage("Toggles UI\nArguments:\non - Turns on\noff - Turns off\nconfig - Opens config\ntimer - Toggles timer")]
    public void PluginCommand(string _, string args)
    {
        switch (args)
        {
            case "on":
                Configuration.On = true;
                Configuration.Save();
                break;
            case "off":
                Configuration.On = false;
                Configuration.Save();
                break;
            case "config":
                ConfigWindow.IsOpen = true;
                break;
            case "timer":
                if (MainWindow.IsTimerActive())
                    MainWindow.StopTimer();
                else
                    MainWindow.BeginTimer();
                break;
            default:
                MainWindow.IsOpen = true;
                break;
        }
    }

    public static string GetTargetName()
    {
        var target = TargetManager.SoftTarget ?? TargetManager.Target;
        if (target is not PlayerCharacter pc || pc.HomeWorld.GameData == null)
            return string.Empty;

        return $"{pc.Name}\uE05D{pc.HomeWorld.GameData.Name}";
    }

    private void OnChatMessage(XivChatType type, uint id, ref SeString sender, ref SeString message, ref bool handled)
    {
        if (!Configuration.On || State is GameState.NotRunning or GameState.Done or GameState.Crash)
            return;

        var xivChatType = (ushort) type;
        var channel = xivChatType & 0x7F;

        if (Configuration.Debug)
        {
            PluginLog.Information("Chat Event fired.");
            PluginLog.Information($"Sender: {sender}.");
            PluginLog.Information($"Content: {message}.");
            PluginLog.Information($"ChatType: {type} Unmasked Channel: {channel}.");
            PluginLog.Information($"Language: {ClientState.ClientLanguage}.");
        }

        // 2122 = Random Roll 8266 = different Player Random roll?
        // Dice Roll: FC, LS, CWLS, Party
        if (!Enum.IsDefined(typeof(DeathRollChatTypes), xivChatType) && channel != 74)
            return;

        var dice = channel != 74;
        switch (dice)
        {
            case true when Configuration.OnlyRandom: // only /random is accepted
            case false when Configuration.OnlyDice: // only /dice is accepted
                return;
        }
        var m = Reg.Match(message.ToString(), ClientState.ClientLanguage, dice);
        if (!m.Success)
            return;

        var local = ClientState.LocalPlayer;
        if (local == null || local.HomeWorld.GameData?.Name == null)
        {
            PluginLog.Error("Unable to fetch character name.");
            return;
        }

        var diceCommand = 0;
        var playerName = $"{local.Name}\uE05D{local.HomeWorld.GameData.Name}";
        LocalPlayer = playerName;
        var isLocalPlayer = sender.ToString() == local.Name.ToString();
        if (!isLocalPlayer || dice)
        {
            var found = isLocalPlayer;
            foreach (var payload in message.Payloads) // try to get name and check for dice cheating
            {
                if (Configuration.Debug)
                    PluginLog.Information($"message: {payload}");
                switch (payload)
                {
                    case PlayerPayload playerPayload:
                        playerName = $"{playerPayload.PlayerName}\uE05D{playerPayload.World.Name}";
                        found = true;
                        break;
                    case IconPayload iconPayload:
                        switch (iconPayload.Icon)
                        {
                            case BitmapFontIcon.Dice:
                            case BitmapFontIcon.AutoTranslateBegin:
                            case BitmapFontIcon.AutoTranslateEnd:
                                diceCommand += 1;
                                break;
                        }

                        break;
                }
            }

            if (!found) // get playerName from payload
                foreach (var payload in sender.Payloads)
                {
                    if (Configuration.Debug)
                        PluginLog.Information($"Sender: {payload}");
                    playerName = payload switch
                    {
                        PlayerPayload playerPayload => $"{playerPayload.PlayerName}\uE05D{playerPayload.World.Name}",
                        _ => playerName
                    };
                }
        }

        if (Configuration.ActiveBlocklist && Configuration.SavedBlocklist.Contains(playerName))
        {
            if (Configuration.Debug)
                PluginLog.Information("Blocked player tried to roll.");
            return;
        }


        // dice always needs the autoTranslate payload
        // if not has a player just written the exact string
        if (dice && !DebugConfig.AllowDiceCheat && diceCommand != 3)
        {
            Chat.Print($"{playerName} tried to cheat~");
            return;
        }

        RollManager.ParseRoll(new Roll(m, playerName));
    }

    public void SwitchState(GameState newState)
    {
        State = newState;
        if (newState is GameState.NotRunning)
            Participants.Reset();
    }

    #region UI Toggles
    private void DrawUI() => WindowSystem.Draw();
    public void OpenMain() => MainWindow.IsOpen = true;
    public void OpenConfig() => ConfigWindow.IsOpen = true;
    public void OpenMatch() => MatchWindow.IsOpen = true;
    public void OpenBracket() => BracketWindow.IsOpen = true;
    public void OpenCardField() => CardFieldWindow.IsOpen = true;

    public void ToggleCardField() => CardFieldWindow.IsOpen ^= true;

    public void ClosePlayWindows()
    {
        MatchWindow.IsOpen = false;
        BracketWindow.IsOpen = false;
        CardFieldWindow.IsOpen = false;
    }
    #endregion
}