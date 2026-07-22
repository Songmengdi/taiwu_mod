namespace TaiwuUiFrameworkPrototype;

internal enum PrototypeEvent
{
    Created,
    Init,
    Reset,
    Show,
    ShowFinished,
    HideStarted,
    Hide,
    CoverModeChanged,
}

// Pure, portable state. Unity and terminal/UI rendering deliberately live elsewhere.
internal sealed class PrototypeTrace
{
    internal int Generation { get; }
    internal int InitCount { get; private set; }
    internal int ResetCount { get; private set; }
    internal int ShowCount { get; private set; }
    internal int ShowFinishedCount { get; private set; }
    internal int HideStartedCount { get; private set; }
    internal int HideCount { get; private set; }
    internal int CoverModeChangeCount { get; private set; }
    internal PrototypeEvent LastEvent { get; private set; }

    internal PrototypeTrace(int generation)
    {
        Generation = generation;
        Apply(PrototypeEvent.Created);
    }

    internal void Apply(PrototypeEvent action)
    {
        LastEvent = action;
        switch (action)
        {
            case PrototypeEvent.Init: InitCount++; break;
            case PrototypeEvent.Reset: ResetCount++; break;
            case PrototypeEvent.Show: ShowCount++; break;
            case PrototypeEvent.ShowFinished: ShowFinishedCount++; break;
            case PrototypeEvent.HideStarted: HideStartedCount++; break;
            case PrototypeEvent.Hide: HideCount++; break;
            case PrototypeEvent.CoverModeChanged: CoverModeChangeCount++; break;
        }
    }
}

