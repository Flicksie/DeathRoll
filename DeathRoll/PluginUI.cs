﻿using System;
using System.Numerics;
using DeathRoll.Gui;
using ImGuiNET;

namespace DeathRoll;

// It is good to have this be disposable in general, in case you ever need it
// to do any cleanup
public class PluginUI : IDisposable
{
    public Participants Participants;
    public Configuration Configuration;

    private bool settingsVisible;

    // this extra bool exists for ImGui, since you can't ref a property
    private bool visible;

    // passing in the image here just for simplicityw
    public PluginUI(Configuration configuration, Participants p)
    {
        this.Configuration = configuration;
        this.Participants = p;
        
        Blocklist = new Blocklist(configuration);
        GeneralSettings = new GeneralSettings(configuration);
        Highlights = new Highlights(this);
        RollTable = new RollTable(this);

        // needs RollTable
        TimerSetting = new TimerSetting(configuration, RollTable);
    }

    private Blocklist Blocklist { get; }
    private GeneralSettings GeneralSettings { get; }
    private Highlights Highlights { get; }
    public RollTable RollTable { get; }
    public TimerSetting TimerSetting { get; }

    public bool Visible
    {
        get => visible;
        set => visible = value;
    }

    public bool SettingsVisible
    {
        get => settingsVisible;
        set => settingsVisible = value;
    }

    public void Dispose()
    {
    }

    public void Draw()
    {
        // This is our only draw handler attached to UIBuilder, so it needs to be
        // able to draw any windows we might have open.
        // Each method checks its own visibility/state to ensure it only draws when
        // it actually makes sense.
        // There are other ways to do this, but it is generally best to keep the number of
        // draw delegates as low as possible.

        DrawMainWindow();
        DrawSettingsWindow();
    }

    public void DrawMainWindow()
    {
        if (!Visible) return;

        ImGui.SetNextWindowSize(new Vector2(375, 480), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(375, 480), new Vector2(float.MaxValue, float.MaxValue));
        if (ImGui.Begin("DeathRoll Helper", ref visible))
        {
            RollTable.RenderControlPanel();

            ImGui.Spacing();

            if (Participants.PList.Count > 0)
            {
                RollTable.RenderRollTable();
                ImGui.Dummy(new Vector2(0.0f, 60.0f));
                RollTable.RenderDeletionDropdown();
            }
        }

        ImGui.End();
    }

    public void DrawSettingsWindow()
    {
        if (!SettingsVisible) return;

        ImGui.SetNextWindowSize(new Vector2(260, 310), ImGuiCond.Always);
        if (ImGui.Begin("DRH Config", ref settingsVisible, ImGuiWindowFlags.NoResize))
            if (ImGui.BeginTabBar("##settings-tabs"))
            {
                // Renders General Settings UI
                GeneralSettings.RenderGeneralSettings();

                // Renders Timer Settings Tab
                TimerSetting.RenderTimerSettings();

                // Renders Highlight Settings Tab
                Highlights.RenderHightlightsTab();

                // Renders Blocklist UI
                Blocklist.RenderBlocklistTab();

                ImGui.EndTabBar();
            }

        ImGui.End();
    }
}