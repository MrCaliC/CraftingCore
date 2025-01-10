using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared;
using ExileCore2.Shared.Enums;
using CraftingCore.Classes;
using static CraftingCore.CraftingCore;

namespace CraftingCore.Compartments;

internal static class ItemManager
{
    public static async SyncTask<bool> ParseItems()
    {
        var serverData = Main.GameController.Game.IngameState.Data.ServerData;
        if (serverData == null)
        {
            Main.LogMessage("Server data is null");
            return false;
        }

        var inventoryItems = serverData.PlayerInventories[0]?.Inventory?.InventorySlotItems;
        if (inventoryItems == null)
        {
            Main.LogMessage("Inventory items are null");
            return false;
        }

        Main.LogMessage($"Found {inventoryItems?.Count ?? 0} total inventory items");

        await TaskUtils.CheckEveryFrameWithThrow(() => inventoryItems != null, new CancellationTokenSource(500).Token);
        Main.CraftableItems = new List<CraftingResult>();
        Main.ClickWindowOffset = Main.GameController.Window.GetWindowRectangle().TopLeft;

        foreach (var invItem in inventoryItems)
        {
            if (invItem.Item == null || invItem.Address == 0) continue;

            var item = invItem.Item;
            var mods = item.GetComponent<Mods>();
            var base_item = item.GetComponent<Base>();

            if (!IsCraftable(item))
            {
                Main.LogMessage($"Skipping non-craftable item: {item.Path}");
                continue;
            }

            var currency = DetermineBestCurrency(item);
            if (currency == null)
            {
                Main.LogMessage($"No suitable currency found for item: {item.Path}");
                continue;
            }

            // Debug the settings state
            Main.LogMessage($"Checking if currency {currency.Name} is enabled in settings");
            var isEnabled = false;
            if (Main.Settings.CurrencyEnabled.TryGetValue(currency.Name, out var enabled))
            {
                isEnabled = enabled.Value;
                Main.LogMessage($"Currency {currency.Name} enabled state: {isEnabled}");
            }
            else
            {
                Main.LogMessage($"Currency {currency.Name} not found in settings");
            }

            // Only add if currency is enabled or if settings entry doesn't exist
            if (isEnabled || !Main.Settings.CurrencyEnabled.ContainsKey(currency.Name))
            {
                Main.CraftableItems.Add(new CraftingResult
                {
                    Currency = currency,
                    ItemClickPos = invItem.GetClientRect().Center,
                    Item = item
                });
                Main.LogMessage($"Added craftable item using {currency.Name}");
            }
            else
            {
                Main.LogMessage($"Skipping item because {currency.Name} is disabled in settings");
            }
        }

        Main.LogMessage($"Parse complete. Found {Main.CraftableItems.Count} craftable items");
        return true;
    }

    private static bool IsCraftable(Entity item)
    {
        var mods = item.GetComponent<Mods>();
        var base_item = item.GetComponent<Base>();
        if (mods == null || base_item == null) return false;

        // Don't craft unique items - checking through rarity
        if (mods.ItemRarity == ItemRarity.Unique) return false;

        // Don't craft corrupted items
        if (base_item.isCorrupted) return false;

        // Only craft waystones for now
        if (!IsWaystone(item)) return false;

        // Only craft normal, magic, or rare items
        if (mods.ItemRarity != ItemRarity.Normal &&
            mods.ItemRarity != ItemRarity.Magic &&
            mods.ItemRarity != ItemRarity.Rare) return false;

        return true;
    }

    private static bool IsWaystone(Entity item)
    {
        var mapComponent = item.GetComponent<Map>();
        return mapComponent != null;
    }

    private static int? GetWaystoneTier(Entity item)
    {
        var mapComponent = item.GetComponent<Map>();
        if (mapComponent == null) return null;

        return mapComponent.Tier;
    }

    private static int GetModCount(Mods mods)
    {
        if (mods?.ItemMods == null) return 0;

        int prefixCount = 0;
        int suffixCount = 0;

        foreach (var mod in mods.ItemMods)
        {
            // Count prefixes and suffixes
            if (mod.DisplayName.StartsWith("of", StringComparison.OrdinalIgnoreCase))
            {
                suffixCount++;
            }
            else
            {
                if (mod.Group != "AfflictionMapDeliriumStacks")
                {
                    prefixCount++;
                }
            }
        }

        return prefixCount + suffixCount;
    }

    private static Currency DetermineBestCurrency(Entity item)
    {
        var mods = item.GetComponent<Mods>();

        // First need to identify if unidentified
        if (!mods.Identified) return null;

        // Special handling for Waystones
        if (IsWaystone(item))
        {
            var tier = GetWaystoneTier(item);
            if (tier == null) return null;

            Main.LogMessage($"Found waystone tier {tier} with rarity {mods.ItemRarity}");

            // IMPORTANT: Process rarities in logical crafting order
            if (mods.ItemRarity == ItemRarity.Normal)
            {
                // Normal waystones should be transmuted first
                return Main.CurrencyList.FirstOrDefault(c => c.Name == "CurrencyUpgradeToMagic"); // Orb of Transmutation
            }
            else if (mods.ItemRarity == ItemRarity.Magic && tier >= 10)
            {
                // Magic waystones can be regaled
                return Main.CurrencyList.FirstOrDefault(c => c.Name == "CurrencyUpgradeMagicToRare"); // Regal Orb
            }
            else if (mods.ItemRarity == ItemRarity.Rare)
            {
                var modCount = GetModCount(mods);
                if (modCount < 6 && tier >= 12)
                {
                    // Rare waystones with open affixes can be exalted
                    return Main.CurrencyList.FirstOrDefault(c => c.Name == "CurrencyAddModToRare"); // Exalted Orb
                }
            }

            Main.LogMessage($"No suitable currency found for this waystone");
            return null;
        }

        return null;
    }
}