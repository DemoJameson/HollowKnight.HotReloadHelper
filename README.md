Ported from the [BepInEx.Debug.ScriptEngine](https://github.com/BepInEx/BepInEx.Debug#scriptengine) to provide hot reload feature for Hollow Knight's mod development.

### Hot Reload Helper
Loads and reloads hollow knight mods from the `Mods\HotReload` folder. User can reload all of these mods by pressing `Ctrl+F5`.
Very useful for quickly developing mods as you don't have to keep reopening the game to see your changes.

Remember to clean up after the old mod version in case you need to. Things like hooks or loose GameObjects/MonoBehaviours remain after the mod gets destroyed. Loose gameobjects and monobehaviours in this case are objects that are not attached to the parent scriptengine gameobject. For example:

```cs
    // Only mods that inherit from the Mod class and implement the ITogglableMod interface support HotReload
    public class SimpleHooks : Mod, ITogglableMod
    {
        public static SimpleHooks LoadedInstance { get; private set; }
        private GameObject permanentGo;
        public override void Initialize()
        {
            if (SimpleHooks.LoadedInstance != null) return;
            SimpleHooks.LoadedInstance = this;
            permanentGo = new GameObject();
            Object.DontDestroyOnLoad(permanentGo);
            ModHooks.AttackHook += LogAttack;
        }

        // Code that should be run when the mod is unload.
        public void Unload()
        {
            // Unhook the methods previously registered so no exceptions will happen.
            ModHooks.AttackHook -= LogAttack;
            // Destroy the loaded instance of the mod.
            SimpleHooks.LoadedInstance = null;
            // Destroy the permanent objects;
            Object.Destroy(permanentGo);
        }

        private void LogAttack(AttackDirection _)
        {
            Log($"You have hit an enemy {this.HitCount} times!");
        }
    }
```

**How to use:** This is a mod. Put the `HotReloadHelper.dll` file into `Mods` folder.
Put the mods that need to be hot reload into `Mods\HotReload` folder, each time the `your_mod.dll` file is updated or `Ctrl+F5` is pressed, the mod will be reloaded.