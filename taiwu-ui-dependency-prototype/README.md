# Taiwu UI cross-MOD dependency prototype — DELETE OR ABSORB

Question:

> Can a consumer MOD reference a UI framework assembly that exists only in a
> separate provider MOD, and does Taiwu's dependency ordering load the provider
> early enough for CLR assembly resolution?

This is a throwaway logic prototype. Runtime results live only in static memory
and are exposed through `DependencyHarness.Snapshot` and
`DependencyProbeState.Snapshot` for Taiwu MCP inspection.

One-command build and installation:

```powershell
.\taiwu-ui-dependency-prototype\run-prototype.ps1
```

The provider uses local `FileId=990058001`. The consumer declares that ID in
`Dependencies` and deliberately does **not** contain the provider DLL in its own
`Plugins` directory.

