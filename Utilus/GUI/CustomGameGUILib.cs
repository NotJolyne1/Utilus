using Il2CppInterop.Runtime;
using Il2CppSG.Airlock.UI.TitleScreen;
using UnityEngine;
using UnityEngine.Events;

namespace Utilus.GUI
{
    using Object = UnityEngine.Object;

    internal static class GameText
    {
        public static readonly Color Gold = new(1f, 0.8f, 0.15f);

        public static IEnumerable<Component> In(Transform root) => root.GetComponentsInChildren<Component>(true).Where(part => part.GetIl2CppType().Name == "TextMeshPro");
        public static string Get(Component text) => text.GetIl2CppType().GetProperty("text").GetValue(text)?.ToString() ?? "";
        public static void Set(Component text, string value) => text.GetIl2CppType().GetProperty("text").SetValue(text, (Il2CppSystem.String)value);
        public static void Paint(Component text, Color color) => text.GetIl2CppType().GetProperty("color").SetValue(text, color.BoxIl2CppObject());
    }

    internal class GameButton
    {
        public readonly GameObject Root;
        readonly Component? label;

        private GameButton(GameObject root, string text, Action click)
        {
            Root = root;
            foreach (var part in root.GetComponentsInChildren<Component>(true))
                if (part.GetIl2CppType().Name == "UserStringComponent_TMP") Object.DestroyImmediate(part);
                else if (part.GetIl2CppType().Name == "LUIButton") Listen(part, click);
            label = GameText.In(root.transform).FirstOrDefault();
            Text = text;
        }

        public static GameButton Use(Transform button, string text, Action click) => new(button.gameObject, text, click);
        public static GameButton Copy(Transform from, string text, Action click, Transform? parent = null)
        {
            var copy = Object.Instantiate(from.gameObject, parent ?? from.parent);
            copy.name = text;
            return new GameButton(copy, text, click);
        }

        public string Text { get => label == null ? "" : GameText.Get(label); set { if (label != null) GameText.Set(label, value); } }
        public Color Color { set { if (label != null) GameText.Paint(label, value); } }

        public GameButton At(float x, float y)
        {
            Root.transform.localPosition = new Vector3(x, y, Root.transform.localPosition.z);
            return this;
        }

        public GameButton Wide(float times)
        {
            var frame = Root.transform.Find("LUI_Button_Frame");
            frame.localScale = new Vector3(times, 1, 1);
            foreach (var text in GameText.In(frame))
            {
                text.transform.localScale = new Vector3(1 / times, 1, 1);
                var box = text.GetComponent<RectTransform>();
                box.sizeDelta = new Vector2(box.sizeDelta.x * times, box.sizeDelta.y);
            }
            return this;
        }

        private static void Listen(Component button, Action click)
        {
            var fields = Il2CppSystem.Reflection.BindingFlags.Instance | Il2CppSystem.Reflection.BindingFlags.Public | Il2CppSystem.Reflection.BindingFlags.NonPublic;
            var released = button.GetIl2CppType().GetField("OnReleased", fields).GetValue(button).Cast<UnityEvent>();
            for (int i = 0; i < released.GetPersistentEventCount(); i++)
                if (released.GetPersistentMethodName(i) != "PlaySoundpack") released.SetPersistentListenerState(i, UnityEventCallState.Off);
            released.AddListener(click);
        }
    }

    internal class GameToggle
    {
        public readonly GameButton Button;
        readonly string caption;
        readonly Func<bool> get;
        readonly Action<bool> set;

        public GameToggle(Transform from, string caption, Func<bool> get, Action<bool> set)
        {
            (this.caption, this.get, this.set) = (caption, get, set);
            Button = GameButton.Copy(from, caption, () => { set(!get()); Refresh(); });
            Refresh();
        }

        public void Refresh()
        {
            Button.Text = $"{caption}: {(get() ? "On" : "Off")}";
            Button.Color = get() ? GameText.Gold : Color.white;
        }
    }

    internal class GamePage
    {
        readonly Menu menu;
        readonly Transform back, row;
        readonly List<GameButton> rows = new();

        public static Transform? Menus => Object.FindObjectsOfType<Transform>(true).FirstOrDefault(t => t.name == "Menus" && t.parent?.name == "MainMenu3D");
        public static void GoTo(Menu menu) => menu._menuManager.TransitionTo(menu._key);
        public static void Popup(string message) => Menus?.Find("PlayOnlineMenu")?.GetComponent<Menu>()._menuManager.ShowError(message);

        public static GamePage? Create(string name, string title)
        {
            var play = Menus?.Find("PlayOnlineMenu");
            if (play == null || play.parent.Find(name) != null) return null;
            bool open = play.gameObject.activeSelf;
            play.gameObject.SetActive(false);
            var copy = Object.Instantiate(play.gameObject, play.parent);
            play.gameObject.SetActive(open);
            copy.name = name;
            return new GamePage(copy.transform, play.GetComponent<Menu>(), title);
        }

        private GamePage(Transform root, Menu previous, string title)
        {
            menu = root.GetComponent<Menu>();
            menu._key = ScriptableObject.CreateInstance(Il2CppType.Of<MenuKey>()).Cast<MenuKey>();
            menu._key.name = root.name;
            menu.AddChild(previous._menuManager);
            menu.Initialize();
            previous._menuManager._menuLookUpTable[menu._key] = menu;

            var heading = root.Find("PlayTitle");
            GameButton.Use(heading, title, () => { });
            var box = heading.GetComponent<RectTransform>();
            box.sizeDelta = new Vector2(box.sizeDelta.x * 3, box.sizeDelta.y);

            back = root.Find("BackButton");
            GameButton.Use(back, "Back", () => GoTo(previous));
            row = root.Find("MainLayout/BottomBar/HowToPlay");
            row.SetParent(root, false);
            row.gameObject.SetActive(false);
            foreach (var icon in row.GetComponentsInChildren<Transform>(true).Where(part => part.name.StartsWith("Icon"))) icon.gameObject.SetActive(false);
            Object.DestroyImmediate(root.Find("MainLayout").gameObject);
        }

        public void Open() => GoTo(menu);

        public GameButton AddButton(string text, float x, Action click) => GameButton.Copy(back, text, click).At(x, back.localPosition.y);

        public GameButton AddRow(string text, Action click)
        {
            var added = GameButton.Copy(row, text, click).Wide(2.2f).At(0, 300 - rows.Count * 125);
            added.Root.SetActive(true);
            rows.Add(added);
            return added;
        }

        public void ClearRows()
        {
            foreach (var added in rows) Object.Destroy(added.Root);
            rows.Clear();
        }
    }
}
