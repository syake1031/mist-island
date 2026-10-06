using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MistIsland.EditorTools
{
    /// <summary>
    /// メニュー「MistIsland」からプロジェクトの準備をする。
    /// 設定ファイル（GameConfig）・ゲーム用シーン・縦画面などをまとめて作る。
    /// </summary>
    public static class MistIslandSetup
    {
        const string Root = "Assets/MistIsland";
        const string ConfigPath = Root + "/Resources/MistIsland/GameConfig.asset";
        const string ScenePath = Root + "/Scenes/Main.unity";

        [MenuItem("MistIsland/セットアップ（設定・シーン・縦画面）", priority = 0)]
        public static void SetupAll()
        {
            CreateConfig();
            ConfigurePlayer();
            CreateScene();
            EditorUtility.DisplayDialog("MistIsland",
                "準備ができました。\n\n" +
                "・数値の調整：" + ConfigPath + "\n" +
                "・シーン：" + ScenePath + "（ビルド設定の先頭に登録済み）\n\n" +
                "Play を押すと島が生成されます。", "OK");
        }

        [MenuItem("MistIsland/設定ファイル（GameConfig）を選択", priority = 20)]
        public static void SelectConfig()
        {
            var config = CreateConfig();
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }

        [MenuItem("MistIsland/セーブデータを削除", priority = 40)]
        public static void DeleteSave()
        {
            if (!EditorUtility.DisplayDialog("MistIsland", "セーブデータを削除しますか？\n" + SaveSystem.FilePath, "削除", "キャンセル")) return;
            SaveSystem.Delete();
            Debug.Log("[MistIsland] セーブデータを削除しました");
        }

        static GameConfig CreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config != null) return config;
            EnsureFolder(Path.GetDirectoryName(ConfigPath));
            config = ScriptableObject.CreateInstance<GameConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[MistIsland] GameConfig を作成しました: " + ConfigPath);
            return config;
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            if (PlayerSettings.productName == "My project" || string.IsNullOrEmpty(PlayerSettings.productName))
                PlayerSettings.productName = "霧の島の守り手";
        }

        static void CreateScene()
        {
            if (!File.Exists(ScenePath))
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EnsureFolder(Path.GetDirectoryName(ScenePath));
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            else if (SceneManager.GetActiveScene().path != ScenePath)
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(ScenePath);
            }

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
