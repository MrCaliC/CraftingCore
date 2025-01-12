using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;
using System.Collections.Generic;
using System.Windows.Forms;

public class CraftingSettings : ISettings
{
    [Menu("Craft Hotkey")]
    public HotkeyNode CraftHotkey { get; set; } = Keys.F4;

    [Menu("Emergency Cleanup Hotkey", "Press this if keyboard gets stuck")]
    public HotkeyNode EmergencyCleanupHotkey { get; set; } = Keys.F6;

    [Menu("Extra Delay", "Delay between crafting attempts (in ms).")]
    public RangeNode<int> ExtraDelay { get; set; } = new(100, 0, 2000);

    [Menu("Click Delay", "Delay between currency and item clicks (in ms).")]
    public RangeNode<int> ClickDelay { get; set; } = new(100, 0, 2000);

    public ToggleNode Enable { get; set; } = new(false);

    // Dictionary to track enabled status for each currency
    public Dictionary<string, ToggleNode> CurrencyEnabled { get; set; } = new();

    [Menu("Enable Debug Logging")]
    public ToggleNode DebugEnabled { get; set; } = new ToggleNode(false);

}