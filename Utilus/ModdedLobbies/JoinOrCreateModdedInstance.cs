using HarmonyLib;
using Il2CppFusion;
using Il2CppSG.Airlock;
using Utilus.Config;

namespace Utilus.ModdedLobbies
{
    [HarmonyPatch]
    internal class JoinOrCreateModdedRoom
    {
        static bool hostingModded;

        [HarmonyPrefix, HarmonyPatch(typeof(NetworkRunner), nameof(NetworkRunner.StartGame))]
        private static void StartGame(StartGameArgs args)
        {
            hostingModded = args.GameMode == GameMode.Host && Settings.MakeRoomsModded;
            if (!hostingModded || args.SessionProperties == null || !args.SessionProperties.ContainsKey("gamemode")) return;
            args.SessionProperties["gamemode"] = "modded_" + (int)args.SessionProperties["gamemode"];
        }

        [HarmonyPrefix, HarmonyPatch(typeof(SessionInfo), nameof(SessionInfo.UpdateCustomProperties))]
        private static void SendMode(Il2CppSystem.Collections.Generic.Dictionary<string, SessionProperty> customProperties)
        {
            if (!hostingModded || customProperties == null || !customProperties.ContainsKey("gamemode")) return;
            var mode = customProperties["gamemode"];
            if (mode.IsInt) customProperties["gamemode"] = "modded_" + (int)mode;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(GameSession), nameof(GameSession.SyncWithSessionInfo))]
        private static void ReadMode(GameSession __instance)
        {
            var properties = __instance.Runner?.SessionInfo?.Properties;
            if (properties == null || !properties.ContainsKey("gamemode")) return;
            var mode = properties["gamemode"];
            if (mode.IsString && Mode(mode) is int number) properties.m_dictionary["gamemode"] = number;
        }

        internal static int? Mode(SessionProperty value)
        {
            if (value.IsInt) return null;
            string text = value;
            return text.StartsWith("modded_") && int.TryParse(text.Substring("modded_".Length), out int mode) ? mode : null;
        }
    }
}
