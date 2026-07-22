# Taiwu UI Framework Prototype — DELETE OR ABSORB

This throwaway prototype answers one question:

> Can a window created entirely from C# participate in Taiwu's native
> `UIManager -> UIElement -> UIBase` lifecycle, including ESC handling,
> full-cover notifications, repeated show/hide, and destruction/recreation?

The prototype keeps all state in memory. Press **F9** in the world-map UI to
open or close it. The window renders its current lifecycle state and counters.

Build and install with one command:

```powershell
.\taiwu-ui-framework-prototype\run-prototype.ps1
```

Enable **Taiwu UI Framework Prototype** in the game's MOD manager and restart
the game. Delete this directory or absorb the validated approach into the real
framework when the question has been answered.

