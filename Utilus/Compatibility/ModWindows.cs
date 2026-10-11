using System.Reflection;
using HarmonyLib;
using Utilus.Config;
using MelonLoader;
using UnityEngine;

namespace Utilus.Compatibility
{
    using GUI = UnityEngine.GUI;
    [HarmonyPatch]
    internal static class ModWindows
    {
        public static bool MouseOverGUI { get; private set; }
        static readonly List<Rect> drawn = new();

        public static void OnGUI()
        {
            if (Event.current?.type != EventType.Repaint) return;
            MouseOverGUI = Settings.ModCompatibility && drawn.Any(area => area.Contains(Event.current.mousePosition));
            drawn.Clear();
        }

        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var method in typeof(GUI).GetMethods().Concat(typeof(GUILayout).GetMethods()))
            {
                var info = method.GetParameters();
                if (method.Name == "Window" && info.Length > 1 && info[0].ParameterType == typeof(int) && info[1].ParameterType == typeof(Rect)) yield return method;
            }
        }

        static bool calling;

        private static bool Prefix(MethodBase __originalMethod, object[] __args, ref Rect __result)
        {
            if (calling || !Settings.ModCompatibility || ModGui.Current == null) return true;
            var key = (ModGui.Current, (int)__args[0]);
            var args = (object[])__args.Clone();
            args[0] = HashCode.Combine(ModGui.Current.FullName, (int)__args[0]);
            bool reset = false;
            if (OffScreen((Rect)args[1]) && placed.Remove(key, out var last))
            {
                reset = true;
                args[1] = last;
                MelonLogger.Msg($"Reset {ModGui.Current.GetName().Name} gui for going off screen");
            }
            calling = true;
            try { __result = (Rect)__originalMethod.Invoke(null, args)!; }
            finally { calling = false; }
            if (!reset && !OffScreen(__result)) placed[key] = __result;
            Track(__result);
            return false;
        }

        static readonly Dictionary<(Assembly, int), Rect> placed = new();

        private static bool OffScreen(Rect area)
        {
            var onScreen = GUIUtility.GUIToScreenRect(area);
            return float.IsNaN(onScreen.x) || float.IsNaN(onScreen.y) || !onScreen.Overlaps(new Rect(0, 0, Screen.width, Screen.height));
        }

        public static void Track(Rect area) => drawn.Add(GUIUtility.GUIToScreenRect(area));
    }

    [HarmonyPatch(typeof(GUI), nameof(GUI.Box), typeof(Rect), typeof(string))]
    internal static class ModBoxes
    {
        private static void Postfix(Rect position)
        {
            if (Settings.ModCompatibility && ModGui.Current != null) ModWindows.Track(position);
        }
    }
}
