using FrameWork;
using GameData.Domains.Map;
using UnityEngine;

namespace MapSkillFinder.Frontend;

/// <summary>
/// Tracks the map blocks marked by this mod so marks can be removed one by
/// one (without wiping marks owned by the native find-block view) and so a
/// mark disappears automatically once its block is explored or visited.
/// </summary>
internal static class MapMarkTracker
{
    private static readonly HashSet<Location> MarkedLocations = new();

    internal static bool HasMarks => MarkedLocations.Count > 0;

    /// <summary>Area of the current marks; null when nothing is marked. All marks
    /// live in one area because ReplaceMarks wipes the previous set.</summary>
    internal static short? MarkedAreaId =>
        MarkedLocations.Count == 0 ? null : MarkedLocations.First().AreaId;

    /// <summary>Identifies the finder entry (holder set / person / merchant …)
    /// whose mark is currently on the map, so the UI can show it as 已标记 after
    /// the window is reopened. Null when nothing is marked.</summary>
    internal static string? MarkedKey { get; private set; }

    internal static void ReplaceMarks(WorldMapModel map, List<Location> locations, string? key)
    {
        foreach (Location location in MarkedLocations)
            RemoveFromMap(map, location);
        MarkedLocations.Clear();
        MarkedKey = null;
        map.AddLocationsToTemporaryMarkList(locations);
        foreach (Location location in locations)
            MarkedLocations.Add(location);
        if (MarkedLocations.Count > 0)
            MarkedKey = key;
    }

    /// <summary>Removes the mark once its block becomes explored (visible).</summary>
    internal static void RemoveIfExplored(WorldMapModel map, MapBlockData block)
    {
        if (block.Visible)
            Remove(map, block.GetLocation());
    }

    /// <summary>Removes the mark once Taiwu steps onto its block.  A block that
    /// was already explored never fires a visibility change, so arrival is the
    /// only signal left for it.</summary>
    internal static void RemoveIfArrived(WorldMapModel map)
    {
        Remove(map, map.CurrentLocation);
    }

    private static void Remove(WorldMapModel map, Location location)
    {
        if (MarkedLocations.Remove(location))
        {
            RemoveFromMap(map, location);
            if (MarkedLocations.Count == 0)
                MarkedKey = null;
        }
    }

    private static void RemoveFromMap(WorldMapModel map, Location location)
    {
        if (map.FindMapBlockMarkLocationList.Remove(location))
        {
            ArgumentBox args = EasyPool.Get<ArgumentBox>().Set("location", location);
            GEvent.OnEvent(UiEvents.MapClearLocationTemporaryMark, args);
        }
    }
}

/// <summary>
/// Removes this mod's map marks once their block is explored or visited.
/// Lives on the bottom bar next to the finder window so it stays active
/// while the window is closed.
/// </summary>
internal sealed class ExploredMarkCleaner : MonoBehaviour
{
    internal static ExploredMarkCleaner Attach(GameObject host)
    {
        ExploredMarkCleaner? cleaner = host.GetComponent<ExploredMarkCleaner>();
        if (cleaner == null)
            cleaner = host.AddComponent<ExploredMarkCleaner>();
        return cleaner;
    }

    private void OnEnable()
    {
        GEvent.Add(UiEvents.WorldMapBlockDataChange, OnBlockDataChange);
        GEvent.Add(UiEvents.WorldMapPlayerBlockChange, OnPlayerBlockChange);
    }

    private void OnDisable()
    {
        GEvent.Remove(UiEvents.WorldMapBlockDataChange, OnBlockDataChange);
        GEvent.Remove(UiEvents.WorldMapPlayerBlockChange, OnPlayerBlockChange);
    }

    private static void OnBlockDataChange(ArgumentBox argsBox)
    {
        if (!MapMarkTracker.HasMarks)
            return;
        argsBox.Get("Data", out MapBlockData block);
        if (block != null)
            MapMarkTracker.RemoveIfExplored(SingletonObject.getInstance<WorldMapModel>(), block);
    }

    private static void OnPlayerBlockChange(ArgumentBox _)
    {
        if (MapMarkTracker.HasMarks)
            MapMarkTracker.RemoveIfArrived(SingletonObject.getInstance<WorldMapModel>());
    }
}
