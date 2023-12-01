using Dalamud.Interface.Utility;
using DeathRoll.Data;

namespace DeathRoll.Windows.Main;

public partial class MainWindow
{
    private bool MHeaderOpen;
    private int FieldSizeSelection;

    private void MinesweeperMode()
    {
        MinesweeperPanel();

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5.0f);

        MinesweeperWinnerPanel();

        var styleSpacing = ImGui.GetStyle().ItemSpacing.Y;
        var scaledSpacing = 20.0f * ImGuiHelpers.GlobalScale;
        var letterHeight = ImGui.CalcTextSize("X").Y;
        var headerHeight = letterHeight + scaledSpacing;
        var extraFields = Configuration.MinesweeperDif.GridSizes().Length;
        var textHeight = (letterHeight + styleSpacing) * (1.5f + extraFields) + (extraFields > 1 ? 5.0f : 0.0f);

        var spaceNeeded = headerHeight + (MHeaderOpen ? textHeight : 0);
        if (ImGui.BeginChild("GameField", new Vector2(0, -spaceNeeded)))
        {
            ImGuiHelpers.ScaledDummy(10.0f);
            MinesweeperFieldPanel();
        }
        ImGui.EndChild();

        if (ImGui.BeginChild("GameOptions", new Vector2(0, 0)))
            MinesweeperOptions();
        ImGui.EndChild();
    }

    private void MinesweeperPanel()
    {
        if (ImGui.Button("Show Settings"))
            Plugin.OpenConfig();

        var spacing = ImGui.GetScrollMaxY() == 0 ? 85.0f : 120.0f;
        ImGui.SameLine(ImGui.GetWindowWidth() - spacing);

        if (ImGui.Button("New Round"))
            Plugin.Minesweeper = new Minesweeper(Configuration.MinesweeperDif.GridSizes()[FieldSizeSelection]);
    }

    private void MinesweeperWinnerPanel()
    {
        ImGui.PushFont(Plugin.FontManager.Jetbrains22);
        SetCursorStart();
        var startPos = ImGui.GetCursorScreenPos();
        ImGui.TextUnformatted($"{Plugin.Minesweeper.MinesLeft:0#0}");

        if (Plugin.Minesweeper.GameOver)
        {
            ImGui.SameLine();
            Helper.SetTextCenter(Plugin.Minesweeper.PlayerWon ? "You won!!!" : "You lost!!!", Helper.Red);
        }

        ImGui.SameLine();
        var text = $"{Plugin.Minesweeper.Time:000}";
        var textWidth = ImGui.CalcTextSize(text).X;
        SetCursorEnd(startPos, textWidth);
        ImGui.TextUnformatted($"{Plugin.Minesweeper.Time:000}");
        ImGui.PopFont();
    }

    private void MinesweeperFieldPanel()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0,0));
        for (var row = 0; row < Plugin.Minesweeper.Rows; row++)
        {
            for (var col = 0; col < Plugin.Minesweeper.Cols; col++)
            {
                if (col > 0)
                    ImGui.SameLine(0,0);
                else
                    SetCursorStart();

                var clicked = CreateSquare(row, col);
                if (Plugin.Minesweeper.GameOver)
                    continue;

                if (clicked && ImGui.GetIO().MouseClicked[0])
                    Plugin.Minesweeper.ProcessLeftClick(row, col);

                // We do not allow flags before first click happened
                if (Plugin.Minesweeper.FirstClick)
                    continue;

                if (clicked && ImGui.GetIO().MouseClicked[1])
                    Plugin.Minesweeper.ProcessRightClick(row, col);
            }
        }
        ImGui.PopStyleVar();
    }

    private void MinesweeperOptions()
    {
        MHeaderOpen = ImGui.CollapsingHeader("Options##Minesweeper");
        if (!MHeaderOpen)
            return;

        var save = false;
        var longText = "Difficulty";
        var width = ImGui.CalcTextSize(longText).X + (20.0f * ImGuiHelpers.GlobalScale);

        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudViolet, "Difficulty");
        ImGui.SameLine(width);
        if (ImGui.BeginCombo($"##DifficultyCombo", Configuration.MinesweeperDif.Name()))
        {
            foreach (var difficulty in (Difficulty[]) Enum.GetValues(typeof(Difficulty)))
            {
                if (!ImGui.Selectable(difficulty.Name()))
                    continue;

                save = true;
                FieldSizeSelection = 0;
                Configuration.MinesweeperDif = difficulty;
            }

            ImGui.EndCombo();
        }

        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudViolet, "Field Size");
        foreach (var ((rows, cols, mines), idx) in Configuration.MinesweeperDif.GridSizes().Select((var, i) => (var, i)))
        {
            ImGui.SameLine(width);
            save |= ImGui.RadioButton($"{rows} x {cols} - {mines} Mines", ref FieldSizeSelection, idx);

            // Create a newline
            ImGui.Dummy(new Vector2(0,0));
        }

        if (save)
        {
            Plugin.Minesweeper = new Minesweeper(Configuration.MinesweeperDif.GridSizes()[FieldSizeSelection]);
            Configuration.Save();
        }
    }

    private void SetCursorStart()
    {
        var min = ImGui.GetCursorScreenPos();
        var windowX = ImGui.GetContentRegionAvail().X;
        var squareSize = 32 * ImGuiHelpers.GlobalScale;

        var sizeNeeded = squareSize * Plugin.Minesweeper.Cols;
        ImGui.SetCursorScreenPos(min with { X = min.X + (windowX - sizeNeeded) * 0.5f});
    }

    private void SetCursorEnd(Vector2 startPos, float textSize)
    {
        var squareSize = 32 * ImGuiHelpers.GlobalScale;
        var sizeNeeded = squareSize * Plugin.Minesweeper.Cols;
        ImGui.SetCursorScreenPos(startPos with { X = startPos.X + sizeNeeded - textSize});
    }

    private bool CreateSquare(int row, int col)
    {
        var min = ImGui.GetCursorScreenPos();
        var squareSize = 32 * ImGuiHelpers.GlobalScale;
        var max = new Vector2(min.X + squareSize, min.Y + squareSize);

        ImGui.PushID($"{row}{col}");
        ImGui.Dummy(max - min);
        var clicked = ImGui.IsItemClicked(ImGuiMouseButton.Left) || ImGui.IsItemClicked(ImGuiMouseButton.Right);
        var hovered = ImGui.IsItemHovered();
        ImGui.PopID();

        ImGui.SetCursorScreenPos(min);

        var square = Plugin.Minesweeper.Board[row, col];
        var darkRed = ImGui.GetColorU32(Helper.DarkRed);
        var lightGrey = ImGui.GetColorU32(ImGuiColors.DalamudGrey);
        var darkGrey = ImGui.GetColorU32(ImGuiColors.DalamudGrey3);
        if (square.State == Minesweeper.SquareState.Hidden)
        {
            if (hovered)
            {
                DrawRect(min, max, darkGrey, lightGrey);
            }
            else
            {
                DrawRect(min, max, lightGrey, darkGrey);
            }
        }
        else
        {
            DrawRect(min, max, !square.Exploded ? lightGrey : darkRed, darkGrey);
            ImGui.SetCursorScreenPos(min);

            var text = square.Symbol;

            ImGui.PushStyleColor(ImGuiCol.Text, square.NumberColor());
            ImGui.PushFont(square.UsesIconFont ? UiBuilder.IconFont : Plugin.FontManager.Jetbrains22);
            var textSize = ImGui.CalcTextSize(text);
            ImGui.SetCursorScreenPos(new Vector2(min.X + (squareSize - textSize.X) * 0.5f, min.Y + (squareSize - textSize.Y) * 0.5f));
            ImGui.TextUnformatted(text);
            ImGui.PopFont();
            ImGui.PopStyleColor();
        }

        ImGui.SetCursorScreenPos(min);
        ImGui.Dummy(max - min);

        return clicked;
    }

    private void DrawRect(Vector2 min, Vector2 max, uint fillColor, uint borderColor)
    {
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(min, max, fillColor);
        drawList.AddRect(min, max, borderColor);
    }
}