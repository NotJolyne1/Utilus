using MelonLoader;
using Microsoft.VisualBasic;
using static MelonLoader.MelonLaunchOptions;

[assembly: MelonInfo(typeof(JolyMod.Main), "JolyMod", "1.0.0", "Jolyne/Shadow.cs", "")]
[assembly: MelonGame("Schell Games", null)]
namespace JolyMod
{
    public class Main : MelonMod
    {
        public override void OnApplicationStart()
        {
            MelonLogger.Msg("JolyMod has been loaded!");
            Compatibility.AntiLogSpam.Start();
        }

        public override void OnLateInitializeMelon()
        {
            Compatibility.ModGui.Start(HarmonyInstance);
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            ModdedLobbies.ModdedMenu.Setup();
        }

        public override void OnUpdate()
        {
        }


        public override void OnGUI()
        {
            Compatibility.ModGui.OnGUI();
            Compatibility.ModWindows.OnGUI();
            Compatibility.AntiTypeThroughGUI.OnGUI();
        }
    }
}
