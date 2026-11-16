using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// A repeatable Quest build. Roguelike > Quest Build > Check Settings compares the project with Meta's
    /// recommendations and only reports differences (it changes nothing, since an earlier build already works);
    /// Build APK builds every enabled scene to Builds/Roguelike-[date].apk; Build And Run also installs and starts it
    /// on a connected headset. Make App Icon creates a simple placeholder icon if none is set.
    /// </summary>
    public static class QuestBuild
    {
        const string IconPath = "Assets/_Project/Art/AppIcon.png";

        [MenuItem("Roguelike/Quest Build/Check Settings")]
        public static void Check()
        {
            var report = new StringBuilder("[Quest build] Settings check (nothing is changed):\n");
            int differences = 0;

            void Compare(bool ok, string what, string fix)
            {
                report.Append(ok ? "  OK   " : "  CHANGE  ").Append(what);
                if (!ok)
                {
                    report.Append("  ->  ").Append(fix);
                    differences++;
                }
                report.Append('\n');
            }

            Compare(PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) == ScriptingImplementation.IL2CPP,
                "Scripting backend IL2CPP", "Player > Other Settings > Scripting Backend: IL2CPP");
            Compare((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) != 0,
                "ARM64 architecture", "Player > Other Settings > Target Architectures: ARM64");
            Compare(PlayerSettings.colorSpace == ColorSpace.Linear, "Linear colour space", "Player > Other Settings > Color Space: Linear");
            Compare((int)PlayerSettings.Android.minSdkVersion >= 29, "Minimum API level 29 or higher (" + PlayerSettings.Android.minSdkVersion + ")",
                "Player > Other Settings > Minimum API Level: Android 10 (29)");
            GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            Compare(apis.Length > 0 && apis[0] == GraphicsDeviceType.Vulkan, "Vulkan first in the graphics APIs",
                "Player > Other Settings > uncheck Auto Graphics API, put Vulkan first");
            Compare(EditorUserBuildSettings.androidBuildSubtarget == MobileTextureSubtarget.ASTC, "ASTC texture compression",
                "File > Build Settings > Android > Texture Compression: ASTC");
            string id = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            Compare(!string.IsNullOrEmpty(id) && !id.Contains("DefaultCompany"), "Package name set (" + id + ")",
                "Player > Other Settings > Package Name, e.g. com.yourname.roguelike");
            Texture2D[] icons = PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Unknown);
            Compare(icons != null && icons.Length > 0 && icons[0] != null, "App icon", "Roguelike > Quest Build > Make App Icon (placeholder) or set your own");
            Compare(EnabledScenes().Length > 0, "Scenes in the build (" + EnabledScenes().Length + ")", "File > Build Settings: add MainMenu, Hub and Mission_Forest");

            report.Append(differences == 0 ? "Everything matches the recommendations." : differences + " setting(s) differ from Meta's recommendations. Change them only if the build has problems.");
            Debug.Log(report.ToString());
        }

        [MenuItem("Roguelike/Quest Build/Build APK")]
        public static void BuildApk() => Build(false);

        [MenuItem("Roguelike/Quest Build/Build And Run On Headset")]
        public static void BuildAndRun() => Build(true);

        static void Build(bool run)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                !EditorUtility.DisplayDialog("Quest build", "The active platform is not Android. Switch to Android now? Unity will re-import textures, which can take a while.", "Switch and build", "Cancel"))
                return;

            Directory.CreateDirectory("Builds");
            string path = "Builds/Roguelike-" + System.DateTime.Now.ToString("yyyyMMdd-HHmm") + ".apk";
            var options = new BuildPlayerOptions
            {
                scenes = EnabledScenes(),
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = run ? BuildOptions.AutoRunPlayer : BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
                Debug.Log("[Quest build] Built " + path + " (" + (report.summary.totalSize / (1024 * 1024)) + " MB) in " + report.summary.totalTime);
            else
                Debug.LogError("[Quest build] Build " + report.summary.result + " with " + report.summary.totalErrors + " error(s). See the Console above.");
        }

        static string[] EnabledScenes()
        {
            var scenes = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled) scenes.Add(scene.path);
            return scenes.ToArray();
        }

        /// <summary>A plain placeholder icon (dark, with a blood-red blade), used only if the project has none.</summary>
        [MenuItem("Roguelike/Quest Build/Make App Icon")]
        public static void MakeIcon()
        {
            Texture2D[] current = PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Unknown);
            if (current != null && current.Length > 0 && current[0] != null)
            {
                Debug.Log("[Quest build] The project already has an icon; left as it is.");
                return;
            }

            const int size = 512;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size - 0.5f, v = (y + 0.5f) / size - 0.5f;
                    float vignette = 1f - Mathf.Clamp01(Mathf.Sqrt(u * u + v * v) * 1.6f);
                    Color color = Color.Lerp(new Color(0.02f, 0.02f, 0.03f), new Color(0.12f, 0.1f, 0.1f), vignette);
                    // A diagonal blade.
                    float along = (u + v) * 0.7071f, across = (u - v) * 0.7071f;
                    if (Mathf.Abs(across) < 0.035f - along * 0.05f && along > -0.36f && along < 0.36f) color = new Color(0.72f, 0.08f, 0.06f);
                    if (Mathf.Abs(along + 0.24f) < 0.02f && Mathf.Abs(across) < 0.11f) color = new Color(0.55f, 0.5f, 0.45f);
                    pixels[y * size + x] = color;
                }
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(IconPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(IconPath);

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });
            Debug.Log("[Quest build] Made a placeholder icon at " + IconPath + " and set it as the app icon.");
        }
    }
}
