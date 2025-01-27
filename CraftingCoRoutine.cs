using System;
using System.Threading.Tasks;
using ExileCore2;
using ExileCore2.Shared;
using static CraftingCore.CraftingCore;
using System.Linq;

namespace CraftingCore.Compartments;

public static class CraftingCoRoutine
{
    public static void InitCraftingCoRoutine()
    {
        if (!TaskRunner.Has(CoroutineName))
        {
            TaskRunner.Run(AutoCraftRoutine, CoroutineName);
        }
    }

    public static void StartCraftingCoroutine()
    {
        if (!TaskRunner.Has(CoroutineName))
        {
            TaskRunner.Run(AutoCraftRoutine, CoroutineName);
        }
        else
        {
            if (Main.Settings.DebugEnabled) Main.LogMessage("Crafting coroutine is already running");
        }
    }

    public static void StopCoroutine(string routineName)
    {
        TaskRunner.Stop(routineName);
        ActionsHandler.CleanUp();
        if (Main.Settings.DebugEnabled) Main.LogMessage("Crafting stopped");
    }

    private static async SyncTask<bool> AutoCraftRoutine()
    {
        try
        {
            var cursorPosPreMoving = Input.ForceMousePosition;
            if (Main.Settings.DebugEnabled) Main.LogMessage("Starting AutoCraftRoutine");

            bool itemsNeedCrafting;
            do
            {
                itemsNeedCrafting = await CraftingCycle();

                // Check if there are any items left that need Vaal Orbs
                if (!itemsNeedCrafting)
                {
                    await ItemManager.ParseItems();
                    itemsNeedCrafting = Main.CraftableItems.Any(item => item.Currency.Name == "CurrencyCorrupt");
                }
            } while (itemsNeedCrafting);

            Input.SetCursorPos(cursorPosPreMoving);
            Input.MouseMove();
            if (Main.Settings.DebugEnabled) Main.LogMessage("AutoCraftRoutine completed");
            return true;
        }
        catch (Exception ex)
        {
            if (Main.Settings.DebugEnabled) Main.LogMessage($"Critical error in AutoCraftRoutine: {ex.Message}");
            ActionsHandler.CleanUp();
            return false;
        }
        finally
        {
            StopCoroutine(CoroutineName);
        }
    }

    private static async SyncTask<bool> CraftingCycle()
    {
        await ItemManager.ParseItems();
        if (Main.CraftableItems.Count == 0)
        {
            if (Main.Settings.DebugEnabled) Main.LogMessage("No items need crafting");
            return false;
        }

        if (Main.Settings.DebugEnabled) Main.LogMessage($"Found {Main.CraftableItems.Count} items to craft");
        await ActionsHandler.ProcessCrafting();
        await Task.Delay(Main.Settings.ExtraDelay);

        return true;
    }
}

