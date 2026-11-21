using System.IO;
using Plop.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Plop.EditorTools
{
    /// <summary>Creates/updates the prototype gameplay scene. Batchmode: -executeMethod Plop.EditorTools.PlopSceneSetup.CreateGameplayScene</summary>
    public static class PlopSceneSetup
    {
        public const string GameplayScene = "Assets/Scenes/Gameplay.unity";
        private const string TemplateScene = "Assets/Scenes/Boot.unity"; // URP 2D camera + Global Light 2D

        [MenuItem("PLOP/Play Game", false, -100)]
        public static void PlayGame()
        {
            if (SceneManager.GetActiveScene().path != GameplayScene)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(GameplayScene, OpenSceneMode.Single);
            }
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("PLOP/Play Game", true)]
        private static bool CanPlayGame()
            => !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(GameplayScene);

        [MenuItem("PLOP/Setup/Create Gameplay Scene")]
        public static void CreateGameplayScene()
        {
            if (!File.Exists(GameplayScene))
            {
                if (!AssetDatabase.CopyAsset(TemplateScene, GameplayScene))
                {
                    var fresh = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                    EditorSceneManager.SaveScene(fresh, GameplayScene);
                }
            }
            var scene = EditorSceneManager.OpenScene(GameplayScene, OpenSceneMode.Single);
            if (Object.FindAnyObjectByType<PlopGameController>() == null)
            {
                var go = new GameObject("PLOP Game");
                go.AddComponent<PlopGameController>();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(GameplayScene, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[PLOP] Gameplay scene ready: " + GameplayScene);
        }
    }
}
