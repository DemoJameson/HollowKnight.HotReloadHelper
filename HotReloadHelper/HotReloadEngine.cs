using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Modding;
using Mono.Cecil;
using UnityEngine;

namespace HollowKnight.HotReloadHelper {
    public class HotReloadEngine : MonoBehaviour {
        private static readonly Type ModLoaderType = typeof(IMod).Assembly.GetType("Modding.ModLoader");
        private static readonly MethodInfo ModLoaderLoadMod = ModLoaderType.GetMethod("LoadMod", BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly MethodInfo ModLoaderUnloadModMethod =
            ModLoaderType.GetMethod("UnloadMod", BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly FieldInfo ModLoaderLoadedModsFieldInfo =
            ModLoaderType.GetField("LoadedMods", BindingFlags.Static | BindingFlags.Public);

        private static List<IMod> ModLoaderLoadedMods => (List<IMod>) ModLoaderLoadedModsFieldInfo.GetValue(null);

        private readonly Dictionary<string, List<ITogglableMod>> hotReloadedMods = new();
        private FileSystemWatcher watcher;

        private static string ManagerPath {
            get {
                string path = Application.dataPath;
                if (SystemInfo.operatingSystem.Contains("Mac")) {
                    path = Path.Combine(path, "Resources", "Data");
                }

                return Path.Combine(path, "Managed");
            }
        }

        private static string ModsPath => Path.Combine(ManagerPath, "Mods");

        private static string HotReloadDirectory => Path.Combine(ModsPath, "HotReload");

        public static void Init() {
            HotReloadEngine hotReloadEngine = new GameObject().AddComponent<HotReloadEngine>();
            DontDestroyOnLoad(hotReloadEngine);
        }

        private void Awake() {
            StartCoroutine(DelayAction(() => {
                ReloadAllDlls();
                WatchDlls();
            }));
        }

        private void WatchDlls() {
            if (Directory.Exists(HotReloadDirectory)) {
                Directory.CreateDirectory(HotReloadDirectory);
            }

            try {
                watcher?.Dispose();

                watcher = new FileSystemWatcher {
                    Path = HotReloadDirectory,
                    Filter = "*.dll",
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                };

                watcher.Changed += (_, e) => { ReloadDll(Path.GetFullPath(e.FullPath)); };

                watcher.EnableRaisingEvents = true;
            } catch (Exception) {
                Modding.Logger.LogError($"Failed watching folder: {HotReloadDirectory}");
                watcher?.Dispose();
            }
        }

        private void Update() {
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.F5)) {
                ReloadAllDlls();
                // TODO Restart the game and then restore the scene.
            }
        }

        private void OnDestroy() {
            UnloadAllDlls();
            watcher?.Dispose();
        }

        private void ReloadAllDlls() {
            UnloadAllDlls();

            var files = Directory.GetFiles(HotReloadDirectory, "*.dll");
            if (files.Length > 0) {
                foreach (string path in Directory.GetFiles(HotReloadDirectory, "*.dll")) {
                    LoadDLL(Path.GetFullPath(path));
                }

                Modding.Logger.Log("Reloaded all mods!");
            } else {
                Modding.Logger.Log("No mod to reload");
            }
        }

        private void UnloadAllDlls() {
            foreach (string path in hotReloadedMods.Keys.ToList()) {
                UnloadDll(path);
            }
            hotReloadedMods.Clear();
            Modding.Logger.Log("Unloaded all old mod instances");
        }

        private void LoadDLL(string path) {
            UnloadDll(path);

            var defaultResolver = new DefaultAssemblyResolver();
            defaultResolver.AddSearchDirectory(HotReloadDirectory);
            defaultResolver.AddSearchDirectory(ManagerPath);
            defaultResolver.AddSearchDirectory(ModsPath);

            Modding.Logger.Log($"Loading mod from {path}");

            using (var dll = AssemblyDefinition.ReadAssembly(path, new ReaderParameters {AssemblyResolver = defaultResolver})) {
                dll.Name.Name = $"{dll.Name.Name}-{DateTime.Now.Ticks}";

                using (var ms = new MemoryStream()) {
                    dll.Write(ms);
                    var ass = Assembly.Load(ms.ToArray());

                    foreach (Type type in GetTypesSafe(ass)) {
                        try {
                            if (type.IsClass && type.IsSubclassOf(typeof(Mod)) && !type.IsAbstract && typeof(ITogglableMod).IsAssignableFrom(type)) {
                                Modding.Logger.Log($"Loading {type.FullName}");
                                StartCoroutine(DelayAction(() => {
                                    try {
                                        LoadMod(path, type);
                                    } catch (Exception e) {
                                        Modding.Logger.LogError($"Failed to load mod {type.FullName} because of exception: {e}");
                                    }
                                }));
                            }
                        } catch (Exception e) {
                            Modding.Logger.LogError($"Failed to load mod {type.FullName} because of exception: {e}");
                        }
                    }
                }
            }
        }

        private void ReloadDll(string path) {
            if (File.Exists(path)) {
                Modding.Logger.Log($"Reloading mod from {path}");
                LoadDLL(path);
            } else {
                UnloadDll(path);
            }
        }

        private void LoadMod(string path, Type type) {
            object mod = type.GetConstructor(new Type[0])?.Invoke(new object[0]);
            if (mod != null) {
                Modding.Logger.LogDebug($"Loaded mod {type.FullName} successfully");
                if (!hotReloadedMods.ContainsKey(path)) {
                    hotReloadedMods[path] = new List<ITogglableMod>();
                }

                hotReloadedMods[path].Add(mod as ITogglableMod);
                ModLoaderLoadedMods.Add(mod as IMod);
                ModLoaderLoadMod.Invoke(null, new object[] {mod, true, true, null});
            }
        }

        private void UnloadDll(string path) {
            if (hotReloadedMods.ContainsKey(path)) {
                Modding.Logger.Log($"Unloaded old mod instance from {path}");
                foreach (ITogglableMod togglableMod in hotReloadedMods[path]) {
                    UnloadMod(togglableMod);
                }

                hotReloadedMods.Remove(path);
            }
        }

        private void UnloadMod(ITogglableMod togglableMod) {
            ModLoaderLoadedMods.Remove(togglableMod);
            ModLoaderUnloadModMethod.Invoke(null, new object[] {togglableMod});
        }

        private static IEnumerable<Type> GetTypesSafe(Assembly ass) {
            try {
                return ass.GetTypes();
            } catch (ReflectionTypeLoadException ex) {
                return ex.Types.Where(x => x != null);
            }
        }

        private static IEnumerator DelayAction(Action action) {
            yield return null;
            action();
        }
    }
}