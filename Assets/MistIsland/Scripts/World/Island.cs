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
        float[] _phase = new float[10];
        GameObject _terrain;
        GameObject _decorations;
        Mesh _decorationMesh;
        Mesh _mesh;

        static readonly Color Sand = new Color(0.93f, 0.87f, 0.72f);
        static readonly Color WetSand = new Color(0.78f, 0.76f, 0.66f);
        static readonly Color Grass = new Color(0.6f, 0.76f, 0.54f);
        static readonly Color LightGrass = new Color(0.72f, 0.84f, 0.6f);
        static readonly Color HighGrass = new Color(0.66f, 0.74f, 0.58f);
        static readonly Color Rock = new Color(0.66f, 0.64f, 0.66f);
        static readonly Color DarkRock = new Color(0.55f, 0.54f, 0.58f);
        static readonly Color SeaBed = new Color(0.5f, 0.62f, 0.62f);

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
            float n = 0.5f
                      + 0.30f * Mathf.Sin(x * 0.21f + _phase[3]) * Mathf.Cos(z * 0.19f + _phase[4])
                      + 0.20f * Mathf.Sin((x - z) * 0.33f + _phase[5])
                      + 0.12f * Mathf.Cos(x * 0.55f + _phase[6]) * Mathf.Sin(z * 0.6f + _phase[7]);
            float f = _tierAmount * n + 0.6f;
            // 拠点のまわりは1段目で平らにする
            f = Mathf.Lerp(1.5f, f, Smooth(2.5f, 5.5f, d));
            // 海岸に向かって段を下げていく
            float mask = 1f - Smooth(coast - 3.5f, coast - 0.3f, d);
            return Mathf.Max(0f, f * mask);
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

        Color GroundColor(float x, float z, float h)
        {
            if (h < -0.05f) return Color.Lerp(WetSand, SeaBed, Mathf.Clamp01(-h / 1.2f));
            if (h < BeachHeight + 0.1f) return Color.Lerp(WetSand, Sand, Mathf.Clamp01(h / 0.3f));

            // 崖は岩、台地の上は草。段ごとに少し色を変える
            float slope = SlopeAt(x, z, 0.25f);
            int tier = Mathf.RoundToInt((h - BeachHeight) / _tierHeight);
            float tint = 0.5f + 0.5f * Mathf.Sin(x * 0.5f + _phase[8]) * Mathf.Sin(z * 0.45f + _phase[9]);
            Color grass = Color.Lerp(Grass, LightGrass, tint);
            if (tier >= 3) grass = Color.Lerp(grass, HighGrass, 0.6f);
            float grassMix = Mathf.Clamp01((h - BeachHeight - 0.1f) / 0.4f);
            Color c = Color.Lerp(Sand, grass, grassMix);
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
            int attempts = Mathf.Min(500, Mathf.RoundToInt(Radius * Radius * 1.2f));
            var pineDark = new Color(0.4f, 0.58f, 0.46f);
            var pineLight = new Color(0.5f, 0.68f, 0.5f);
            var trunk = new Color(0.55f, 0.45f, 0.38f);
            Mesh sphere = Shapes.PrimitiveMesh(PrimitiveType.Sphere);
            Mesh cylinder = Shapes.PrimitiveMesh(PrimitiveType.Cylinder);
            Mesh cone = Shapes.ConeMesh;

            // 木や岩は1つのメッシュにまとめて、描画回数を減らす（モバイル向け）
            var batch = new MeshBatch();
            for (int i = 0; i < attempts; i++)
            {
                float x = (float)(rng.NextDouble() * 2 - 1) * Radius;
                float z = (float)(rng.NextDouble() * 2 - 1) * Radius;
                float t = NormalizedDistance(x, z);
                float d = Mathf.Sqrt(x * x + z * z);
                if (t > 0.85f || d < 4.5f) continue;
                if (TownLayout.IsNearSlot(Slots, new Vector3(x, 0, z), 2f)) continue;
                float h = HeightAt(x, z);
                if (h < 0.6f || SlopeAt(x, z) > 0.4f) continue;
                if (rng.NextDouble() < 0.45) continue; // まばらにする

                bool isRock = rng.NextDouble() < 0.22;
                var p = new Vector3(x, h, z);
                Quaternion yaw = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                if (isRock)
                {
                    float s = 0.4f + (float)rng.NextDouble() * 0.5f;
                    batch.Add(sphere, Matrix4x4.TRS(p, yaw, new Vector3(s * 1.3f, s * 0.7f, s)), Rock);
                }
                else
                {
                    float s = 0.6f + (float)rng.NextDouble() * 0.5f;
                    Matrix4x4 tree = Matrix4x4.TRS(p, yaw, Vector3.one * s);
                    Color leaf = Color.Lerp(pineDark, pineLight, (float)rng.NextDouble());
                    batch.Add(cylinder, tree * Matrix4x4.TRS(new Vector3(0, 0.25f, 0), Quaternion.identity, new Vector3(0.18f, 0.25f, 0.18f)), trunk);
                    batch.Add(cone, tree * Matrix4x4.TRS(new Vector3(0, 0.4f, 0), Quaternion.identity, new Vector3(1.2f, 1.3f, 1.2f)), leaf);
                    batch.Add(cone, tree * Matrix4x4.TRS(new Vector3(0, 1.1f, 0), Quaternion.identity, new Vector3(0.85f, 1.1f, 0.85f)), leaf);
                }
            }
            _decorationMesh = batch.Build("Decorations");
            Shapes.FromMesh(_decorationMesh, _decorations.transform, Shapes.VertexColorMaterial, "DecorationMesh");
        }
    }
}
