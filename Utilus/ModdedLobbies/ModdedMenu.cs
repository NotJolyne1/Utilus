using System.Collections;
using HarmonyLib;
using Il2CppFusion;
using Il2CppSG.Airlock;
using Il2CppSG.Airlock.Network;
using Il2CppSG.Airlock.UI.TitleScreen;
using Utilus.Config;
using Utilus.GUI;
using MelonLoader;
using UnityEngine;

namespace Utilus.ModdedLobbies
{
    using Object = UnityEngine.Object;

    internal static class ModdedMenu
    {
        static AirlockPeer? lobby;
        static NetworkRunner? lobbyRunner;
        static readonly List<(SessionInfo room, string mode, string text)> rooms = new();
        static bool loading;
        static string joinCode = "";
        static GamePage? page;

        internal static void Setup()
        {
            if (!Settings.ModdedLobbies) return;
            var buttons = GamePage.Menus?.Find("PlayOnlineMenu/MainLayout/TopBar/MatchButtons");
            var directCode = buttons?.Find("DirectCode");
            if (buttons == null || directCode == null) return;
            page = GamePage.Create("ModdedLobbiesMenu", "Modded Lobbies");
            if (page == null) return;
            page.AddButton("Refresh", 160, () => MelonCoroutines.Start(ShowRooms()));
            page.AddButton("Join Random", 480, JoinAny);

            var card = GameButton.Copy(directCode, "Modded Lobbies", Open).At(directCode.localPosition.x + 350, directCode.localPosition.y);
            buttons.localPosition -= new Vector3(175, 0, 0);
            ShowBadge(card.Root.transform);
        }

        private static void ShowBadge(Transform card)
        {
            var picture = card.Find("LUI_Button_Frame/Icon")?.GetComponent<SpriteRenderer>();
            var tile = card.Find("LUI_Button_Frame/Icon (2)");
            if (picture == null || tile == null) return;
            picture.sprite = Badge(picture.sprite);
            picture.transform.localPosition = tile.localPosition;
            picture.transform.localScale *= 1.25f;
            picture.gameObject.SetActive(true);
            tile.gameObject.SetActive(false);
        }

        private static void Open()
        {
            page?.Open();
            MelonCoroutines.Start(ShowRooms());
        }

        private static IEnumerator ShowRooms()
        {
            Fill("Searching...");
            yield return Refresh();
            Fill(rooms.Count == 0 ? "No modded lobbies right now" : "");
        }

        private static void Fill(string message)
        {
            if (page == null) return;
            page.ClearRows();
            if (message != "") page.AddRow(message, () => MelonCoroutines.Start(ShowRooms()));
            else foreach (var found in rooms.Take(5)) page.AddRow(found.text, () => Join(found.room));
        }

        private static IEnumerator Refresh()
        {
            if (loading) yield break;
            loading = true;
            rooms.Clear();
            try
            {
                if (lobbyRunner == null)
                {
                    var source = Resources.FindObjectsOfTypeAll<AirlockPeer>().FirstOrDefault(peer => !peer.name.Contains("(Clone)"));
                    if (source == null) yield break;
                    var existing = Object.FindObjectsOfType<NetworkRunner>(true).Select(runner => runner.GetInstanceID()).ToHashSet();
                    var copy = Object.Instantiate(source.gameObject);
                    Object.DontDestroyOnLoad(copy);
                    copy.SetActive(true);
                    lobby = copy.GetComponent<AirlockPeer>();
                    lobby.JoinLobbyAuthorized();
                    for (int i = 0; i < 24 && (lobby.Sessions == null || lobby.Sessions.Count == 0); i++) yield return new WaitForSeconds(0.25f);
                    lobbyRunner = Object.FindObjectsOfType<NetworkRunner>(true).FirstOrDefault(runner => !existing.Contains(runner.GetInstanceID()));
                }

                if (lobby?.Sessions == null) yield break;
                foreach (var room in lobby.Sessions)
                {
                    if (room?.Properties == null || !room.Properties.TryGetValue("gamemode", out var tagged) || tagged == null || JoinOrCreateModdedRoom.Mode(tagged) is not int number) continue;
                    string mode = ((GameModes)number).ToString().Replace("Gameplay", "Classic");
                    string state = (int)room.Properties["game_in_progress"] == 1 ? "In Game" : "Lobby";
                    rooms.Add((room, mode, $"{room.Name}   {room.PlayerCount}/{room.MaxPlayers}   {(string)room.Properties["mapname"]}   {mode.Replace("Infection", "Tag")}   {state}"));
                }
                rooms.Sort((a, b) => b.room.PlayerCount.CompareTo(a.room.PlayerCount));
            }
            finally
            {
                loading = false;
            }
        }

        private static void JoinAny()
        {
            var open = rooms.Where(found => found.room.PlayerCount < found.room.MaxPlayers).ToList();
            if (open.Count == 0) GamePage.Popup("No modded lobbies to join right now");
            else Join(open[UnityEngine.Random.Range(0, open.Count)].room);
        }

        internal static IEnumerator JoinRandom(string mode)
        {
            yield return Refresh();
            var picks = rooms.Where(found => found.mode == mode && found.room.PlayerCount < found.room.MaxPlayers).ToList();
            if (picks.Count == 0) GamePage.Popup($"No Modded {mode} lobbies right now");
            else Join(picks[UnityEngine.Random.Range(0, picks.Count)].room);
        }

        private static void Join(SessionInfo room)
        {
            var menu = GamePage.Menus?.Find("DirectJoinMenu")?.GetComponent<DirectJoinMenu>();
            if (menu == null) return;
            joinCode = room.Name;
            GamePage.GoTo(menu);
        }

        [HarmonyPatch(typeof(AirlockPeer), nameof(AirlockPeer.DirectJoinFindGame))]
        class UseJoinCode
        {
            private static void Prefix(ref string pGameName)
            {
                if (joinCode == "") return;
                pGameName = joinCode;
                joinCode = "";
            }
        }

        [HarmonyPatch(typeof(NetworkRunner), nameof(NetworkRunner.GetRunnerForScene), typeof(int))]
        class SkipLobbyRunner
        {
            private static bool Prefix(ref NetworkRunner __result)
            {
                if (lobbyRunner == null) return true;
                var runners = NetworkRunner.GetInstancesEnumerator();
                while (runners.MoveNext())
                    if (runners.Current != null && runners.Current.Pointer != lobbyRunner.Pointer)
                    {
                        __result = runners.Current;
                        return false;
                    }
                return true;
            }
        }

        private static Sprite Badge(Sprite like)
        {
            int size = (int)like.rect.width;
            var icon = Convert.FromBase64String(RepairIcon);
            var navy = new Color(0.12f, 0.15f, 0.3f);
            var art = new Texture2D(size, size) { hideFlags = HideFlags.HideAndDontSave };

            Color Sample(double dx, double dy)
            {
                double reach = Math.Sqrt(dx * dx + dy * dy) / (size / 2.0);
                int ix = (int)((dx / (size * 0.62) + 0.5) * 240), iy = (int)((0.5 - dy / (size * 0.62)) * 240);
                bool drawn = ix >= 0 && ix < 240 && iy >= 0 && iy < 240 && (icon[(iy * 240 + ix) / 8] >> ((iy * 240 + ix) % 8) & 1) == 1;
                return reach > 1 ? Color.clear : reach > 0.93 || drawn ? Color.white : navy;
            }

            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                {
                    double cx = x - size / 2.0, cy = y - size / 2.0;
                    var a = Sample(cx + 0.25, cy + 0.25);
                    var b = Sample(cx + 0.75, cy + 0.25);
                    var c = Sample(cx + 0.25, cy + 0.75);
                    var d = Sample(cx + 0.75, cy + 0.75);
                    art.SetPixel(x, y, new Color((a.r + b.r + c.r + d.r) / 4, (a.g + b.g + c.g + d.g) / 4, (a.b + b.b + c.b + d.b) / 4, (a.a + b.a + c.a + d.a) / 4));
                }
            art.Apply();
            return Sprite.Create(art, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), like.pixelsPerUnit);
        }

        const string RepairIcon = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD+AwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/PwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/PwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/PwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB4AAID/PwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAwD8AAID/fwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA+D8AAMD/fwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/n8AAMD/fwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA//8AAOD//wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA//8BAOD//wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA//8DAOD//wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA//8DAPD//wAAADgAAAAAAAAAAAAAAAAAAAAAAAAA//8HAPD//wEAAPwAAAAAAAAAAAAAAAAAAAAAAAAA//8PAPj//wEAAP4DAAAAAAAAAAAAAAAAAAAAAAAA//8fAPj//wEAAP8HAAAAAAAAAAAAAAAAAAAAAAAA//8fAPz//wEAgP8fAAAAAAAAAAAAAAAAAAAAAAAA//8/gP///wMAwP9/AAAAAAAAAAAAAAAAAAAAAAAA/////////wMA4P9/AAAAAAAAAAAAAAAAAAAAAAAA/////////w8A+P9/AAAAAAAAAAAAAAAAAAAAAAAA//////////8A/P9/AAAAAAAAAAAAAAAAAAAAAAAA//////////8H/v9/AAAAAAAAAAAAAAAAAAAAAAAA//////////8f//8/AAAAAAAAAAAAAAAAAAAAAAAA//////////////8/AAAAAAAAAAAAAAAAAAAAAAAA//////////////8/AAAAAAAAAAAAAAAAAAAAAAAA//////////////8/AAAAAAAAAAAAAAAAAAAAAAAA//////////////8/AAAAAAAAAAAAAAAAAAAAAADA//////////////8fAAAAAAAAAAAAAAAAAAACAADw//////////////8fAAAAAAAAAAAAAAAAAAAPAAD4//////////////8fAAAAAAAAAAAAAAAAAIA/AAD+//////////////8fAAAAAAAAAAAAAAAAAMD/AQD///////////////8fAAAAAAAAAAAAAAAAAOD/B8D///////////////8PAAAAAAAAAAAAAAAAAPD/H+D///////////////8PAAAAAAAAAAAAAAAAAPj/f/D///////////////8PAAAAAAAAAAAAAAAAAPj///3///////////////8PAAAAAAAAAAAAAAAAAPz/////////PwAA/v////8fAAAAAAAAAAAAAAAAAP7/////////AQAAgP////9/AAAAAAAAAAAAAAAAAPz///////8fAAAAAPj/////AAAAAAAAAAAAAAAAAPz///////8DAAAAAMD/////AQDwAAAAAAAAAAAAAPj//////38AAAAAAAD/////AwD+AAAAAAAAAAAAAPj//////x8AAAAAAAD4////B+D/AQAAAAAAAAAAAPD//////wMAAAAAAADg////D/z/AwAAAAAAAAAAAPD//////wAAAAAAAACA////3///AwAAAAAAAAAAAOD/////fwAAAAAAAAAA////////BwAAAAAAAAAAAOD/////HwAAAAAAAAAA/P//////DwAAAAAAAAAAAMD/////BwAAAAAAAAAA8P//////DwAAAAAAAAAAAMD/////AwAAAAAAAAAA4P//////HwAAAAAAAAAAAID/////AAAAAAAAAAAAgP//////PwAAAAAAAAAAAID///9/AAAAAAAAAAAAAP//////PwAAAAAAAAAAAAD///8/AAAAAAAAAAAAAP7/////HwAAAAAAAAAAAAD///8fAAAAAAAAAAAAAPj/////DwAAAAAAAAAAAAD+//8HAAAAAAAAAAAAAPD/////DwAAAAAAAAAAAAD+//8DAAAAAAAAAAAAAOD/////BwAAAAAAAAAAAAD+//8BAAAAAAAAAAAAAMD/////AwAAAAAAAAAAAAD///8AAAAAAAAAAAAAAID/////AwAAAAAAAAAAAID//38AAAAAAAAAAAAAAAD/////AQAAAAAAAAAAAID//z8AAAAAAAAAAAAAAAD+////AAAAAAAAAAAAAMD//x8AAAAAAAAAAAAAAAD8//9/AAAAAAAAAAAAAOD//w8AAAAAAAAAAAAAAAD4//9/AAAAAAAAAAAAAOD//w8AAAAAAAAAAAAAAAD4//8/AAAAAAAAAAAAAPD//wcAAAAAAAAAAAAAAADw//8fAAAAAAAAAAAAAPD//wMAAAAAAAAAAAAAAADg//8PAAAAAAAAAAAAAPj//wEAAAAAAAAAAAAAAADA//8PAAAAAAAAAAAAAPj//wAAAAAAAAAAAAAAAADA//8PAAAAAAAAAAAAAPz//wAAAAAAAAAAAAAAAACA//8PAAAAAAAAAAD/////fwAAAAAAAAAAAAAAAAAA//8fAAAAAAAAAAD/////fwAAAAAAAAAAAAAAAAAA//8fAAAAAAAAAID/////PwAAAAAAAAAAAAAAAAAA/v8/AAAAAAAAAID/////HwAAAAAAAAAAAAAAAAAA/P8/AAAAAAAAAID/////HwAAAAAAAAAAAAAAAAAA/P9/AAAAAAAAAMD/////DwAAAAAAAAAAAAAAAAAA+P9/AAAAAAAAAMD/////DwAAAAAAAAAAAAAAAAAA+P9/AAAAAAAAAMD/////BwAAAP8fAAAAAAAAAAAA8P//AAAAAAAAAOD/////BwAA8P//AQAAAAAAAAAA8P//AAAAAAAAAOD/////AwAA/P//BwAAAAAAAAAA4P//AAAAAAAAAOD/////AwAA////HwAAAAAAAAAA4P//AQAAAAAAAPD/////AQAA/v//fwAAAAAAAAAA4P//AQAAAAAAAOD/////AQAA+P///wAAAAAAAAAAwP//AwAAAAAAAMD/////AQAA8P///wMAAAAAAAAAwP//HwAAAAAAAAD/////AAAA4P///wcAAAAAAAAAwP///wMAAAAAAAD+////AAAAgP///w8AAAAAAAAAgP////8AAAAAAAD8////AAAAAP///x8AAAAAAAAAgP////8HAAAAAADw//9/AAAAAP7//z8AAAAAAAAAgP////8PAAAAAADg//9/AAAAAPj//38AAAAAAAAAAP////8PAAAAAACA//9/AAAAAPD///8AAAAAAAAAAP////8PAAAAAAAA//9/AAAAAOD///8AAAAAAAAAAP////8PAAAAAAAA/v8/AAAAAID///8BAAAAAAAAAP////8PAAAAAAAA+P8/AAAAAAD///8DAAAAAAAAAP7///8PAAAAAAAA+P8/AAAAAAD+//8DAAAAAAAAAP7///8PAAAAAAAA+P8/AAAAAAD4//8HAAAAAAAAAP7///8fAAAAAAAA+P8/AAAAAADw//8HAAAAAAAAAP7///8fAAAAAAAA+P8fAAAAAADg//8PAAAAAAAAAP7///8fAAAAAAAA/P8fAAAAAADA//8PAAAAAAAAAPz///8fAAAAAAAA/P8fAAAAAACA//8PAAAAAAAAAPz///8fAAAAAAAA/P8fAAAAAAAA//8fAAAAAAAAAPz///8HAAAAAAAA/P8fAAAAAAAA/v8fAAAAAAAAAPz///8BAAAAAAAA/P8fAAAAAAAA/v8fAAAAAAAAAPz//38AAAAAAAAA/P8fAAAAAAAA/P8/AAAAAAAAAPz//x8AAAAAAAAA/P8fAAAAAAAA/P8/AAAAAAAAAPz//wcAAAAAAAAA/P8fwAAAAAAA/P8/AAAAAAAAAPz//wEAAAAAAAAA/P8fwAEAAAAA+P8/AAAAAAAAAPz/fwAAAAAAAAAA/v8fwAcAAAAA+P8/AAAAAAAAAPz/HwAAAAAAAACA//8fwA8AAAAA+P8/AAAAAAAAAPz/HwAAAAAAAADg//8fwB8AAAAA+P8/AAAAAAAAAPz/HwAAAAAAAAD4//8fwD8AAAAA+P8/AAAAAAAAAPz/HwAAAAAAAAD+//8fwP8AAAAA+P8/AAAAAAAAAPz/HwAAAAAAAID///8fwP8BAAAA+P8/AAAAAAAAAPz/HwAAAAAAAOD///8fwP8DAAAA/P8/AAAAAAAAAPz/HwAAAAAAAPj///8fwP8PAAAA/P8/AAAAAAAAAPz/HwAAAAAAAPz///8fwP8fAAAA/P9/AAAAAAAAAPz/DwAAAAAAAPz///8fwP8/AAAA/v//AQAAAAAAAPz/DwAAAAAAAPz///8fgP//AAAA/v//AwAAAAAAAP7/DwAAAAAAAPz///8fgP//AQAA////BwAAAAAAAP7/DwAAAAAAAPz///8/gP//AwCA////HwAAAAAAAP7/DwAAAAAAAPz///8/gP//BwDA//v/PwAAAAAAAP7/DwAAAAAAAPz///8/AP//HwDw//H/fwAAAAAAAP7/DwAAAAAAAPj///8/AP//fwD8/8D//wAAAAAAAP//PwAAAAAAAPj///9/AP7//+f//4D//wMAAAAAAP//fwAAAAAAAPj///9/AP7/////fwD//wcAAAAAAP///wAAAAAAAPj///9/AP7/////PwD8/w8AAAAAAP///wMAAAAAAPj///9/APz/////HwD4/z8AAAAAgP///wcAAAAAAPj/////APj/////PwDw/38AAAAAgP///w8AAAAAAID/////APj/////fwDg//8AAAAAgP///z8AAAAAAADw////APD//////wCA//8DAAAAwP///38AAAAAAAAA/v//AeD//////wMA//8HAAAAwP////8AAAAAAAAA4P//AcD//////wcA/v8PAAAAwP////8DAAAAAAAAwP//AYD//////w8A+P8/AAAA4P////8DAAAAAAAAwP//AwD//////z8A8P9/AAAA4P////8DAAAAAAAAwP//AwD+/////38A4P//AAAA8P////8DAAAAAAAAgP//BwD8//////8AgP//AwAA8P////8BAAAAAAAAgP//BwD4//////8BAP//BwAA+P////8BAAAAAAAAgP//DwDg//////8HAP7/DwAA+P////8BAAAAAAAAAP//DwDA//////8PAPj/HwAA/P////8AAAAAAAAAAP//HwAA//////8fAPD/fwAA/P////8AAAAAAAAAAP7/HwAA+P//8f9/AOD//wAA/v////8AAAAAAAAAAP7/PwAAwP8fwP//AID//wEA/v///38AAAAAAAAAAP7/fwAAAAAAgP//AQD//wcA/v///38AAAAAAAAAAP//fwAAAAAAAP//BwD+/w8A+P///38AAAAAAAAAgP///wAAAAAAAPz/DwD8/x8A8P8fAAAAAAAAAAAAgP///wEAAAAAAPj/HwDw/38A4P8PAAAAAAAAAAAAwP///wEAAAAAAPD/fwDg//8AgP8HAAAAAAAAAAAA4P///wMAAAAAAMD//wDA//8BAP8HAAAAAAAAAAAA8P///wcAAAAAAID//wEA//8HAP4DAAAAAAAAAAAA+P///wcAAAAAAAD//wcA/v8PAPwDAAAAAAAAAAAA+P///w8AAAAAAAD+/w8A/P8fAPABAAAAAAAAAAAA/P///x8AAAAAAAD4/x8A8P9/AOABAAAAAAAAAAAA/v///z8AAAAAAADw/z8A4P//AMAAAAAAAAAAAAAA/////38AAAAAAADg//8AwP//AQAAAAAAAAAAAACA//////8AAAAAAACA//8BAP//AwAAAAAAAAAAAACA//////8BAAAAAAAA//8DAP7/DwAAAAAAAAAAAADA//////8DAAAAAAAA/v8PAPz/HwAAAAAAAAAAAADA//////8HAAAAAAAA+P8fAPD/PwAAAAAAAAAAAADA//////8PAAAAAAAA8P8/AOD//wAAAAAAAAAAAACA//////8/AAAAAAAA4P//AMD//wEAAAAAAAAAAAAA//////9/AAAAAAAAgP//AYD//wMAAAAAAAAAAAAA////////AAAAAAAAAP//AwD+/w8AAAAAAAAAAAAA/v//////AQAAAAAAAP7/DwD8/x8AAAAAAAAAAAAA/P+P////BwAAAAAAAPj/HwD4/z8A/gcAAAAAAAAA/P8A////DwAAAAAAAPD/PwDg///w/38AAAAAAAAA+A8A/v//PwAAAAAAAOD//wDA///9//8DAAAAAAAA+AAA/P///wAAAAAAAMD//wGA//////8fAAAAAAAAAAAA+P///wMAAAAAAAD//wMA/v////9/AAAAAAAAAAAA8P///w8AAAAAAAD+/w8A/P//////AAAAAAAAAAAA4P///z8AAAAAAAD8/x8A+P//////AwAAAAAAAAAAwP////8BAAAAAADw/z8A4P//////BwAAAAAAAAAAgP////8PAAAAAADg/38AwP//////DwAAAAAAAAAAAP////9/AAAAAADA//8BgP//////HwAAAAAAAAAAAP7/////DwAA8AEA//8DAP7/////PwAAAAAAAAAAAPz////////4/wMA/v8HAPz/////fwAAAAAAAAAAAPz//////////w8A/P8fAPj//////wAAAAAAAAAAAPz//////////x8A8P8/AOD//////wEAAAAAAAAAAPz//////////z8A4P9/AMD//////wEAAAAAAAAAAPz///////////8AwP//AYD//////wMAAAAAAAAAAP7///////////8BAP//A8D//////wMAAAAAAAAAAP7///////////8DAP7/B8D//////wcAAAAAAAAAAP7///////////8PAPz/H+D//////wcAAAAAAAAAAP7///////////8fAPj/P/D/B/j//w8AAAAAAAAAAP7///////////8/AOD/f/j/AcD//w8AAAAAAAAAAP//////////////AMD///l/AAD//x8AAAAAAAAAAP//////////////AYD///8/AAD+/x8AAAAAAAAAAP//////////////AwD+//8fAAD4/x8AAAAAAAAAAP//f/7/////////DwD8//8PAADw/x8AAAAAAAAAAP//P/D/////////HwD4//8HAADg/z8AAAAAAAAAgP//H4D/////////PwDg//8HAACA/z8AAAAAAAAAgP//DwD8/////////wDA//8DAAAA/z8AAAAAAAAAgP//AwDw//////D//wHA//8DAAAA/j8AAAAAAAAAgP//AQDw////AeD//wPA//8DAAAA+D8AAAAAAAAAAP//AADg//8PAMD//wfA//8BAAAA8D8AAAAAAAAAAP5/AADg//8PAID//wfA//8BAAAA4D8AAAAAAAAAAPg/AADg//8HAAD//wfA//8BAAAAgD8AAAAAAAAAAOAfAADg//8HAAD+/wfA//8BAAAAAD8AAAAAAAAAAIAPAADA//8DAAD8/wfA//8BAAAAAD4AAAAAAAAAAAADAADA//8DAAD8/wfA//8BAAAAADgAAAAAAAAAAAAAAADA//8DAAD4/wfA//8BAAAAADAAAAAAAAAAAAAAAADA//8BAADw/wHA//8DAAAAACAAAAAAAAAAAAAAAACA//8BAADgfwCA//8DAAAAAAAAAAAAAAAAAAAAAACA//8AAADADwCA//8HAAAAAAAAAAAAAAAAAAAAAACA//8AAACAAQCA//8HAAAAAAAAAAAAAAAAAAAAAACA/38AAAAAAACA//8PAAAAAAAAAAAAAAAAAAAAAAAA/38AAAAAAAAA//8PAAAAAAAAAAAAAAAAAAAAAAAA/z8AAAAAAAAA//8fAAAAAAAAAAAAAAAAAAAAAAAA/z8AAAAAAAAA//8/AAAAAAAAAAAAAAAAAAAAAAAA/z8AAAAAAAAA/v9/AAAAAAAAAAAAAAAAAAAAAAAAgB8AAAAAAAAA/v//AQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/P//AwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/P//BwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA+P//HwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA+P//PwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA8P//fwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA4P///wEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAwP///wMAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgP///wcAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP///x8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP7//z8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAPz//38AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAPj///8BAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAOD///8DAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAMD///8HAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD///8PAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD4//8BAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAADA/z8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA+AEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    }
}
