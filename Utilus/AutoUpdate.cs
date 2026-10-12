using MelonLoader;
using MelonLoader.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using UnityEngine;

namespace Utilus
{
    internal static class AutoUpdate
    {
        const string versionUrl = "https://raw.githubusercontent.com/NotJolyne1/Utilus/master/Version.txt";
        const string releaseUrl = "https://api.github.com/repos/NotJolyne1/Utilus/releases?per_page=1";

        internal static async void Check(string current)
        {
            try
            {
                using HttpClient client = CreateClient();
                string latest = (await client.GetStringAsync(versionUrl)).Trim().TrimStart('v', 'V');

                if (!Version.TryParse(current.Trim().TrimStart('v', 'V'), out Version installed)) return;
                if (!Version.TryParse(latest, out Version newest)) return;
                if (installed >= newest)
                {
                    MelonLogger.Msg("Utilus is up to date");
                    return;
                }

                MelonLogger.Msg($"Updating Utilus to {latest}");
                await Install(client, latest);
            }
            catch (Exception error)
            {
                MelonLogger.Error($"Update check failed! Please update utilus to prevent issues and report this: {error.Message}");
            }
        }

        private static async Task Install(HttpClient client, string version)
        {
            using JsonDocument releases = JsonDocument.Parse(await client.GetStringAsync(releaseUrl));
            if (releases.RootElement.GetArrayLength() == 0) throw new InvalidDataException("No releases were found");

            string download = "";
            foreach (JsonElement asset in releases.RootElement[0].GetProperty("assets").EnumerateArray())
            {
                string name = asset.GetProperty("name").GetString() ?? "";
                if (name.Equals("Utilus.dll", StringComparison.OrdinalIgnoreCase))
                    download = asset.GetProperty("browser_download_url").GetString() ?? "";
            }
            if (string.IsNullOrEmpty(download)) throw new InvalidDataException("Utilus.dll was not in the latest release");

            int pid = Process.GetCurrentProcess().Id;
            string game = Process.GetCurrentProcess().MainModule.FileName;
            string modPath = Path.Combine(MelonEnvironment.ModsDirectory, "Utilus.dll");
            string staged = Path.Combine(Path.GetTempPath(), "Utilus.dll.new");
            string script = Path.Combine(Path.GetTempPath(), "UtilusUpdate.bat");

            await File.WriteAllBytesAsync(staged, await client.GetByteArrayAsync(download));
            File.WriteAllText(script, BuildBatch(pid, modPath, staged, game, version));

            Process.Start(new ProcessStartInfo { FileName = script, UseShellExecute = true });
            Application.Quit();
        }

        private static HttpClient CreateClient()
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Utilus-Updater");
            client.Timeout = TimeSpan.FromSeconds(30);
            return client;
        }

        private static string BuildBatch(int pid, string modPath, string staged, string game, string version)
        {
            return $"@echo off\r\ntitle Utilus Updater\r\ncolor 0B\r\necho.\r\necho Utilus Updater\r\necho Installing Utilus {version}. Among Us 3D will restart on its own.\r\necho If your anti virus warns about this window it is a false positive, all it does is move one file and reopen the game.\r\necho.\r\necho Waiting for Among Us 3D to close...\r\ntaskkill /PID {pid} /F >nul 2>&1\r\n:wait\r\ntasklist /FI \"PID eq {pid}\" /NH | find \"{pid}\" >nul\r\nif not errorlevel 1 (timeout /t 1 /nobreak >nul & goto wait)\r\nmove /Y \"{staged}\" \"{modPath}\" >nul\r\nif errorlevel 1 goto manual\r\necho Done, starting Among Us 3D...\r\ntimeout /t 1 /nobreak >nul\r\nstart \"\" \"{game}\"\r\ntimeout /t 3 /nobreak >nul\r\nexit /b\r\n:manual\r\necho.\r\necho Could not write to the Mods folder. Move this file yourself:\r\necho {staged}\r\necho to:\r\necho {modPath}\r\npause\r\nexit /b";
        }
    }
}
