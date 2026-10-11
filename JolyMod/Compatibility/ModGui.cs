using System.Reflection;
using HarmonyLib;
using JolyMod.Config;
using MelonLoader;
using UnityEngine;

namespace JolyMod.Compatibility
{
    using GUI = UnityEngine.GUI;
    using Object = UnityEngine.Object;

    internal static class ModGui
    {
        public static Assembly? Current { get; private set; }
        static readonly Dictionary<Assembly, GUISkin> skins = new();

        public static void Start(HarmonyLib.Harmony harmony)
        {
            if (!Settings.ModCompatibility) return;
            var enter = new HarmonyMethod(typeof(ModGui), nameof(Enter));
            var leave = new HarmonyMethod(typeof(ModGui), nameof(Leave));
            foreach (var melon in MelonBase.RegisteredMelons.Where(melon => melon.MelonAssembly.Assembly != typeof(ModGui).Assembly))
                foreach (var method in Types(melon.MelonAssembly.Assembly).Select(type => type.GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)))
                    if (method != null && !method.IsAbstract) harmony.Patch(method, prefix: enter, finalizer: leave);
        }

        static IEnumerable<Type> Types(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException error) { return error.Types.Where(type => type != null)!; }
        }

        public static bool Hidden { get; private set; }

        public static void OnGUI()
        {
            var key = Event.current;
            if (key?.type == EventType.KeyDown && GUIUtility.keyboardControl == 0 && key.keyCode is KeyCode.F1 or KeyCode.Backslash) Hidden = !Hidden;
        }

        static bool Enter(MethodBase __originalMethod, out Snapshot __state)
        {
            __state = new Snapshot(Current);
            Current = __originalMethod.DeclaringType!.Assembly;
            if (!skins.TryGetValue(Current, out var skin))
            {
                skin = Object.Instantiate(GUI.skin);
                skin.hideFlags = HideFlags.HideAndDontSave;
                skins[Current] = skin;
            }
            GUI.skin = skin;
            return !Hidden;
        }

        static void Leave(Snapshot __state)
        {
            __state.Restore();
            Current = __state.Owner;
        }

        readonly struct Snapshot
        {
            public readonly Assembly? Owner;
            readonly Color color, background, content;
            readonly bool enabled;
            readonly Matrix4x4 matrix;
            readonly GUISkin skin;

            public Snapshot(Assembly? owner)
            {
                Owner = owner;
                (color, background, content) = (GUI.color, GUI.backgroundColor, GUI.contentColor);
                (enabled, matrix, skin) = (GUI.enabled, GUI.matrix, GUI.skin);
            }

            public void Restore()
            {
                (GUI.color, GUI.backgroundColor, GUI.contentColor) = (color, background, content);
                (GUI.enabled, GUI.matrix, GUI.skin) = (enabled, matrix, skin);
            }
        }
    }
}
