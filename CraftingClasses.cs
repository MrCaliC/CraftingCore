using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using Vector2N = System.Numerics.Vector2;
using ExileCore2.Shared.Nodes;

namespace CraftingCore.Classes;

public class Currency
{
    public string Name;
    public Vector2N ClickPos;

    public Currency(string name)
    {
        Name = name;
    }
}

public class CraftingResult
{
    public Currency Currency { get; set; }
    public Vector2N ItemClickPos { get; set; }
    public Entity Item { get; set; }
    public bool IsIdentified => Item?.GetComponent<Mods>()?.Identified ?? false;
}

public class ItemPosition
{
    public int X { get; set; }
    public int Y { get; set; }
    public Vector2N ClickPos { get; set; }
    public Entity Item { get; set; }
}