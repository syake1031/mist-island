using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MistIsland
{
    /// <summary>
    /// ゆるく波打つ海。島のまわりは細かいメッシュで浅瀬の明るい色にし、
    /// その外側は粗い平面で深い青にする（端は霧に溶ける）。
    /// </summary>
    public class Sea : MonoBehaviour
    {
        static readonly Color Shallow = new Color(0.36f, 0.8f, 0.78f);
        static readonly Color Mid = new Color(0.25f, 0.56f, 0.76f);
        static readonly Color Deep = new Color(0.2f, 0.4f, 0.68f);

        Material _material;
        GameObject _near;
        Mesh _nearMesh;

        public void Build(float size, Island island)
        {
            _material = new Material(Shapes.Shader);
            _material.SetColor("_Color", Color.white);
            _material.SetFloat("_UseVertexColor", 1f);
            _material.SetFloat("_WaveAmp", 0.05f);

            // 外側の深い海（島のまわりの細かい海より少し下に置いて重なりを避ける）
            Mesh far = Grid(size, 64, null, -0.04f);
            Shapes.FromMesh(far, transform, _material, "SeaFar");

            Rebuild(island);
        }

        /// <summary>島の大きさが変わったら浅瀬の色を作り直す。</summary>
        public void Rebuild(Island island)
        {
            if (_near != null) Destroy(_near);
            if (_nearMesh != null) Destroy(_nearMesh);
            float size = island.Radius * 3.2f + 20f;
            int n = Mathf.Clamp(Mathf.CeilToInt(size / 0.9f), 32, 160);
            _nearMesh = Grid(size, n, island, 0f);
            _near = Shapes.FromMesh(_nearMesh, transform, _material, "SeaNear");
        }

        static Mesh Grid(float size, int n, Island island, float y)
        {
            float step = size / (n - 1);
            float half = size * 0.5f;
            var verts = new List<Vector3>(n * n);
            var colors = new List<Color>(n * n);
            var tris = new List<int>((n - 1) * (n - 1) * 6);
            for (int iz = 0; iz < n; iz++)
            {
                for (int ix = 0; ix < n; ix++)
                {
                    float x = -half + ix * step;
                    float z = -half + iz * step;
                    verts.Add(new Vector3(x, y, z));
                    colors.Add(SeaColor(island, x, z));
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
            var mesh = new Mesh { name = "Sea" };
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            // 波で上下するぶん、カリングされないよう範囲を広げる
            mesh.bounds = new Bounds(new Vector3(0f, y, 0f), new Vector3(size, 2f, size));
            return mesh;
        }

        static Color SeaColor(Island island, float x, float z)
        {
            if (island == null) return Deep;
            float t = island.NormalizedDistance(x, z);
            // 海岸から沖へ：浅瀬 → 中くらい → 深い
            if (t < 1.15f) return Shallow;
            if (t < 1.5f) return Color.Lerp(Shallow, Mid, (t - 1.15f) / 0.35f);
            return Color.Lerp(Mid, Deep, Mathf.Clamp01((t - 1.5f) / 0.6f));
        }
    }
}
