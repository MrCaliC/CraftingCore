using System;
using System.Threading.Tasks;
using ExileCore2;
using ExileCore2.Shared;
using static CraftingCore.CraftingCore;

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
            Main.LogMessage("Crafting coroutine is already running");
        }
    }

    public static void StopCoroutine(string routineName)
    {
        TaskRunner.Stop(routineName);
        ActionsHandler.CleanUp();
        Main.LogMessage("Crafting stopped");
    }

    private static async SyncTask<bool> AutoCraftRoutine()
    {
        try
        {
            var cursorPosPreMoving = Input.ForceMousePosition;
            Main.LogMessage("Starting AutoCraftRoutine");

            // Try crafting items 3 times
            var tries = 0;
            do
            {
                try
                {
                    await ItemManager.ParseItems();
                    if (Main.CraftableItems.Count > 0)
                    {
                        Main.LogMessage($"Attempt {tries + 1}: Found {Main.CraftableItems.Count} items to craft");
                        await ActionsHandler.ProcessCrafting();
                        await Task.Delay(Main.Settings.ExtraDelay);
                    }
                    else
                    {
                        Main.LogMessage($"Attempt {tries + 1}: No craftable items found");
                    }
                }
                catch (Exception ex)
                {
                    Main.LogMessage($"Error during crafting attempt {tries + 1}: {ex.Message}");
                    ActionsHandler.CleanUp(); // Ensure cleanup happens on error
                }
                tries++;
            } while (tries < 3 && Main.CraftableItems.Count > 0);

            Input.SetCursorPos(cursorPosPreMoving);
            Input.MouseMove();
            Main.LogMessage("AutoCraftRoutine completed");
            return true;
        }
        catch (Exception ex)
        {
            Main.LogMessage($"Critical error in AutoCraftRoutine: {ex.Message}");
            ActionsHandler.CleanUp();
            return false;
        }
        finally
        {
            StopCoroutine(CoroutineName);
        }
    }
}

