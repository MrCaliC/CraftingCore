using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using static CraftingCore.CraftingCore;

namespace CraftingCore.Compartments
{
    public static class WaystoneScoring
    {
        private static List<string> BannedModifiers;

        public static void ParseBannedModifiers()
        {
            BannedModifiers = Main.Settings.ScoreSettings.BannedModifiers.Value
                .Split(',')
                .Select(x => x.Trim().ToLower())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
        }

        public static int CalculateWaystoneScore(Entity item)
        {
            var mods = item.GetComponent<Mods>();
            var mapComponent = item.GetComponent<Map>();

            if (mods == null || mapComponent == null)
                return 0;

            if (mapComponent.Tier < Main.Settings.ScoreSettings.MinimumTier)
                return 0;

            int score = 0;
            int iiq = 0;
            int iir = 0;
            bool extraRareMod = false;
            int packSize = 0;
            int magicPackSize = 0;
            int extraPacks = 0;
            int extraMagicPack = 0;
            int extraRarePack = 0;
            int additionalPacks = 0;

            foreach (var mod in mods.ItemMods)
            {
                if (IsBannedModifier(mod.DisplayName))
                    return 0;

                switch (mod.Name)
                {
                    case "MapDroppedItemRarityIncrease":
                        iir += mod.Values[0];
                        break;
                    case "MapDroppedItemQuantityIncrease":
                        iiq += mod.Values[0];
                        if (mod.Values.Count != 1)
                        {
                            iir += mod.Values[1];
                        }
                        break;
                    case "MapRareMonstersAdditionalModifier":
                        extraRareMod = true;
                        break;
                    case "MapPackSizeIncrease":
                        packSize += mod.Values[0];
                        break;
                    case "MapMagicPackSizeIncrease":
                        magicPackSize += mod.Values[0];
                        break;
                    case "MapTotalEffectivenessIncrease":
                        extraPacks += mod.Values[0];
                        break;
                    case "MapMagicPackIncrease":
                        extraMagicPack += mod.Values[0];
                        break;
                    case "MapMagicRarePackIncrease":
                        extraRarePack += mod.Values[0];
                        if (mod.Values.Count != 1)
                        {
                            extraMagicPack += mod.Values[1];
                        }
                        break;
                    case "MapRarePackIncrease":
                        extraRarePack += mod.Values[0];
                        break;
                    case string s when s.StartsWith("MapMonsterAdditionalPacks"):
                        additionalPacks += mod.Values[0];
                        break;
                }
            }

            var settings = Main.Settings.ScoreSettings;
            score += iiq * settings.ScorePerQuantity;
            score += iir * settings.ScorePerRarity;
            score += packSize * settings.ScorePerPackSize;
            score += magicPackSize * settings.ScorePerMagicPackSize;
            score += extraPacks * settings.ScorePerExtraPacksPercent;
            score += extraMagicPack * settings.ScorePerExtraMagicPack;
            score += extraRarePack * settings.ScorePerExtraRarePack;
            score += additionalPacks * settings.ScorePerAdditionalPack;
            if (extraRareMod)
            {
                score += settings.ScoreForExtraRareMonsterModifier;
            }

            return score;
        }

        private static bool IsBannedModifier(string modName)
        {
            return BannedModifiers.Any(bannedMod => modName.Contains(bannedMod, StringComparison.OrdinalIgnoreCase));
        }
    }
}

