using System.Reflection;
using HarmonyLib;
using JolyMod.Config;
using UnityEngine;

namespace JolyMod.Compatibility
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
            var mouse = Event.current.mousePosition;
            MouseOverGUI = Settings.ModCompatibility && drawn.Any(area => area.Contains(mouse));
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

        static bool Prefix(MethodBase __originalMethod, object[] __args, ref Rect __result)
        {
            var mod = ModGui.Current;
            if (calling || !Settings.ModCompatibility || mod == null) return true;
            var args = (object[])__args.Clone();
            args[0] = HashCode.Combine(mod.FullName, (int)__args[0]);
            calling = true;
            try { __result = (Rect)__originalMethod.Invoke(null, args)!; }
            finally { calling = false; }
            Track(__result);
            return false;
        }

        public static void Track(Rect area) => drawn.Add(GUIUtility.GUIToScreenRect(area));
    }

    [HarmonyPatch(typeof(GUI), nameof(GUI.Box), typeof(Rect), typeof(string))]
    internal static class ModBoxes
    {
        static void Postfix(Rect position)
        {
            if (Settings.ModCompatibility && ModGui.Current != null) ModWindows.Track(position);
        }
    }
}
