using HarmonyLib;
using Il2CppSG.Airlock;
using Il2CppSG.Airlock.UI.TitleScreen;
using Utilus.Config;
using Utilus.GUI;
using MelonLoader;
using UnityEngine;

namespace Utilus.ModdedLobbies
{
    [HarmonyPatch(typeof(GamemodeSelectionMenu))]
    internal static class ModdedModes
    {
        static GameToggle? toggle;

        [HarmonyPostfix, HarmonyPatch(nameof(GamemodeSelectionMenu.Update))]
        private static void Update(GamemodeSelectionMenu __instance)
        {
            if (!Settings.ModdedLobbies) return;
            if (toggle == null || toggle.Button.Root == null) AddToggle(__instance);
            var modes = __instance.GetComponentsInChildren<MapModeSelect>().Where(select => !string.IsNullOrEmpty(select.ModeInfo?.ModeName)).ToList();
            foreach (var mode in modes)
                foreach (var label in GameText.In(mode.transform)) Mark(label);
            var title = __instance.transform.Find("MainLayout/Center/Layout/GameModeDescription/GameModeTitle");
            if (title != null) foreach (var label in GameText.In(title)) Mark(label);
            if (toggle?.Button.Root != null && modes.Count > 1) PlaceUnder(toggle.Button.Root.transform, modes);
        }

        [HarmonyPrefix, HarmonyPatch(nameof(GamemodeSelectionMenu.StartGame))]
        private static bool StartGame(GamemodeSelectionMenu __instance)
        {
            if (!Settings.ModdedLobbies || !Settings.MakeRoomsModded || !__instance._isQuickMatch) return true;
            MelonCoroutines.Start(ModdedMenu.JoinRandom(__instance._activeMode?.ModeInfo?.ModeName ?? "Classic"));
            return false;
        }

        private static void AddToggle(GamemodeSelectionMenu menu)
        {
            var settings = menu.transform.Find("MainLayout/TopLayout/AdvandedSettingsButton");
            if (settings != null) toggle = new GameToggle(settings, "Modded", () => Settings.MakeRoomsModded, on => Settings.MakeRoomsModded = on);
        }

        private static void PlaceUnder(Transform button, List<MapModeSelect> modes)
        {
            var first = Center(modes[0].transform);
            var last = Center(modes[modes.Count - 1].transform);
            button.position += last + (last - first) / (modes.Count - 1) - Center(button);
        }

        private static void Mark(Component label)
        {
            string text = GameText.Get(label);
            string plain = text.StartsWith("Modded ") ? text.Substring(7) : text;
            string wanted = Settings.MakeRoomsModded && plain.Trim() != "" ? "Modded " + plain : plain;
            if (text == wanted) return;
            GameText.Set(label, wanted);
            GameText.Paint(label, Settings.MakeRoomsModded ? GameText.Gold : Color.white);
        }

        private static Vector3 Center(Transform button) => button.GetComponentsInChildren<MeshRenderer>().OrderByDescending(part => part.bounds.size.x).First().bounds.center;
    }
}
