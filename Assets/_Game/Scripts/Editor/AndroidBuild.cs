using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BottleSort.Editor
{
    // APK из меню BottleSort/Build или из командной строки, без открытого редактора:
    // Unity.exe -batchmode -quit -projectPath <проект> -executeMethod BottleSort.Editor.AndroidBuild.ReleaseApk [-apkPath <файл>]
    // Оба подписаны ключом из Bottle_Sort_key (AndroidSigning) и встают обновлением поверх версии из RuStore.
    public static class AndroidBuild
    {
        const string TestPath = "Builds/BottleSort-test.apk";

        [MenuItem("BottleSort/Build/Test APK")]
        public static void TestApk() => Build(ArgumentAfter("-apkPath") ?? TestPath, requireSigning: false);

        // Для загрузки в RuStore: без ключа не собирается вовсе, чтобы случайно не выложить отладочную подпись.
        [MenuItem("BottleSort/Build/Release APK (RuStore)")]
        public static void ReleaseApk()
        {
            string path = ArgumentAfter("-apkPath") ?? $"Builds/BottleSort-{PlayerSettings.bundleVersion}-{PlayerSettings.Android.bundleVersionCode}.apk";
            Build(path, requireSigning: true);
        }

        static void Build(string path, bool requireSigning)
        {
            bool signed = AndroidSigning.Apply();
            if (requireSigning && !signed)
            {
                Debug.LogError("[Build] Нет ключа в Bottle_Sort_key — release APK не собираю");
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
                return;
            }
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
            Debug.Log($"[Build] {summary.result}: {path}, {summary.totalSize / (1024 * 1024)} MB, {summary.totalTime:mm\\:ss}, errors {summary.totalErrors}, signed {(signed ? "release key" : "debug")}");
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
