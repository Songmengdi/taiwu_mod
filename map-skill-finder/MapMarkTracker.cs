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

    internal static void ReplaceMarks(WorldMapModel map, List<Location> locations)
    {
        foreach (Location location in MarkedLocations)
            RemoveFromMap(map, location);
        MarkedLocations.Clear();
        map.AddLocationsToTemporaryMarkList(locations);
        foreach (Location location in locations)
            MarkedLocations.Add(location);
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
            RemoveFromMap(map, location);
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
