using HarmonyLib;
using Il2CppSG.Airlock;

namespace JolyMod.Compatibility
{
    [HarmonyPatch(typeof(UITransformInputModule), "Process")]
    internal static class AntiClickThroughGUI
    {
        private static bool Prefix() => !ModWindows.MouseOverGUI;
    }
}
