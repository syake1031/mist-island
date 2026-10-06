using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MistIsland
{
    /// <summary>
    /// 霧の海に浮かぶ島の地形。高さは式で決まるので、移動や配置はメッシュを使わずに計算できる。
    /// 島の拡張では半径を広げて作り直す。海面の高さは 0。
    /// </summary>
    public class Island : MonoBehaviour
    {
        public const float SeaLevel = 0f;
        public const float WalkableHeight = 0.12f;

        public static Island Instance { get; private set; }

        public float Radius { get; private set; }
        /// <summary>建物を置ける範囲（町）の半径。</summary>
        public float TownRadius { get; private set; }

        float _landHeight = 1.4f;
        float[] _phase = new float[8];
        Vector4[] _mountains = new Vector4[0]; // x, z, 高さ, 半径
        GameObject _terrain;
        GameObject _decorations;
        Mesh _decorationMesh;
        Mesh _mesh;

        static readonly Color Sand = new Color(0.93f, 0.87f, 0.72f);
        static readonly Color WetSand = new Color(0.78f, 0.76f, 0.66f);
        static readonly Color Grass = new Color(0.6f, 0.76f, 0.54f);
        static readonly Color LightGrass = new Color(0.72f, 0.84f, 0.6f);
        static readonly Color Rock = new Color(0.68f, 0.67f, 0.66f);
        static readonly Color SeaBed = new Color(0.5f, 0.62f, 0.62f);
        static readonly Color Snow = new Color(0.94f, 0.95f, 0.98f);

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
            TownRadius = config.TownRadius(expansion);
            _landHeight = config.landHeight;
            int seed = config.islandSeed;
            var rng = new System.Random(seed);
            for (int i = 0; i < _phase.Length; i++) _phase[i] = (float)(rng.NextDouble() * Mathf.PI * 2f);
            BuildMountains(config, seed);

            BuildTerrain();
            BuildDecorations(seed);
        }

        /// <summary>
        /// 山の位置は島の大きさに関係なく種から決まる（拡張しても山は動かない）。
        /// 町の中心から離れた所に、ほぼ均等な向きで並べる。
        /// </summary>
        void BuildMountains(GameConfig config, int seed)
        {
            var rng = new System.Random(seed * 17 + 3);
            int count = Mathf.Max(0, config.mountainCount);
            _mountains = new Vector4[count];
            float startAngle = (float)(rng.NextDouble() * Mathf.PI * 2f);
            for (int i = 0; i < count; i++)
            {
                float angle = startAngle + i * Mathf.PI * 2f / Mathf.Max(1, count) + (float)(rng.NextDouble() - 0.5) * 0.8f;
                float dist = Mathf.Lerp(config.mountainMinDistance, config.mountainMaxDistance, (float)rng.NextDouble());
                float height = Mathf.Lerp(config.mountainMinHeight, config.mountainMaxHeight, (float)rng.NextDouble());
                float r = Mathf.Lerp(config.mountainMinRadius, config.mountainMaxRadius, (float)rng.NextDouble());
                _mountains[i] = new Vector4(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist, height, r);
            }
        }

        float MountainHeight(float x, float z)
        {
            float h = 0f;
            for (int i = 0; i < _mountains.Length; i++)
            {
                Vector4 m = _mountains[i];
                float dx = x - m.x;
                float dz = z - m.y;
                float q = (dx * dx + dz * dz) / (m.w * m.w);
                if (q > 9f) continue;
                // 尾根っぽく少し揺らす
                float ridge = 1f + 0.12f * Mathf.Sin(dx * 0.9f + _phase[i % _phase.Length]) * Mathf.Cos(dz * 0.8f);
                h = Mathf.Max(h, m.z * Mathf.Exp(-q) * ridge);
            }
            return h;
        }

        // ---- 高さ ----

        public float CoastRadius(float angle)
        {
            float wobble = 0.08f * Mathf.Sin(3f * angle + _phase[0])
                         + 0.05f * Mathf.Sin(5f * angle + _phase[1])
                         + 0.03f * Mathf.Sin(9f * angle + _phase[2]);
            return Radius * (1f + wobble);
        }

        /// <summary>中心からの距離を海岸線で割った値。1 で海岸。</summary>
        public float NormalizedDistance(float x, float z)
        {
            float d = Mathf.Sqrt(x * x + z * z);
            return d / CoastRadius(Mathf.Atan2(z, x));
        }

        public float HeightAt(float x, float z)
        {
            float t = NormalizedDistance(x, z);
            float d = Mathf.Sqrt(x * x + z * z);

            float land = 1f - Smooth(0.78f, 1f, t);
            float hills = 0.32f * Mathf.Sin(x * 0.23f + _phase[3]) * Mathf.Cos(z * 0.19f + _phase[4])
                        + 0.16f * Mathf.Sin((x + z) * 0.41f + _phase[5]);
            // 町の中心は平らにしておく
            hills *= Smooth(3f, 8f, d);
            // 大きなうねりと山。町の中心付近は平らなまま
            float rolling = 0.9f * Mathf.Sin(x * 0.07f + _phase[6]) * Mathf.Cos(z * 0.06f + _phase[7]);
            float relief = (rolling + MountainHeight(x, z)) * Smooth(6f, 13f, d);
            float h = (_landHeight + hills + Mathf.Max(-0.6f, relief)) * land;
            h -= 0.25f * Smooth(0.9f, 1.05f, t);
            h -= 1.6f * Smooth(1f, 1.35f, t);
            return h;
        }

        public float HeightAt(Vector3 p)
        {
            return HeightAt(p.x, p.z);
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
            float r = CoastRadius(angle) * 1.12f + extra;
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
            float extent = Radius * 1.45f + 4f;
            const float step = 0.75f;
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
            if (h < 0.35f) return Color.Lerp(WetSand, Sand, Mathf.Clamp01(h / 0.2f));

            // 斜面は岩、平らなところは草。少し揺らぎを入れる
            const float e = 0.4f;
            float slope = Mathf.Abs(HeightAt(x + e, z) - HeightAt(x - e, z)) + Mathf.Abs(HeightAt(x, z + e) - HeightAt(x, z - e));
            float grassMix = Mathf.Clamp01((h - 0.35f) / 0.3f);
            float tint = 0.5f + 0.5f * Mathf.Sin(x * 0.5f + _phase[6]) * Mathf.Sin(z * 0.45f + _phase[7]);
            Color grass = Color.Lerp(Grass, LightGrass, tint);
            Color c = Color.Lerp(Sand, grass, grassMix);
            c = Color.Lerp(c, Rock, Mathf.Clamp01((slope - 0.45f) * 1.5f));
            // 高いところは雪
            return Color.Lerp(c, Snow, Mathf.Clamp01((h - 6.5f) / 1.2f));
        }

        // ---- 木と岩 ----

        void BuildDecorations(int seed)
        {
            if (_decorations != null) Destroy(_decorations);
            if (_decorationMesh != null) Destroy(_decorationMesh);
            _decorations = new GameObject("Decorations");
            _decorations.transform.SetParent(transform, false);

            var rng = new System.Random(seed * 31 + 1);
            int attempts = Mathf.Min(900, Mathf.RoundToInt(Radius * Radius * 0.3f));
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
                if (t > 0.8f || d < 4f) continue;
                if (TownLayout.IsNearAnySlot(new Vector3(x, 0, z), 2.2f, TownRadius)) continue;
                float h = HeightAt(x, z);
                if (h < 0.5f || h > 6.5f) continue;

                bool isRock = rng.NextDouble() < 0.18;
                var p = new Vector3(x, h, z);
                Quaternion yaw = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                if (isRock)
                {
                    float s = 0.5f + (float)rng.NextDouble() * 0.7f;
                    batch.Add(sphere, Matrix4x4.TRS(p, yaw, new Vector3(s * 1.3f, s * 0.7f, s)), Rock);
                }
                else
                {
                    float s = 0.8f + (float)rng.NextDouble() * 0.6f;
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
