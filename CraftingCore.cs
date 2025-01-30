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
using ExileCore2.Shared;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.Elements.InventoryElements;
using ExileCore2.PoEMemory.MemoryObjects;
using System.Drawing;

namespace CraftingCore
{
    public class CraftingCore : BaseSettingsPlugin<CraftingSettings>
    {
        public const string CoroutineName = "Auto Craft";
        public static CraftingCore Main;
        public readonly List<Currency> CurrencyList = new()
        {
            new Currency("CurrencyUpgradeToMagic"),      // Orb of Transmutation
            new Currency("CurrencyAddModToMagic"),       // Orb of Augmentation
            new Currency("CurrencyUpgradeMagicToRare"),  // Regal Orb
            new Currency("CurrencyAddModToRare"),        // Exalted Orb
            new Currency("CurrencyCorrupt"),             // Vaal Orb
            new Currency("CurrencyUpgradeToRare")        // Alchemy Orb
        };

        private readonly Dictionary<string, string> CurrencyDisplayNames = new()
        {
            {"CurrencyUpgradeToMagic", "Orb of Transmutation"},
            {"CurrencyAddModToMagic", "Orb of Augmentation"},
            {"CurrencyUpgradeMagicToRare", "Regal Orb"},
            {"CurrencyAddModToRare", "Exalted Orb"},
            {"CurrencyCorrupt", "Vaal Orb"},
            {"CurrencyUpgradeToRare", "Orb of Alchemy"}
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

            Input.RegisterKey(Settings.CraftHotkey);
            Settings.CraftHotkey.OnValueChanged += () => { Input.RegisterKey(Settings.CraftHotkey); };

            WaystoneScoring.ParseBannedModifiers();

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
                    Settings.CurrencyEnabled[currency.Name] = new ToggleNode(true);
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

            if (Settings.EmergencyCleanupHotkey.PressedOnce())
            {
                LogMessage("Emergency cleanup triggered!");
                TaskRunner.Stop(CoroutineName);
                ActionsHandler.CleanUp();
            }

            if (!Settings.CraftHotkey.PressedOnce()) return;

            if (TaskRunner.Has(CoroutineName))
                CraftingCoRoutine.StopCoroutine(CoroutineName);
            else
                CraftingCoRoutine.StartCraftingCoroutine();

            RenderWaystoneHighlights();
        }

        private void RenderWaystoneHighlights()
        {
            /*if (!Settings.Enable || !Settings.WaystoneHighlight.Enable) return;

            var stashPanel = GameController.Game.IngameState.IngameUi.StashElement;
            var inventoryPanel = GameController.Game.IngameState.IngameUi.InventoryPanel;

            if (inventoryPanel.IsVisible)
            {
                RenderWaystonesInPanel(inventoryPanel.VisibleInventoryItems);

                if (stashPanel.IsVisible && stashPanel.VisibleStash != null)
                {
                    RenderWaystonesInPanel(stashPanel.VisibleStash.VisibleInventoryItems);
                }
            }*/
        }

        /*private void RenderWaystonesInPanel(IEnumerable<NormalInventoryItem> items)
        {
            foreach (var item in items)
            {
                var entity = item.Item;
                if (!ItemManager.IsWaystone(entity)) continue;

                var score = WaystoneScoring.CalculateWaystoneScore(entity);
                var rect = item.GetClientRect();

                if (score >= Settings.WaystoneHighlight.Score.MinimumRunHighlightScore.Value)
                {
                    DrawHighlight(rect, Settings.WaystoneHighlight.Graphics.RunHighlightColor.Value, Settings.WaystoneHighlight.Graphics.RunHightlightStyle.Value);
                }
                else if (score >= Settings.WaystoneHighlight.Score.MinimumCraftHighlightScore.Value)
                {
                    DrawHighlight(rect, Settings.WaystoneHighlight.Graphics.CraftHighlightColor.Value, Settings.WaystoneHighlight.Graphics.CraftHightlightStyle.Value);
                }

                DrawWaystoneInfo(rect, score, entity);
            }
        }

        private void DrawHighlight(ExileCore2.Shared.RectangleF rect, Color color, int style)
        {
            if (style == 1) // Border
            {
                Graphics.DrawFrame(rect, color, Settings.WaystoneHighlight.Graphics.BorderHighlight.RunBorderThickness.Value);
            }
            else if (style == 2) // Filled box
            {
                Graphics.DrawBox(rect, color);
            }
        }*/

        private void DrawWaystoneInfo(ExileCore2.Shared.RectangleF rect, int score, Entity item)
        {
            var mods = item.GetComponent<Mods>();
            var mapComponent = item.GetComponent<Map>();

            if (mods == null || mapComponent == null) return;

            /* Graphics.DrawText(score.ToString(), new Vector2N(rect.Center.X, rect.Bottom - 20), Color.White,
                 Settings.WaystoneHighlight.Graphics.FontSize.ScoreFontSizeMultiplier.Value);

             Graphics.DrawText($"T{mapComponent.Tier}", new Vector2N(rect.Left + 5, rect.Top + 5), Color.Yellow,
                 Settings.WaystoneHighlight.Graphics.FontSize.QRFontSizeMultiplier.Value);

             var modCount = ItemManager.GetModCount(mods);
             Graphics.DrawText($"{modCount}/6", new Vector2N(rect.Right - 20, rect.Top + 5), Color.Cyan,
                 Settings.WaystoneHighlight.Graphics.FontSize.PrefSuffFontSizeMultiplier.Value);*/
        }

        public bool CraftingRequirementsMet()
        {
            return GameController.Game.IngameState.IngameUi.InventoryPanel.IsVisible;
        }
    }
}

