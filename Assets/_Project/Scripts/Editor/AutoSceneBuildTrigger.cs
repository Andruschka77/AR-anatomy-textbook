#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnatomyAR.EditorScripts
{
    [InitializeOnLoad]
    public static class AutoSceneBuildTrigger
    {
        private const string PrefKey = "AR_Anatomy_SceneBuilt_v14";
        public const string OutputApkPath = @"C:\Users\kulik\Desktop\Курсовая\Unity_WebGL_Deploy\AR-anatomy-textbook.apk";

        static AutoSceneBuildTrigger()
        {
            EditorApplication.delayCall += ExecuteSetupOnce;
        }

        private static void ExecuteSetupOnce()
        {
            if (EditorPrefs.GetBool(PrefKey, false))
            {
                return;
            }

            Debug.Log("[AutoBuild] >>> Выполняется автоматическая настройка сцены и 13 органов...");
            try
            {
                AnatomySceneBuilder.BuildCompleteProjectAndScene();
                EditorPrefs.SetBool(PrefKey, true);
                Debug.Log("[AutoBuild] >>> Сцена и 13 префабов органов успешно обновлены!");
                BuildAndroidAPK();
            }
            catch (Exception ex)
            {
                Debug.LogError("[AutoBuild] Ошибка при автогенерации сцены: " + ex);
            }
        }

        [MenuItem("Anatomy AR/Build Android APK", false, 2)]
        public static void BuildAndroidAPK()
        {
            Debug.Log("[AutoBuild] >>> Запуск сборки Android APK...");
            try
            {
                // Принудительно генерируем и обновляем сцену, материалы, UI и все 13 префабов
                AnatomySceneBuilder.BuildCompleteProjectAndScene();
                EditorSceneManager.SaveOpenScenes();
                AssetDatabase.SaveAssets();

                string dir = Path.GetDirectoryName(OutputApkPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/_Project/Scenes/AR_Anatomy_MainScene.unity" },
                    locationPathName = OutputApkPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.None
                };

                var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
                var summary = report.summary;

                if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
                {
                    Debug.Log($"[AutoBuild] >>> УСПЕХ! Android APK собран: {OutputApkPath} ({(summary.totalSize / (1024f * 1024f)):F1} MB)");
                    EditorUtility.RevealInFinder(OutputApkPath);
                }
                else
                {
                    Debug.LogError($"[AutoBuild] Ошибка сборки APK: {summary.result}, ошибок: {summary.totalErrors}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[AutoBuild] Исключение при сборке APK: " + ex);
            }
        }
    }
}
#endif
