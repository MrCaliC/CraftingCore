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
        if (Main.Settings.DebugEnabled) Main.LogMessage("Starting input cleanup");
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

            if (Main.Settings.DebugEnabled) Main.LogMessage("Input cleanup completed successfully");
        }
        catch (Exception ex)
        {
            if (Main.Settings.DebugEnabled) Main.LogMessage($"Error during input cleanup: {ex.Message}");
        }
    }

    public static async SyncTask<bool> ProcessCrafting()
    {
        try
        {
            if (Main.Settings.DebugEnabled) Main.LogMessage("Starting ProcessCrafting");

            var itemsToCraft = Main.CraftableItems;
            if (itemsToCraft.Count == 0)
            {
                if (Main.Settings.DebugEnabled) Main.LogMessage("No items to craft found");
                return true;
            }

            try
            {
                // Hold shift
                if (Main.Settings.DebugEnabled) Main.LogMessage("Holding shift key");
                Input.KeyDown(Keys.ShiftKey);
                await Task.Delay(50);

                foreach (var craftResult in itemsToCraft)
                {
                    await CraftItem(craftResult);
                }
            }
            finally
            {
                Input.KeyUp(Keys.ShiftKey);
                if (Main.Settings.DebugEnabled) Main.LogMessage("Released shift key");
            }

            return true;
        }
        catch (Exception ex)
        {
            if (Main.Settings.DebugEnabled) Main.LogMessage($"Error in ProcessCrafting: {ex.Message}");
            return false;
        }
    }

    private static async SyncTask<bool> CraftItem(CraftingResult craftResult)
    {
        var currencyPos = await FindCurrency(craftResult.Currency.Name);
        if (currencyPos == null)
        {
            if (Main.Settings.DebugEnabled) Main.LogMessage($"Currency not found: {craftResult.Currency.Name}");
            return false;
        }

        // Right-click on currency
        if (Main.Settings.DebugEnabled) Main.LogMessage($"Right-clicking currency: {craftResult.Currency.Name}");
        Input.SetCursorPos(currencyPos.Value + Main.ClickWindowOffset);
        await Task.Delay(Main.Settings.ClickDelay);
        Input.Click(MouseButtons.Right);
        await Task.Delay(Main.Settings.ClickDelay);

        // Left-click on item
        if (Main.Settings.DebugEnabled) Main.LogMessage($"Left-clicking item at position: {craftResult.ItemClickPos}");
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
            if (Main.Settings.DebugEnabled) Main.LogMessage("Currency tab not accessible");
            return null;
        }

        foreach (var item in currencyTab.VisibleInventoryItems)
        {
            if (item?.Item?.Path == null) continue;

            if (Main.Settings.DebugEnabled) Main.LogMessage($"Checking stash item: {item.Item.Path}");
            if (item.Item.Path.EndsWith(currencyName))
            {
                var pos = item.GetClientRect().Center;
                if (Main.Settings.DebugEnabled) Main.LogMessage($"Found {currencyName} at position {pos}");
                return pos;
            }

            await Task.Delay(1);
        }

        return null;
    }
}

