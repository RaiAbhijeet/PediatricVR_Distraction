// ============================================================
// PediatricVR — BuildHelper.cs  [EDITOR ONLY]
//
// Adds a "PediatricVR" menu to the Unity Editor toolbar.
// One-click APK build targeting Meta Quest 3S.
//
// Usage: Unity menu bar → PediatricVR → Build Quest APK
//
// Prerequisites before building:
//   1. Build target is Android (File → Build Settings → Android)
//   2. Quest 3S connected via USB with Developer Mode enabled
//   3. Meta XR SDK installed and OVR project configured
//   4. websocket-sharp.dll in Assets/Plugins/WebSocketSharp/
//   5. IL2CPP scripting backend (Project Settings → Player → Android)
// ============================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System.IO;

namespace PediatricVR.Editor
{
    public static class BuildHelper
    {
        private const string AppId      = "com.pediatricvr.distraction";
        private const string AppName    = "PediatricVR";
        private const string ScenePath  = "Assets/Scenes/Main.unity";
        private const string BuildPath  = "Builds/PediatricVR.apk";

        // ══════════════════════════════════════════════════════
        // Menu Items
        // ══════════════════════════════════════════════════════

        [MenuItem("PediatricVR/Build Quest APK %&b", false, 1)]
        public static void BuildQuestAPK()
        {
            if (!ValidateScene()) return;

            ConfigurePlayerSettings();

            Directory.CreateDirectory("Builds");

            var options = new BuildPlayerOptions
            {
                scenes             = new[] { ScenePath },
                locationPathName   = BuildPath,
                target             = BuildTarget.Android,
                targetGroup        = BuildTargetGroup.Android,
                options            = BuildOptions.None
            };

            Debug.Log("[Build] Starting PediatricVR Quest build…");
            BuildReport report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Build] ✓ Build succeeded → {Path.GetFullPath(BuildPath)}");
                Debug.Log($"[Build] Size: {report.summary.totalSize / 1048576f:F1} MB");
                EditorUtility.RevealInFinder(BuildPath);

                ShowADBInstructions();
            }
            else
            {
                Debug.LogError($"[Build] ✗ Build FAILED — {report.summary.totalErrors} error(s).");
                foreach (var step in report.steps)
                {
                    foreach (var msg in step.messages)
                    {
                        if (msg.type == LogType.Error || msg.type == LogType.Exception)
                            Debug.LogError($"  [{step.name}] {msg.content}");
                    }
                }
            }
        }

        [MenuItem("PediatricVR/Install APK via ADB", false, 2)]
        public static void InstallViaADB()
        {
            if (!File.Exists(BuildPath))
            {
                EditorUtility.DisplayDialog("APK Not Found",
                    $"No APK found at: {BuildPath}\nBuild the APK first.", "OK");
                return;
            }

            // Run ADB install
            RunADB($"install -r \"{Path.GetFullPath(BuildPath)}\"");
        }

        [MenuItem("PediatricVR/Set as Default Launcher (Auto-Boot)", false, 3)]
        public static void SetAsDefaultLauncher()
        {
            string activity = $"{AppId}/com.unity3d.player.UnityPlayerGameActivity";
            RunADB($"shell cmd package set-home-activity {activity}");
            Debug.Log($"[ADB] Set {AppId} as default launcher.");
            Debug.Log("[ADB] Quest will now auto-launch PediatricVR on boot.");
        }

        [MenuItem("PediatricVR/Open Device Portal", false, 4)]
        public static void OpenDevicePortal()
        {
            // Opens Meta Quest Developer Hub or device portal
            Application.OpenURL("https://developer.oculus.com/manage/");
        }

        [MenuItem("PediatricVR/Log WebSocket Test Command (ADB)", false, 20)]
        public static void LogTestCommand()
        {
            Debug.Log("[Test] To test WebSocket without a phone browser, run:");
            Debug.Log("  wscat -c ws://[QUEST-IP]:8080/control");
            Debug.Log("  Then send: {\"cmd\":\"start\",\"profile\":\"A\"}");
            Debug.Log("  Install wscat: npm install -g wscat");
        }

        // ══════════════════════════════════════════════════════
        // Private Helpers
        // ══════════════════════════════════════════════════════

        private static bool ValidateScene()
        {
            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("Scene Not Found",
                    $"Expected scene at: {ScenePath}\n\nCreate your main scene there first.", "OK");
                return false;
            }
            return true;
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.applicationIdentifier = AppId;
            PlayerSettings.productName           = AppName;
            PlayerSettings.bundleVersion         = "1.0.0";

            // Android — Quest 3S minimum API is 32 (Android 12)
            PlayerSettings.Android.minSdkVersion    = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel33;

            // IL2CPP required for Quest builds
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // VR settings — disable multithreaded rendering if causing issues
            PlayerSettings.MTRendering = false;

            Debug.Log("[Build] Player settings configured for Quest 3S.");
        }

        private static void RunADB(string args)
        {
            // Attempt to find adb in common locations
            string adbPath = FindADB();
            if (string.IsNullOrEmpty(adbPath))
            {
                Debug.LogError("[ADB] adb not found. Add Android SDK platform-tools to your PATH.");
                return;
            }

            var proc = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName               = adbPath,
                    Arguments              = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                }
            };

            proc.Start();
            string output = proc.StandardOutput.ReadToEnd();
            string error  = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            if (!string.IsNullOrEmpty(output)) Debug.Log($"[ADB] {output.Trim()}");
            if (!string.IsNullOrEmpty(error))  Debug.LogWarning($"[ADB] {error.Trim()}");
        }

        private static string FindADB()
        {
            // Check environment PATH first
            string envPath = System.Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string dir in envPath.Split(Path.PathSeparator))
            {
                string candidate = Path.Combine(dir, Application.platform == RuntimePlatform.WindowsEditor ? "adb.exe" : "adb");
                if (File.Exists(candidate)) return candidate;
            }

            // Check Android SDK path from Unity preferences
            string sdkRoot = EditorPrefs.GetString("AndroidSdkRoot");
            if (!string.IsNullOrEmpty(sdkRoot))
            {
                string adb = Path.Combine(sdkRoot, "platform-tools", "adb");
                if (Application.platform == RuntimePlatform.WindowsEditor) adb += ".exe";
                if (File.Exists(adb)) return adb;
            }

            return null;
        }

        private static void ShowADBInstructions()
        {
            bool install = EditorUtility.DisplayDialog(
                "Build Succeeded",
                $"APK built at:\n{Path.GetFullPath(BuildPath)}\n\nInstall to connected Quest now?",
                "Install via ADB",
                "No, I'll install manually"
            );

            if (install) InstallViaADB();
        }
    }
}
#endif
