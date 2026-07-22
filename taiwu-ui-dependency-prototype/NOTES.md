# Prototype verdict

Question: Can a consumer resolve a framework assembly supplied by another MOD?

Verdict: **Yes, but only with strict provider-first loading.** The consumer must
declare the framework MOD as a dependency; copying the framework DLL into each
consumer is neither necessary nor desirable.

Expected model from decompilation of Taiwu 1.0.58:

1. `ModManager.LoadAllEnabledMods` sorts enabled MODs and recursively enqueues
   declared `Dependencies` before consumers.
2. `PluginHelper.LoadPlugin` loads each plugin with `Assembly.Load(byte[])`.
3. Referenced DLLs are searched only in that MOD's own `Plugins` directory.
4. A cross-MOD reference therefore works only when an assembly with the exact
   identity was already loaded by the provider MOD.

Runtime result using the real `PluginHelper.LoadPlugin`:

- Consumer before provider: failed with `TypeLoadException` for
  `TaiwuUiDependencyPrototype.Provider.ProviderReply`.
- Provider load: succeeded and its plugin initialized.
- Consumer after provider: succeeded.
- `ProviderInitializedFirst=True`.
- `ProviderLoadedCopies=1`.
- Provider `Assembly.Location` was empty because Taiwu loads DLL bytes with
  `Assembly.Load(byte[])`.
- The consumer output and installed `Plugins` directory contain only the
  consumer DLL; no provider DLL was copied.

Production implications:

1. Publish the framework as a separate MOD with a stable workshop `FileId`.
2. Every consumer declares that ID in `Dependencies`.
3. Consumer builds reference the framework with `Private=false`.
4. Framework assembly name and public API major version must be stable.
5. Diagnostics must report dependency/version mismatch without assuming
   `Assembly.Location` points to a real file.
