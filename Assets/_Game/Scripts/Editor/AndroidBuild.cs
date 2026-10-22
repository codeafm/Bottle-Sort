using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BottleSort.Editor
{
    // Тестовый APK из командной строки, без открытого редактора:
    // Unity.exe -batchmode -quit -projectPath <проект> -executeMethod BottleSort.Editor.AndroidBuild.TestApk [-apkPath <файл>]
    // Подпись — отладочная, так что поверх версии из RuStore он не встанет: старую нужно сначала удалить.
    public static class AndroidBuild
    {
        const string DefaultPath = "Builds/BottleSort-test.apk";

        [MenuItem("BottleSort/Build/Test APK")]
        public static void TestApk()
        {
            string path = ArgumentAfter("-apkPath") ?? DefaultPath;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));

            EditorUserBuildSettings.buildAppBundle = false;
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[Build] {summary.result}: {path}, {summary.totalSize / (1024 * 1024)} MB, {summary.totalTime:mm\\:ss}, errors {summary.totalErrors}");
            if (Application.isBatchMode && summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        static string ArgumentAfter(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
