using UnityEngine;

namespace MistIsland
{
    /// <summary>
    /// 時間帯に合わせて光・霧・空の色を変える。Bad North のような淡い色合いと霧が目標。
    /// 値はシェーダーのグローバル変数として渡す（MistLit.shader 参照）。
    /// </summary>
    public class MistVisuals : MonoBehaviour
    {
        struct Palette
        {
            public Color sun;
            public Color ambient;
            public Color fog;
            public float fogStart;
            public float fogEnd;
            public float fogHeight;
            public float sunElevation;

            public static Palette Lerp(Palette a, Palette b, float t)
            {
                return new Palette
                {
                    sun = Color.Lerp(a.sun, b.sun, t),
                    ambient = Color.Lerp(a.ambient, b.ambient, t),
                    fog = Color.Lerp(a.fog, b.fog, t),
                    fogStart = Mathf.Lerp(a.fogStart, b.fogStart, t),
                    fogEnd = Mathf.Lerp(a.fogEnd, b.fogEnd, t),
                    fogHeight = Mathf.Lerp(a.fogHeight, b.fogHeight, t),
                    sunElevation = Mathf.Lerp(a.sunElevation, b.sunElevation, t),
                };
            }
        }

        static readonly Palette Dawn = new Palette
        {
            sun = new Color(0.95f, 0.72f, 0.6f), ambient = new Color(0.55f, 0.52f, 0.6f),
            fog = new Color(0.86f, 0.8f, 0.8f), fogStart = 28f, fogEnd = 75f, fogHeight = 2.2f, sunElevation = 18f,
        };
        static readonly Palette Morning = new Palette
        {
            sun = new Color(0.92f, 0.86f, 0.76f), ambient = new Color(0.58f, 0.6f, 0.64f),
            fog = new Color(0.84f, 0.87f, 0.88f), fogStart = 30f, fogEnd = 85f, fogHeight = 1.6f, sunElevation = 40f,
        };
        static readonly Palette Noon = new Palette
        {
            sun = new Color(0.9f, 0.9f, 0.86f), ambient = new Color(0.62f, 0.66f, 0.7f),
            fog = new Color(0.8f, 0.86f, 0.9f), fogStart = 32f, fogEnd = 95f, fogHeight = 1.2f, sunElevation = 60f,
        };
        static readonly Palette Dusk = new Palette
        {
            sun = new Color(0.95f, 0.6f, 0.5f), ambient = new Color(0.45f, 0.42f, 0.55f),
            fog = new Color(0.7f, 0.62f, 0.72f), fogStart = 28f, fogEnd = 75f, fogHeight = 2f, sunElevation = 15f,
        };
        static readonly Palette Night = new Palette
        {
            sun = new Color(0.45f, 0.52f, 0.75f), ambient = new Color(0.22f, 0.25f, 0.38f),
            fog = new Color(0.24f, 0.28f, 0.4f), fogStart = 26f, fogEnd = 65f, fogHeight = 2.6f, sunElevation = 35f,
        };

        // (時間帯, その時間帯の中での位置 0..1, 色) のキー
        struct Key
        {
            public Phase phase;
            public float at;
            public Palette palette;
            public Key(Phase phase, float at, Palette palette) { this.phase = phase; this.at = at; this.palette = palette; }
        }

        static readonly Key[] Keys =
        {
            new Key(Phase.Morning, 0f, Dawn),
            new Key(Phase.Morning, 0.5f, Morning),
            new Key(Phase.Day, 0.3f, Noon),
            new Key(Phase.Day, 0.85f, Noon),
            new Key(Phase.Night, 0f, Dusk),
            new Key(Phase.Night, 0.2f, Night),
            new Key(Phase.Night, 0.9f, Night),
        };

        // パレットの霧の距離はカメラ距離 34 を基準にしている
        const float BaseCameraDistance = 34f;

        DayCycle _clock;
        Camera _camera;
        CameraRig _rig;
        float _yawOffset = 35f;

        public void Initialize(DayCycle clock, Camera cam, CameraRig rig)
        {
            _clock = clock;
            _camera = cam;
            _rig = rig;
            Apply();
        }

        void LateUpdate()
        {
            Apply();
        }

        void Apply()
        {
            Palette p = _clock != null ? Evaluate(_clock.Durations, _clock.Time) : Morning;

            float elev = p.sunElevation * Mathf.Deg2Rad;
            float yaw = _yawOffset * Mathf.Deg2Rad;
            var sunDir = new Vector3(Mathf.Cos(elev) * Mathf.Cos(yaw), Mathf.Sin(elev), Mathf.Cos(elev) * Mathf.Sin(yaw));

            Shader.SetGlobalVector("_MI_SunDir", sunDir);
            Shader.SetGlobalColor("_MI_SunColor", p.sun);
            Shader.SetGlobalColor("_MI_Ambient", p.ambient);
            Shader.SetGlobalColor("_MI_FogColor", p.fog);
            float offset = _rig != null ? _rig.distance - BaseCameraDistance : 0f;
            Shader.SetGlobalFloat("_MI_FogStart", p.fogStart + offset);
            Shader.SetGlobalFloat("_MI_FogEnd", p.fogEnd + offset);
            Shader.SetGlobalFloat("_MI_FogHeight", p.fogHeight);
            Shader.SetGlobalFloat("_MI_Time", Time.time);

            if (_camera != null)
            {
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = p.fog;
            }
        }

        static Palette Evaluate(DayDurations d, float time)
        {
            float total = d.Total;
            int n = Keys.Length;
            // キーの絶対時刻を求めて、time を挟む2つを補間する（1日の終わりで先頭に戻る）
            for (int i = 0; i < n; i++)
            {
                Key a = Keys[i];
                Key b = Keys[(i + 1) % n];
                float ta = d.Start(a.phase) + d.Length(a.phase) * a.at;
                float tb = d.Start(b.phase) + d.Length(b.phase) * b.at;
                if (i == n - 1) tb += total;
                float t = time;
                if (i == n - 1 && t < ta) t += total;
                if (t >= ta && t <= tb)
                {
                    float u = tb > ta ? (t - ta) / (tb - ta) : 0f;
                    return Palette.Lerp(a.palette, b.palette, Mathf.SmoothStep(0f, 1f, u));
                }
            }
            return Morning;
        }
    }
}
