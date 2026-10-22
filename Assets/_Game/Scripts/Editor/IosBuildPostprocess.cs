#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace BottleSort.Editor
{
    // iOS-сборка: текст вопроса о трекинге (без него App Store отклонит сборку, а iOS уронит приложение
    // на запросе) и фреймворк AppTrackingTransparency для Plugins/iOS/BSTrackingConsent.mm.
    // SKAdNetwork для рекламы добавляет плагин Yandex из Assets/Editor/YandexAds/skad_ids.xml.
    static class IosBuildPostprocess
    {
        const string TrackingQuestion =
            "Разрешение нужно, чтобы показывать вам более подходящую рекламу. Реклама помогает игре оставаться бесплатной.";

        [PostProcessBuild(100)]
        static void OnPostprocessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS)
                return;

            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetString("NSUserTrackingUsageDescription", TrackingQuestion);
            plist.WriteToFile(plistPath);

            string projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "AppTrackingTransparency.framework", true);
            project.WriteToFile(projectPath);
        }
    }
}
#endif
