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
        try 
        {
            var serverData = Main.GameController.Game.IngameState.Data.ServerData;
            if (serverData == null)
            {
                if (Main.Settings.DebugEnabled) Main.LogMessage("Server data is null");
                return false;
            }

            var inventoryItems = serverData.PlayerInventories;
            if (inventoryItems == null || inventoryItems.Count == 0)
            {
                if (Main.Settings.DebugEnabled) Main.LogMessage("Player inventories is null or empty");
                return false;
            }

            var inventory = inventoryItems[0]?.Inventory;
            if (inventory == null)
            {
                if (Main.Settings.DebugEnabled) Main.LogMessage("Inventory is null");
                return false;
            }

            var items = inventory.InventorySlotItems;
            if (items == null)
            {
                if (Main.Settings.DebugEnabled) Main.LogMessage("Inventory items are null");
                return false;
            }

            if (Main.Settings.DebugEnabled) Main.LogMessage($"Found {items.Count} total inventory items");

            await TaskUtils.CheckEveryFrameWithThrow(() => items != null, new CancellationTokenSource(500).Token);
            Main.CraftableItems = new List<CraftingResult>();
            Main.ClickWindowOffset = Main.GameController.Window.GetWindowRectangle().TopLeft;

            foreach (var invItem in items)
            {
                if (invItem?.Item == null || invItem.Address == 0) continue;

                var item = invItem.Item;
                var mods = item.GetComponent<Mods>();
                var base_item = item.GetComponent<Base>();

                if (!IsCraftable(item))
                {
                    if (Main.Settings.DebugEnabled) Main.LogMessage($"Skipping non-craftable item: {item.Path}");
                    continue;
                }

                var currency = DetermineBestCurrency(item);
                if (currency == null)
                {
                    if (Main.Settings.DebugEnabled) Main.LogMessage($"No suitable currency found for item: {item.Path}");
                    continue;
                }

                // Debug the settings state
                if (Main.Settings.DebugEnabled) Main.LogMessage($"Checking if currency {currency.Name} is enabled in settings");
                var isEnabled = false;
                if (Main.Settings.CurrencyEnabled.TryGetValue(currency.Name, out var enabled))
                {
                    isEnabled = enabled.Value;
                    if (Main.Settings.DebugEnabled) Main.LogMessage($"Currency {currency.Name} enabled state: {isEnabled}");
                }
                else
                {
                    if (Main.Settings.DebugEnabled) Main.LogMessage($"Currency {currency.Name} not found in settings");
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
                    if (Main.Settings.DebugEnabled) Main.LogMessage($"Added craftable item using {currency.Name}");
                }
                else
                {
                    if (Main.Settings.DebugEnabled) Main.LogMessage($"Skipping item because {currency.Name} is disabled in settings");
                }
            }

            if (Main.Settings.DebugEnabled) Main.LogMessage($"Parse complete. Found {Main.CraftableItems.Count} craftable items");
            return true;
        }
        catch (Exception ex)
        {
            if (Main.Settings.DebugEnabled) Main.LogMessage($"Error in ParseItems: {ex.Message}");
            return false;
        }
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

        // Check if the item is a Waystone or a Tablet
        bool isWaystone = IsWaystone(item);
        bool isTablet = IsTablet(item);

        if (!isWaystone && !isTablet) return false;

        // If Alchemy Craft Only is enabled, only craft normal or rare items for Waystones
        // For Tablets, only craft normal or magic items
        if (Main.Settings.AlchemyCraftOnly)
        {
            if (isWaystone && mods.ItemRarity == ItemRarity.Magic) return false;
        }

        // Only craft normal or magic items for Tablets
        if (isTablet && mods.ItemRarity != ItemRarity.Normal && mods.ItemRarity != ItemRarity.Magic) return false;

        // For Waystones, only craft normal, magic, or rare items
        if (isWaystone && mods.ItemRarity != ItemRarity.Normal &&
            mods.ItemRarity != ItemRarity.Magic &&
            mods.ItemRarity != ItemRarity.Rare) return false;

        return true;
    }

    private static bool IsWaystone(Entity item)
    {
        var mapComponent = item.GetComponent<Map>();
        return mapComponent != null;
    }

    private static bool IsTablet(Entity item)
    {
        var baseComponent = item.GetComponent<Base>();
        if (baseComponent == null) return false;

        return Main.GameController.Files.BaseItemTypes.Contents.TryGetValue(item.Metadata, out var baseItemType) 
               && baseItemType.ClassName.Contains("TowerAugment", StringComparison.OrdinalIgnoreCase);
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

        bool isWaystone = IsWaystone(item);
        bool isTablet = IsTablet(item);

        if (isWaystone)
        {
            return DetermineBestCurrencyForWaystone(item, mods);
        }
        else if (isTablet)
        {
            return DetermineBestCurrencyForTablet(item, mods);
        }

        return null;
    }

    private static Currency DetermineBestCurrencyForWaystone(Entity item, Mods mods)
    {
        var tier = GetWaystoneTier(item);
        if (tier == null) return null;

        if (Main.Settings.DebugEnabled) Main.LogMessage($"Found waystone tier {tier} with rarity {mods.ItemRarity}");

        var modCount = GetModCount(mods);
        if (Main.Settings.DebugEnabled) Main.LogMessage($"Waystone mod count: {modCount}");

        if (Main.Settings.AlchemyCraftOnly)
        {
            if (mods.ItemRarity == ItemRarity.Normal)
            {
                return Main.CurrencyList.FirstOrDefault(c => c.Name == "CurrencyUpgradeToRare"); // Alchemy Orb
            }
            else if (mods.ItemRarity == ItemRarity.Rare)
            {
                if (modCount < 6 && Main.Settings.CurrencyEnabled.TryGetValue("CurrencyAddModToRare", out var exaltEnabled) && exaltEnabled.Value)
                {
                    return Main.CurrencyList.FirstOrDefault(c => c.Name == "CurrencyAddModToRare"); // Exalted Orb
                }
                else if (modCount == 6 && Main.Settings.CurrencyEnabled.TryGetValue("CurrencyCorrupt", out var vaalEnabled) && vaalEnabled.Value)
                {
                    return Main.CurrencyList.FirstOrDefault(c => c.Name == "CurrencyCorrupt"); // Vaal Orb
                }
            }
        }
        else
        {
            // Existing logic for non-Alchemy Craft Only mode
            if (mods.ItemRarity == ItemRarity.Normal)
            {
                return Main.CurrencyList.FirstOrDefault(c => c.Name == "CurrencyUpgradeToMagic"); // Orb of Transmutation
            }
            else if (mods.ItemRarity == ItemRarity.Magic)
            {
                if (modCount < 2)
                {
                    return Main.CurrencyList.FirstOrDefault(c => c.Name == "CurrencyAddModToMagic"); // Orb of Augmentation
                }
                else if (tier >= 10)
                {
                    return Main.CurrencyList.FirstOrDefault(c => c.Name == "CurrencyUpgradeMagicToRare"); // Regal Orb
                }
            }
            else if (mods.ItemRarity == ItemRarity.Rare)
            {
                if (modCount < 6 && tier >= 12)
                {
                    return Main.CurrencyList.FirstOrDefault(c => c.Name == "CurrencyAddModToRare"); // Exalted Orb
                }
                else if (modCount == 6 && Main.Settings.CurrencyEnabled.TryGetValue("CurrencyCorrupt", out var vaalEnabled) && vaalEnabled.Value)
                {
                    return Main.CurrencyList.FirstOrDefault(c => c.Name == "CurrencyCorrupt"); // Vaal Orb
                }
            }
        }

        if (Main.Settings.DebugEnabled) Main.LogMessage($"No suitable currency found for this waystone");
        return null;
    }



    private static int GetTabletModCount(Mods mods)
    {
        if (mods?.ItemMods == null) return 0;

        // For Tablets, we count actual mods without filtering prefixes/suffixes
        int modCount = 0;
        foreach (var mod in mods.ItemMods)
        {
            // Skip hidden or special mods
            if (string.IsNullOrEmpty(mod.DisplayName)) continue;
            if (mod.Group == "AfflictionMapDeliriumStacks") continue;

            modCount++;
            if (Main.Settings.DebugEnabled) Main.LogMessage($"Found tablet mod: {mod.DisplayName}, Group: {mod.Group}");
        }

        return modCount;
    }

    private static Currency DetermineBestCurrencyForTablet(Entity item, Mods mods)
    {
        if (Main.Settings.DebugEnabled) Main.LogMessage($"Found tablet with rarity {mods.ItemRarity}");

        var modCount = GetTabletModCount(mods);
        if (Main.Settings.DebugEnabled) Main.LogMessage($"The mod count for this tablet is: {modCount}");

        if (mods.ItemRarity == ItemRarity.Normal)
        {
            return Main.CurrencyList.FirstOrDefault(c => c.Name == "CurrencyUpgradeToMagic"); // Orb of Transmutation
        }
        else if (mods.ItemRarity == ItemRarity.Magic)
        {
            if (modCount < 2)
            {
                if (Main.Settings.DebugEnabled) Main.LogMessage("Tablet has less than 2 mods, can apply Augmentation");
                return Main.CurrencyList.FirstOrDefault(c => c.Name == "CurrencyAddModToMagic"); // Orb of Augmentation
            }
            else
            {
                if (Main.Settings.DebugEnabled) Main.LogMessage("Tablet already has 2 mods, cannot apply more currency");
            }
        }

        if (Main.Settings.DebugEnabled) Main.LogMessage($"No suitable currency found for this tablet");
        return null;
    }
}

