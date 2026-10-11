using Utilus.Config;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Utilus.Compatibility
{
    internal static class AntiTypeThroughGUI
    {
        static Keyboard? keyboard;

        public static void OnGUI()
        {
            if (Event.current?.type == EventType.MouseDown && !ModWindows.MouseOverGUI) GUIUtility.keyboardControl = 0;
            bool typing = Settings.ModCompatibility && !ModGui.Hidden && GUIUtility.keyboardControl != 0;
            if (typing == (keyboard != null)) return;
            if (typing)
            {
                keyboard = Keyboard.current;
                if (keyboard != null) InputSystem.DisableDevice(keyboard, false);
            }
            else
            {
                InputSystem.EnableDevice(keyboard);
                keyboard = null;
            }
        }
    }
}
