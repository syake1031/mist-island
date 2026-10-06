using System.Collections;
using UnityEngine;

namespace MistIsland
{
    /// <summary>
    /// ゲームを組み立てる入口。シーンに置いておくか、置いていなければ Play 時に自動で作られる
    /// （GameConfig.autoBootstrap）。島・海・町・プレイヤー・UI はすべてコードで生成する。
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        static GameBootstrap _instance;
        GameObject _root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            if (!GameConfig.Load().autoBootstrap) return;
            if (FindAnyObjectByType<GameBootstrap>() != null) return;
            new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.Portrait;
            Build();
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void Build()
        {
            GameConfig config = GameConfig.Load();
            _root = new GameObject("MistIsland");
            Camera cam = PrepareCamera();
            _root.AddComponent<GameManager>().Initialize(config, cam);
        }

        Camera PrepareCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                go.transform.SetParent(_root.transform, false);
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            return cam;
        }

        /// <summary>ゲームを作り直す（セーブを消したあとなど）。</summary>
        public static void Restart()
        {
            if (_instance != null) _instance.StartCoroutine(_instance.RestartRoutine());
        }

        IEnumerator RestartRoutine()
        {
            if (_root != null) Destroy(_root);
            _root = null;
            foreach (var e in Enemy.All.ToArray())
                if (e != null) Destroy(e.gameObject);
            foreach (var p in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                Destroy(p.gameObject);
            // Destroy は次のフレームで反映されるので1フレーム待つ
            yield return null;
            Build();
        }
    }
}
