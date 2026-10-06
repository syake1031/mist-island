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

        // 昼は霧を薄くして明るく、夜は霧を濃く。霧の距離はカメラ距離 34 を基準にした値
        static readonly Palette Dawn = new Palette
        {
            sun = new Color(0.86f, 0.62f, 0.5f), ambient = new Color(0.4f, 0.38f, 0.45f),
            fog = new Color(0.9f, 0.82f, 0.8f), fogStart = 50f, fogEnd = 130f, fogHeight = 1.4f, sunElevation = 22f,
        };
        static readonly Palette Morning = new Palette
        {
            sun = new Color(0.82f, 0.74f, 0.62f), ambient = new Color(0.42f, 0.43f, 0.48f),
            fog = new Color(0.82f, 0.88f, 0.92f), fogStart = 60f, fogEnd = 150f, fogHeight = 0.8f, sunElevation = 45f,
        };
        static readonly Palette Noon = new Palette
        {
            sun = new Color(0.8f, 0.77f, 0.7f), ambient = new Color(0.43f, 0.46f, 0.5f),
            fog = new Color(0.76f, 0.87f, 0.95f), fogStart = 70f, fogEnd = 170f, fogHeight = 0.6f, sunElevation = 60f,
        };
        static readonly Palette Dusk = new Palette
        {
            sun = new Color(0.86f, 0.55f, 0.45f), ambient = new Color(0.36f, 0.33f, 0.45f),
            fog = new Color(0.72f, 0.62f, 0.72f), fogStart = 45f, fogEnd = 120f, fogHeight = 1.6f, sunElevation = 18f,
        };
        static readonly Palette Night = new Palette
        {
            sun = new Color(0.36f, 0.43f, 0.64f), ambient = new Color(0.2f, 0.23f, 0.36f),
            fog = new Color(0.22f, 0.26f, 0.38f), fogStart = 24f, fogEnd = 90f, fogHeight = 2.2f, sunElevation = 40f,
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
