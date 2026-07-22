# Prototype verdict

Question: Can a code-created window fully participate in Taiwu's native UI
lifecycle without loading a prefab from `RemakeResources/Prefab/Views`?

Verdict: **Yes.** A manually constructed `UIBase` can be paired with a
`UIElement`, assigned a private resource path through the compatibility
adapter, placed with `UIManager.PlaceUI`, and then shown through the complete
native state machine without loading a prefab.

Important finding: `UiFlags` must not be mutated while the element remains in
the visibility handler. Change cover mode through `Hide -> change flags -> Show`.
Calling `UIVisableHandler.ForceUpdateElements()` for this purpose throws and can
leave stale coverage nodes in Taiwu 1.0.58.

Checks:

- [x] F9 host and probe entry open the window through `UIManager.ShowUI`.
- [x] `ViewBottom` becomes covered when `FullCover` is enabled.
- [x] Switching to `IncludeCoverCheck` uncovers `ViewBottom`.
- [ ] Physical ESC key still needs a manual input check; the element is on the
      native UI stack with `CanQuickHide=true`, and the close path uses the same
      `UIManager.HideUI` operation.
- [x] Reopening the cached window runs `OnReset`/`OnInit` again.
- [x] Destroy and recreate removes the old `UIBase` and creates generation 2.
- [x] Root and panel use stretch/center anchors; live resize remains a visual
      check rather than an automated resolution change.
