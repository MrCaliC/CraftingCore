using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared;
using ExileCore2.Shared.Enums;
using CraftingCore.Classes;
using Vector2N = System.Numerics.Vector2;
using static CraftingCore.CraftingCore;

namespace CraftingCore.Compartments;

internal static class ActionsHandler
{
    public static void CleanUp()
    {
        Main.LogMessage("Starting input cleanup");
        try
        {
            Input.KeyUp(Keys.LControlKey);
            Input.KeyUp(Keys.RControlKey);
            Input.KeyUp(Keys.ShiftKey);
            Input.KeyUp(Keys.LShiftKey);
            Input.KeyUp(Keys.RShiftKey);
            Input.KeyUp(Keys.Alt);
            Input.KeyUp(Keys.LButton);
            Input.KeyUp(Keys.RButton);

            Main.LogMessage("Input cleanup completed successfully");
        }
        catch (Exception ex)
        {
            Main.LogMessage($"Error during input cleanup: {ex.Message}");
        }
    }

    public static async SyncTask<bool> PressKey(Keys key, int repetitions = 1)
    {
        for (var i = 0; i < repetitions; i++)
        {
            Input.KeyDown(key);
            await Task.Delay(10);
            Input.KeyUp(key);
            await Task.Delay(10);
        }

        return true;
    }

    public static async SyncTask<bool> ProcessCrafting()
    {
        try
        {
            Main.LogMessage("Starting ProcessCrafting");

            var itemsToCraft = Main.CraftableItems;
            if (itemsToCraft.Count == 0)
            {
                Main.LogMessage("No items to craft found");
                return true;
            }

            // Use the first item to find the currency position
            var currencyPos = await FindCurrency(itemsToCraft[0].Currency.Name);
            if (currencyPos == null)
            {
                Main.LogMessage("Currency not found");
                return true;
            }

            try
            {
                // Hold shift using the new method
                Main.LogMessage("Pressing shift key");
                await PressKey(Keys.ShiftKey, 1);
                await Task.Delay(50);

                // Pick up currency once
                Main.LogMessage("Picking up currency");
                Input.SetCursorPos(currencyPos.Value + Main.ClickWindowOffset);
                await Task.Delay(Main.Settings.ClickDelay);
                Input.Click(MouseButtons.Right);
                await Task.Delay(Main.Settings.ClickDelay);

                // Click each item
                foreach (var craftResult in itemsToCraft)
                {
                    await CraftItem(craftResult, skipCurrencyPickup: true);
                }
            }
            finally
            {
                Input.KeyUp(Keys.ShiftKey);
                Main.LogMessage("Released shift key");
            }

            return true;
        }
        catch (Exception ex)
        {
            Main.LogMessage($"Error in ProcessCrafting: {ex.Message}");
            return false;
        }
    }

    private static async SyncTask<bool> CraftItem(CraftingResult craftResult, bool skipCurrencyPickup = false)
    {
        if (!skipCurrencyPickup)
        {
            var currencyPos = await FindCurrency(craftResult.Currency.Name);
            if (currencyPos == null)
            {
                Main.LogMessage($"Currency not found: {craftResult.Currency.Name}");
                return false;
            }

            // Pick up currency
            Main.LogMessage("Moving to currency position and right-clicking");
            Input.SetCursorPos(currencyPos.Value + Main.ClickWindowOffset);
            await Task.Delay(Main.Settings.ClickDelay);
            Input.Click(MouseButtons.Right);
            await Task.Delay(Main.Settings.ClickDelay);
        }

        // Use currency on item
        Main.LogMessage($"Moving to item position: {craftResult.ItemClickPos}");
        Input.SetCursorPos(craftResult.ItemClickPos + Main.ClickWindowOffset);
        await Task.Delay(Main.Settings.ClickDelay);
        Input.Click(MouseButtons.Left);
        await Task.Delay(Main.Settings.ExtraDelay);

        return true;
    }

    private static async SyncTask<Vector2N?> FindCurrency(string currencyName)
    {
        var stashElement = Main.GameController.Game.IngameState.IngameUi.StashElement;
        var currencyTab = stashElement.VisibleStash;

        if (currencyTab?.VisibleInventoryItems == null)
        {
            Main.LogMessage("Currency tab not accessible");
            return null;
        }

        foreach (var item in currencyTab.VisibleInventoryItems)
        {
            if (item?.Item?.Path == null) continue;

            Main.LogMessage($"Checking stash item: {item.Item.Path}");
            if (item.Item.Path.EndsWith(currencyName))
            {
                var pos = item.GetClientRect().Center;
                Main.LogMessage($"Found {currencyName} at position {pos}");
                return pos;
            }

            // Add a small delay to prevent blocking the main thread
            await Task.Delay(1);
        }

        return null;
    }

}