using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ExileCore2;
using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;
using ImGuiNET;
using CraftingCore.Compartments;
using CraftingCore.Classes;
using Vector2N = System.Numerics.Vector2;

namespace CraftingCore;

public class CraftingCore : BaseSettingsPlugin<CraftingSettings>
{
    public const string CoroutineName = "ExileCraftingCore";
    public static CraftingCore Main;

    public readonly List<Currency> CurrencyList = new()
    {
        new Currency("CurrencyUpgradeToMagic"),      // Orb of Transmutation
        new Currency("CurrencyAddModToMagic"),       // Orb of Augmentation
        new Currency("CurrencyUpgradeMagicToRare"),  // Regal Orb
        new Currency("CurrencyUpgradeToRare"),       // Alchemy Orb
        new Currency("CurrencyAddModToRare"),        // Exalted Orb
        new Currency("CurrencyCorrupt")              // Vaal Orb
    };

    private readonly Dictionary<string, string> CurrencyDisplayNames = new()
    {
        {"CurrencyUpgradeToMagic", "Orb of Transmutation"},
        {"CurrencyAddModToMagic", "Orb of Augmentation"},
        {"CurrencyUpgradeMagicToRare", "Regal Orb"},
        {"CurrencyUpgradeToRare", "Alchemy Orb"},
        {"CurrencyAddModToRare", "Exalted Orb"},
        {"CurrencyCorrupt", "Vaal Orb"}
    };

    public Vector2N ClickWindowOffset;
    public List<CraftingResult> CraftableItems;

    public CraftingCore()
    {
        Name = "CraftingCore";
    }

    public override bool Initialise()
    {
        Main = this;
        Settings.Enable.OnValueChanged += (sender, b) =>
        {
            if (b)
                CraftingCoRoutine.InitCraftingCoRoutine();
            else
                TaskRunner.Stop(CoroutineName);
        };

        // Register the hotkey
        Input.RegisterKey(Settings.CraftHotkey);
        Settings.CraftHotkey.OnValueChanged += () =>
        {
            Input.RegisterKey(Settings.CraftHotkey);
        };

        return true;
    }

    public override void DrawSettings()
    {
        base.DrawSettings();
        DrawCraftingSettings();
    }

    private void DrawCraftingSettings()
    {
        ImGui.TextColored(new System.Numerics.Vector4(0f, 1f, 0.022f, 1f), "Crafting Settings");

        var alchemyCraftOnly = Settings.AlchemyCraftOnly.Value;
        if (ImGui.Checkbox("Enable Alchemy Craft Only", ref alchemyCraftOnly))
        {
            Settings.AlchemyCraftOnly.Value = alchemyCraftOnly;
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Enable this to only craft with Alchemy, Exalts, and Vaal Orbs");
        }


        foreach (var currency in CurrencyList)
        {
            if (!Settings.CurrencyEnabled.ContainsKey(currency.Name))
            {
                Settings.CurrencyEnabled[currency.Name] = new ToggleNode(true);  // Set to true by default
            }

            var enabled = Settings.CurrencyEnabled[currency.Name].Value;
            string displayName = CurrencyDisplayNames.TryGetValue(currency.Name, out var name) ? name : currency.Name;
            if (ImGui.Checkbox($"Enable {displayName}", ref enabled))
            {
                Settings.CurrencyEnabled[currency.Name].Value = enabled;
            }
        }
    }

    public override void Render()
    {
        if (!Settings.Enable) return;

        if (!CraftingRequirementsMet())
        {
            TaskRunner.Stop(CoroutineName);
            return;
        }

        // Check for hotkey press
        if (Settings.CraftHotkey.PressedOnce())
        {
            if (Settings.DebugEnabled) LogMessage("Craft hotkey pressed");
            if (TaskRunner.Has(CoroutineName))
            {
                if (Settings.DebugEnabled) LogMessage("Stopping existing crafting routine");
                CraftingCoRoutine.StopCoroutine(CoroutineName);
            }
            else
            {
                if (Settings.DebugEnabled) LogMessage("Starting new crafting routine");
                CraftingCoRoutine.StartCraftingCoroutine();
            }
        }

        // Emergency cleanup hotkey
        if (Settings.EmergencyCleanupHotkey.PressedOnce())
        {
            if (Settings.DebugEnabled) LogMessage("Emergency cleanup triggered!");
            TaskRunner.Stop(CoroutineName);
            ActionsHandler.CleanUp();
        }
    }

    public bool CraftingRequirementsMet()
    {
        return GameController.Game.IngameState.IngameUi.InventoryPanel.IsVisible &&
               GameController.Game.IngameState.IngameUi.StashElement.IsVisibleLocal;
    }
}

