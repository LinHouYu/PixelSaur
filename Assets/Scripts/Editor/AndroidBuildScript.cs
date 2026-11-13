#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PixelSaur.Editor
{
    /// <summary>
    /// Android 构建自动化脚本：配置包名、架构、SDK 版本并执行 APK 导出。
    /// </summary>
    public static class AndroidBuildScript
    {
        public const string CurrentVersion = "0.1.4";
        public const string OutputApkPath = "Builds/Android/PixelSaur_v0.1.4.apk";

        [MenuItem("PixelSaur/构建 Android APK")]
        public static bool BuildAndroidApk()
        {
            string projectDir = Directory.GetCurrentDirectory();
            string outputFullPath = Path.Combine(projectDir, OutputApkPath);
            string outputDir = Path.GetDirectoryName(outputFullPath);

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // 配置 Android 播放器基础设置
            PlayerSettings.productName = "PixelSaur";
            PlayerSettings.companyName = "LinHouYu";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.LinHouYu.PixelSaur");
            PlayerSettings.bundleVersion = CurrentVersion;
            PlayerSettings.Android.bundleVersionCode = 5;

            // 确保生成独立安装的 APK（非 AAB）
            EditorUserBuildSettings.buildAppBundle = false;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            // 锁定横屏，禁止竖屏打开或旋转
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            string activeScenePath = File.Exists("Assets/Scenes/Game.unity") ? "Assets/Scenes/Game.unity" : "Assets/Scenes/SampleScene.unity";
            string[] scenes = new string[] { activeScenePath };

            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputFullPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            Debug.Log($"[AndroidBuildScript] 开始执行 Android APK 构建: {outputFullPath}...");
            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                float sizeMb = summary.totalSize / (1024f * 1024f);
                Debug.Log($"<color=#00FF00>[AndroidBuildScript] APK 构建成功！文件大小: {sizeMb:F2} MB，耗时: {summary.totalTime.TotalSeconds:F1} 秒，输出路径: {outputFullPath}</color>");
                return true;
            }
            else
            {
                Debug.LogError($"[AndroidBuildScript] APK 构建失败！结果: {summary.result}，错误数: {summary.totalErrors}");
                return false;
            }
        }
    }
}
#endif
