using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Patches duplicate com.oculus.Integration namespace in Meta XR AARs before
/// each Android build. Both OVRPlugin.aar and InteractionSdk.aar in Meta XR SDK v201+
/// ship with the same package= attribute, which causes a Gradle 9 namespace conflict
/// in Unity 6.3+.
///
/// HOW TO USE:
/// 1. Place this file in Assets/Editor/MetaAarNamespacePatcher.cs
/// 2. Runs automatically before every Android build via IPreprocessBuildWithReport
/// 3. Or run manually via menu: Build > Patch Meta AAR Namespaces
///
/// IMPORTANT: If you clear the Library/ folder or package cache, the patch
/// re-applies automatically on the next build — no action needed.
/// </summary>
public class MetaAarNamespacePatcher : IPreprocessBuildWithReport
{
    public int callbackOrder => -100;

    private const string Tag = "[MetaAarNamespacePatcher]";

    private static readonly (string packagePrefix, string aarRelativePath, string newNamespace)[] Patches =
    {
        (
            "com.meta.xr.sdk.core",
            "Plugins/AndroidOpenXR/OVRPlugin.aar",
            "com.oculus.Integration.core"
        ),
        (
            "com.meta.xr.sdk.interaction",
            "Runtime/Plugins/Android/InteractionSdk.aar",
            "com.oculus.Integration.interaction"
        ),
    };

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android)
            return;

        ApplyAll();
    }

    [MenuItem("Build/Patch Meta AAR Namespaces")]
    public static void PatchManually()
    {
        ApplyAll();
        Debug.Log($"{Tag} Done.");
    }

    private static void ApplyAll()
    {
        string sevenZa = EditorApplication.sevenZipPath;
        if (string.IsNullOrEmpty(sevenZa) || !File.Exists(sevenZa))
        {
            Debug.LogError($"{Tag} 7za not found at: {sevenZa}. Cannot patch AARs.");
            return;
        }

        string packageCache = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "Library", "PackageCache"));

        if (!Directory.Exists(packageCache))
        {
            Debug.LogWarning($"{Tag} PackageCache not found at: {packageCache}");
            return;
        }

        foreach (var (prefix, relativePath, newNs) in Patches)
            PatchAar(sevenZa, packageCache, prefix, relativePath, newNs);
    }

    private static void PatchAar(string sevenZa, string packageCache, string packagePrefix,
                                  string aarRelativePath, string newNamespace)
    {
        string[] candidates = Directory.GetDirectories(packageCache, packagePrefix + "@*",
                                                       System.IO.SearchOption.TopDirectoryOnly);
        if (candidates.Length == 0)
        {
            Debug.LogWarning($"{Tag} Package '{packagePrefix}' not found in PackageCache.");
            return;
        }

        string aarPath = Path.Combine(candidates[0], aarRelativePath);
        if (!File.Exists(aarPath))
        {
            Debug.LogWarning($"{Tag} AAR not found: {aarPath}");
            return;
        }

        string tempDir = Path.Combine(Path.GetTempPath(), "MetaAarPatch_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Extract the AAR (it's a zip)
            RunProcess(sevenZa, $"x \"{aarPath}\" -o\"{tempDir}\" -y");

            string manifestPath = Path.Combine(tempDir, "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning($"{Tag} AndroidManifest.xml not found inside {aarPath}");
                return;
            }

            string content = File.ReadAllText(manifestPath);
            string originalContent = content;

            // Replace the package= attribute with the new unique namespace
            content = Regex.Replace(
                content,
                @"package=""com\.oculus\.Integration""",
                $"package=\"{newNamespace}\""
            );

            if (content == originalContent)
            {
                Debug.Log($"{Tag} {Path.GetFileName(aarPath)} already patched or namespace not found — skipping.");
                return;
            }

            File.WriteAllText(manifestPath, content);

            // Repack — delete original aar entries and re-add from temp folder
            // First backup the original
            string backupPath = aarPath + ".bak";
            if (!File.Exists(backupPath))
                File.Copy(aarPath, backupPath);

            // Update the manifest inside the AAR
            RunProcess(sevenZa, $"u \"{aarPath}\" \"{manifestPath}\"");

            Debug.Log($"{Tag} Patched {Path.GetFileName(aarPath)} -> {newNamespace}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"{Tag} Failed to patch {aarPath}: {ex.Message}");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    private static void RunProcess(string exe, string args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(exe, args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(psi);
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            string err = process.StandardError.ReadToEnd();
            throw new System.Exception($"7za exited with code {process.ExitCode}: {err}");
        }
    }
}