using MelonLoader;

[assembly: MelonInfo(typeof(Utilus.Main), "Utilus", "1.0.0", "Jolyne/Shadow.cs", "")]
[assembly: MelonGame("Schell Games", null)]
namespace Utilus
{
    public class Main : MelonMod
    {
        public override void OnApplicationStart()
        {
            MelonLogger.Msg("Utilus has been loaded!");
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

        public override void OnGUI()
        {
            Compatibility.ModGui.OnGUI();
            Compatibility.ModWindows.OnGUI();
            Compatibility.AntiTypeThroughGUI.OnGUI();
        }
    }
}
