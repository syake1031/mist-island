using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MistIsland
{
    /// <summary>
    /// 霧の海に浮かぶ島の地形。一画面に収まる大きさで、段々の台地と崖が入り組んでいる。
    /// 高さは式で決まるので、移動や配置はメッシュを使わずに計算できる。
    /// 台地の形は絶対座標のノイズで決まり、島の拡張で変わるのは海岸近くだけ。海面の高さは 0。
    /// </summary>
    public class Island : MonoBehaviour
    {
        public const float SeaLevel = 0f;
        public const float WalkableHeight = 0.12f;
        const float BeachHeight = 0.35f;

        public static Island Instance { get; private set; }

        public float Radius { get; private set; }
        public List<SlotInfo> Slots { get; private set; }

        float _tierHeight = 1.3f;
        float _tierAmount = 3.2f;
        float _cliff = 0.3f;
        float _scale = 0.55f;
        float _plaza = 5.5f;
        float[] _phase = new float[10];
        GameObject _terrain;
        GameObject _decorations;
        Mesh _decorationMesh;
        Mesh _mesh;

        static readonly Color Sand = new Color(0.97f, 0.89f, 0.7f);
        static readonly Color WetSand = new Color(0.86f, 0.8f, 0.64f);
        static readonly Color Foam = new Color(0.96f, 0.98f, 0.98f);
        static readonly Color Grass = new Color(0.58f, 0.76f, 0.36f);
        static readonly Color LightGrass = new Color(0.7f, 0.84f, 0.42f);
        static readonly Color HighGrass = new Color(0.62f, 0.74f, 0.4f);
        static readonly Color PathColor = new Color(0.92f, 0.84f, 0.62f);
        static readonly Color Rock = new Color(0.66f, 0.65f, 0.66f);
        static readonly Color DarkRock = new Color(0.56f, 0.56f, 0.6f);
        static readonly Color SeaBed = new Color(0.4f, 0.62f, 0.66f);

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Build(GameConfig config, int expansion)
        {
            Radius = config.IslandRadius(expansion);
            _tierHeight = config.tierHeight;
            _tierAmount = config.tierAmount;
            _cliff = Mathf.Clamp(config.cliffWidth, 0.05f, 0.95f);
            _scale = Mathf.Max(0.05f, config.terrainScale);
            _plaza = Mathf.Max(2f, config.plazaRadius);
            var rng = new System.Random(config.islandSeed);
            for (int i = 0; i < _phase.Length; i++) _phase[i] = (float)(rng.NextDouble() * Mathf.PI * 2f);

            BuildTerrain();
            Slots = TownLayout.AvailableSlots(this);
            BuildDecorations(config.islandSeed);
        }

        // ---- 高さ ----

        public float CoastRadius(float angle)
        {
            float wobble = 0.09f * Mathf.Sin(3f * angle + _phase[0])
                         + 0.06f * Mathf.Sin(5f * angle + _phase[1])
                         + 0.03f * Mathf.Sin(9f * angle + _phase[2]);
            return Radius * (1f + wobble);
        }

        /// <summary>中心からの距離を海岸線で割った値。1 で海岸。</summary>
        public float NormalizedDistance(float x, float z)
        {
            float d = Mathf.Sqrt(x * x + z * z);
            return d / CoastRadius(Mathf.Atan2(z, x));
        }

        /// <summary>台地の段数（小数）。整数部が段、小数部の終わりで崖になる。</summary>
        float TierField(float x, float z, float d, float coast)
        {
            float X = x * _scale, Z = z * _scale;
            float n = 0.5f
                      + 0.30f * Mathf.Sin(X * 0.21f + _phase[3]) * Mathf.Cos(Z * 0.19f + _phase[4])
                      + 0.20f * Mathf.Sin((X - Z) * 0.33f + _phase[5])
                      + 0.12f * Mathf.Cos(X * 0.55f + _phase[6]) * Mathf.Sin(Z * 0.6f + _phase[7]);
            float f = _tierAmount * n + 0.6f;
            // 拠点のまわりは1段目の平らな広場にする
            f = Mathf.Lerp(1.5f, f, Smooth(_plaza - 2.5f, _plaza + 1f, d));
            // 海岸に向かって段を下げていく
            float mask = 1f - Smooth(coast - 4.5f, coast - 0.3f, d);
            return Mathf.Max(0f, f * mask);
        }

        /// <summary>拠点から海へ向かう砂の小道（0..1）。</summary>
        float PathAmount(float x, float z)
        {
            float d = Mathf.Sqrt(x * x + z * z);
            float a = Mathf.Atan2(z, x);
            float amount = 0f;
            // 広場を囲む輪
            amount = Mathf.Max(amount, 1f - Mathf.Clamp01(Mathf.Abs(d - _plaza * 0.75f) / 0.6f));
            // 放射状に3本
            for (int i = 0; i < 3; i++)
            {
                float dir = _phase[i] + i * Mathf.PI * 2f / 3f + 0.25f * Mathf.Sin(d * 0.35f + _phase[i + 3]);
                float diff = Mathf.DeltaAngle(a * Mathf.Rad2Deg, dir * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                float side = Mathf.Abs(Mathf.Sin(diff)) * d;
                if (Mathf.Cos(diff) < 0f || d < _plaza * 0.75f) continue;
                amount = Mathf.Max(amount, 1f - Mathf.Clamp01((side - 0.4f) / 0.5f));
            }
            return amount;
        }

        public float HeightAt(float x, float z)
        {
            float d = Mathf.Sqrt(x * x + z * z);
            float coast = CoastRadius(Mathf.Atan2(z, x));
            float f = TierField(x, z, d, coast);
            float k = Mathf.Floor(f);
            float step = Smooth(1f - _cliff, 1f, f - k);
            float h = BeachHeight + (k + step) * _tierHeight;
            // 砂浜から海へ
            h -= 0.6f * Smooth(coast - 0.3f, coast + 0.6f, d);
            h -= 1.6f * Smooth(coast + 0.6f, coast + 3.5f, d);
            return h;
        }

        public float HeightAt(Vector3 p)
        {
            return HeightAt(p.x, p.z);
        }

        /// <summary>そこの傾き（高さの変化 / 距離）。</summary>
        public float SlopeAt(float x, float z, float e = 0.6f)
        {
            float sx = Mathf.Abs(HeightAt(x + e, z) - HeightAt(x - e, z));
            float sz = Mathf.Abs(HeightAt(x, z + e) - HeightAt(x, z - e));
            return Mathf.Max(sx, sz) / (2f * e);
        }

        public bool IsWalkable(float x, float z)
        {
            return HeightAt(x, z) > WalkableHeight;
        }

        /// <summary>地面の高さ（海の上なら海面）。</summary>
        public float SurfaceAt(float x, float z)
        {
            return Mathf.Max(HeightAt(x, z), SeaLevel - 0.35f);
        }

        /// <summary>沖の地点。敵の出現位置に使う。</summary>
        public Vector3 OffshorePoint(float angle, float extra)
        {
            float r = CoastRadius(angle) + 3f + extra;
            return new Vector3(Mathf.Cos(angle) * r, SeaLevel - 0.35f, Mathf.Sin(angle) * r);
        }

        static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        // ---- メッシュ ----

        void BuildTerrain()
        {
            float extent = Radius * 1.2f + 6f;
            const float step = 0.35f;
            int n = Mathf.CeilToInt(extent * 2f / step) + 1;

            var verts = new List<Vector3>(n * n);
            var colors = new List<Color>(n * n);
            var tris = new List<int>((n - 1) * (n - 1) * 6);

            for (int iz = 0; iz < n; iz++)
            {
                for (int ix = 0; ix < n; ix++)
                {
                    float x = -extent + ix * step;
                    float z = -extent + iz * step;
                    float h = HeightAt(x, z);
                    verts.Add(new Vector3(x, h, z));
                    colors.Add(GroundColor(x, z, h));
                }
            }

            for (int iz = 0; iz < n - 1; iz++)
            {
                for (int ix = 0; ix < n - 1; ix++)
                {
                    int i = iz * n + ix;
                    tris.Add(i); tris.Add(i + n); tris.Add(i + 1);
                    tris.Add(i + 1); tris.Add(i + n); tris.Add(i + n + 1);
                }
            }

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "IslandTerrain" };
                _mesh.indexFormat = IndexFormat.UInt32;
            }
            _mesh.Clear();
            _mesh.SetVertices(verts);
            _mesh.SetColors(colors);
            _mesh.SetTriangles(tris, 0);
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();

            if (_terrain == null)
                _terrain = Shapes.FromMesh(_mesh, transform, Shapes.VertexColorMaterial, "Terrain");
        }

        public Color GroundColorAt(float x, float z)
        {
            return GroundColor(x, z, HeightAt(x, z));
        }

        Color GroundColor(float x, float z, float h)
        {
            // 波打ち際は白く
            if (h > -0.2f && h < 0.08f) return Foam;
            if (h < -0.05f) return Color.Lerp(WetSand, SeaBed, Mathf.Clamp01(-h / 1.2f));
            if (h < BeachHeight + 0.1f) return Color.Lerp(WetSand, Sand, Mathf.Clamp01(h / 0.3f));

            // 崖は岩、台地の上は草。段ごとに少し色を変える
            float slope = SlopeAt(x, z, 0.25f);
            int tier = Mathf.RoundToInt((h - BeachHeight) / _tierHeight);
            float tint = 0.5f + 0.5f * Mathf.Sin(x * 0.35f + _phase[8]) * Mathf.Sin(z * 0.3f + _phase[9]);
            Color grass = Color.Lerp(Grass, LightGrass, tint);
            if (tier >= 3) grass = Color.Lerp(grass, HighGrass, 0.6f);
            float grassMix = Mathf.Clamp01((h - BeachHeight - 0.1f) / 0.4f);
            Color c = Color.Lerp(Sand, grass, grassMix);
            if (slope < 0.3f) c = Color.Lerp(c, PathColor, PathAmount(x, z) * grassMix);
            Color rock = (tier % 2 == 0) ? Rock : DarkRock;
            return Color.Lerp(c, rock, Mathf.Clamp01((slope - 0.5f) * 2f));
        }

        // ---- 木と岩 ----

        void BuildDecorations(int seed)
        {
            if (_decorations != null) Destroy(_decorations);
            if (_decorationMesh != null) Destroy(_decorationMesh);
            _decorations = new GameObject("Decorations");
            _decorations.transform.SetParent(transform, false);

            var rng = new System.Random(seed * 31 + 1);
            int attempts = Mathf.Min(700, Mathf.RoundToInt(Radius * Radius * 1.2f));
            var trunk = new Color(0.62f, 0.48f, 0.36f);
            var frondDark = new Color(0.36f, 0.62f, 0.3f);
            var frondLight = new Color(0.48f, 0.74f, 0.36f);
            var bush = new Color(0.44f, 0.66f, 0.34f);
            Mesh sphere = Shapes.PrimitiveMesh(PrimitiveType.Sphere);
            Mesh cylinder = Shapes.PrimitiveMesh(PrimitiveType.Cylinder);
            Mesh cone = Shapes.ConeMesh;

            // ヤシの木・茂み・岩・花は1つのメッシュにまとめて、描画回数を減らす（モバイル向け）
            var batch = new MeshBatch();
            for (int i = 0; i < attempts; i++)
            {
                float x = (float)(rng.NextDouble() * 2 - 1) * Radius;
                float z = (float)(rng.NextDouble() * 2 - 1) * Radius;
                float t = NormalizedDistance(x, z);
                float d = Mathf.Sqrt(x * x + z * z);
                if (t > 0.92f || d < _plaza) continue;
                if (TownLayout.IsNearSlot(Slots, new Vector3(x, 0, z), 3.2f)) continue;
                float h = HeightAt(x, z);
                if (h < 0.3f || SlopeAt(x, z) > 0.3f || PathAmount(x, z) > 0.3f) continue;
                if (rng.NextDouble() < 0.4) continue; // まばらにする

                var p = new Vector3(x, h, z);
                Color ground = GroundColor(x, z, h);
                Quaternion yaw = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                double kind = rng.NextDouble();
                if (kind < 0.3)
                {
                    AddPalm(batch, cylinder, cone, p, yaw, 0.9f + (float)rng.NextDouble() * 0.5f, trunk,
                        Color.Lerp(frondDark, frondLight, (float)rng.NextDouble()), ground, rng);
                }
                else if (kind < 0.6)
                {
                    float s = 0.6f + (float)rng.NextDouble() * 0.5f;
                    AddShadow(batch, cylinder, p, s * 1.1f, ground);
                    batch.Add(sphere, Matrix4x4.TRS(p + new Vector3(0, s * 0.3f, 0), yaw, new Vector3(s, s * 0.8f, s)), bush);
                    batch.Add(sphere, Matrix4x4.TRS(p + yaw * new Vector3(s * 0.55f, s * 0.2f, 0.1f), yaw, Vector3.one * s * 0.7f), bush * 1.08f);
                    batch.Add(sphere, Matrix4x4.TRS(p + yaw * new Vector3(-s * 0.45f, s * 0.15f, -0.2f), yaw, Vector3.one * s * 0.6f), bush * 0.94f);
                }
                else if (kind < 0.72)
                {
                    float s = 0.5f + (float)rng.NextDouble() * 0.6f;
                    AddShadow(batch, cylinder, p, s * 0.9f, ground);
                    batch.Add(sphere, Matrix4x4.TRS(p, yaw, new Vector3(s * 1.3f, s * 0.8f, s)), Rock);
                }
                else
                {
                    // 小さな花
                    Color petal = rng.NextDouble() < 0.6 ? new Color(0.98f, 0.98f, 0.95f) : new Color(1f, 0.85f, 0.4f);
                    for (int k = 0; k < 3; k++)
                    {
                        var o = new Vector3((float)(rng.NextDouble() - 0.5), 0.08f, (float)(rng.NextDouble() - 0.5));
                        batch.Add(sphere, Matrix4x4.TRS(p + o, Quaternion.identity, Vector3.one * 0.14f), petal);
                    }
                }
            }
            _decorationMesh = batch.Build("Decorations");
            Shapes.FromMesh(_decorationMesh, _decorations.transform, Shapes.VertexColorMaterial, "DecorationMesh");
        }

        /// <summary>地面に落ちる影（地面の色を暗くした円）。</summary>
        static void AddShadow(MeshBatch batch, Mesh cylinder, Vector3 p, float radius, Color ground)
        {
            batch.Add(cylinder, Matrix4x4.TRS(p + new Vector3(0.25f, 0.02f, -0.2f), Quaternion.identity, new Vector3(radius * 2f, 0.01f, radius * 1.6f)), ground * 0.8f);
        }

        static void AddPalm(MeshBatch batch, Mesh cylinder, Mesh cone, Vector3 p, Quaternion yaw, float s, Color trunk, Color frond, Color ground, System.Random rng)
        {
            AddShadow(batch, cylinder, p + yaw * new Vector3(0.6f * s, 0f, 0f), 1.4f * s, ground);
            // 少し傾いた幹を節ごとに積む
            Vector3 lean = yaw * new Vector3(0.12f, 0f, 0f);
            Vector3 top = p;
            for (int i = 0; i < 6; i++)
            {
                Vector3 c = p + Vector3.up * (0.35f + i * 0.55f) * s + lean * (i * i) * s;
                float w = (0.26f - i * 0.02f) * s;
                batch.Add(cylinder, Matrix4x4.TRS(c, Quaternion.identity, new Vector3(w, 0.3f * s, w)), i % 2 == 0 ? trunk : trunk * 0.9f);
                top = c + Vector3.up * 0.3f * s;
            }
            // 葉を放射状に垂らす
            int fronds = 7;
            float start = (float)rng.NextDouble() * 360f;
            for (int i = 0; i < fronds; i++)
            {
                Quaternion r = Quaternion.Euler(0f, start + i * 360f / fronds, 0f) * Quaternion.Euler(105f, 0f, 0f);
                batch.Add(cone, Matrix4x4.TRS(top, r, new Vector3(0.6f * s, 1.9f * s, 0.12f * s)), i % 2 == 0 ? frond : frond * 0.92f);
            }
        }
    }
}
