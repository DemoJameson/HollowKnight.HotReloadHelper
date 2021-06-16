using System.Reflection;
using Modding;

namespace HollowKnight.HotReloadHelper {
    public class HotReloadHelper : Mod {
        public override int LoadPriority() => int.MaxValue;
        public override string GetVersion() => Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
        
        public override void Initialize() {
            HotReloadEngine.Init();
        }
    }
}