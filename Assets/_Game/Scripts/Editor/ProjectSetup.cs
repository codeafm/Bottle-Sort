using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BottleSort.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BottleSort.Editor
{
    // Можно запускать повторно: готовые ассеты и сцены не пересоздаются.
    // Из batchmode: -executeMethod BottleSort.Editor.ProjectSetup.Run
    public static class ProjectSetup
    {
        public const string BundleId = "com.codeafm.bottlesort";
        const string SettingsDir = "Assets/_Game/Settings";
        const string RendererPath = SettingsDir + "/Renderer2D.asset";
        const string PipelinePath = SettingsDir + "/URP_Mobile.asset";
        const string ScenesDir = "Assets/_Game/Scenes";
        // Порядок = Build Settings: заставка, меню, игра. Интерфейс в них собирает UiSceneBuilder.
        static readonly string[] ScenePaths =
        {
            ScenesDir + "/Splash.unity",
            ScenesDir + "/Menu.unity",
            ScenesDir + "/Game.unity"
        };

        static readonly Color BackgroundColor = new Color(0.06f, 0.07f, 0.12f, 1f);

        [MenuItem("BottleSort/Setup Project")]
        public static void Run()
        {
            SetupRenderPipeline();
            SetupPlayerSettings();
            SetupScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] Done");
        }

        static void SetupRenderPipeline()
        {
            Directory.CreateDirectory(SettingsDir);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath)
                               ?? Create2DRendererData(RendererPath);
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.supportsHDR = false;
                pipeline.msaaSampleCount = 1;
                pipeline.renderScale = 1f;
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            var current = QualitySettings.GetQualityLevel();
            for (var i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
                QualitySettings.vSyncCount = 0;
            }
            QualitySettings.SetQualityLevel(current, false);

            // URP кладёт их в корень Assets
            MoveIfExists("Assets/UniversalRenderPipelineGlobalSettings.asset", SettingsDir + "/UniversalRenderPipelineGlobalSettings.asset");
            MoveIfExists("Assets/DefaultVolumeProfile.asset", SettingsDir + "/DefaultVolumeProfile.asset");
        }

        static void MoveIfExists(string from, string to)
        {
            if (File.Exists(from) && !File.Exists(to))
            {
                var error = AssetDatabase.MoveAsset(from, to);
                if (!string.IsNullOrEmpty(error))
                    Debug.LogWarning($"[ProjectSetup] Move {from}: {error}");
            }
        }

        // В URP это internal, поэтому через reflection - то же, что делает меню Create > Rendering > URP 2D Renderer.
        static ScriptableRendererData Create2DRendererData(string path)
        {
            var menus = typeof(UniversalRenderPipelineAsset).Assembly.GetType("UnityEditor.Rendering.Universal.Renderer2DMenus")
                        ?? AppDomain.CurrentDomain.GetAssemblies()
                            .Select(a => a.GetType("UnityEditor.Rendering.Universal.Renderer2DMenus"))
                            .FirstOrDefault(t => t != null);
            var method = menus?.GetMethod("CreateRendererAsset", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (method == null)
                throw new InvalidOperationException("URP: Renderer2DMenus.CreateRendererAsset not found");

            var rendererType = method.GetParameters()[1].ParameterType;
            var type2D = Enum.Parse(rendererType, "_2DRenderer");
            return (ScriptableRendererData)method.Invoke(null, new object[] { path, type2D, false, "Renderer" });
        }

        static void SetupPlayerSettings()
        {
            PlayerSettings.companyName = "BottleSort";
            PlayerSettings.productName = "BottleSort";

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25; // 24 в Unity 6.3 уже не поддерживается
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            // Рисуем под вырезом, отступы считает SafeAreaFitter
            PlayerSettings.Android.renderOutsideSafeArea = true;

            PlayerSettings.iOS.targetOSVersionString = "15.0"; // ниже 15 Unity 6.3 не даёт
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.requiresFullScreen = true;

            // 1 = только новый Input System
            var playerSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")
                .FirstOrDefault(o => o is PlayerSettings);
            if (playerSettings != null)
            {
                var so = new SerializedObject(playerSettings);
                var input = so.FindProperty("activeInputHandler");
                if (input != null && input.intValue != 1)
                {
                    input.intValue = 1;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        static void SetupScenes()
        {
            Directory.CreateDirectory(ScenesDir);

            foreach (string path in ScenePaths)
                if (!File.Exists(path))
                    CreateUiScene(path);

            EditorBuildSettings.scenes = ScenePaths.Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
        }

        static void CreateUiScene(string path)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();

            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var root = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaFitter));
            root.layer = canvasGo.layer;
            var rect = (RectTransform)root.transform;
            rect.SetParent(canvasGo.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            EditorSceneManager.SaveScene(scene, path);
        }

        static void CreateCamera()
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(UniversalAdditionalCameraData));
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0, 0, -10);
            var cam = go.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 9.6f; // 1080x1920 при 100 px/unit
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BackgroundColor;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
        }
    }
}
