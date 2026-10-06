using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MistIsland
{
    /// <summary>色付きのメッシュをたくさん重ねて1つのメッシュにする（頂点カラーで色を持つ）。</summary>
    public class MeshBatch
    {
        readonly List<Vector3> _vertices = new List<Vector3>();
        readonly List<Vector3> _normals = new List<Vector3>();
        readonly List<Color> _colors = new List<Color>();
        readonly List<int> _triangles = new List<int>();

        public void Add(Mesh mesh, Matrix4x4 matrix, Color color)
        {
            int offset = _vertices.Count;
            Vector3[] v = mesh.vertices;
            Vector3[] n = mesh.normals;
            Matrix4x4 normalMatrix = matrix.inverse.transpose;
            for (int i = 0; i < v.Length; i++)
            {
                _vertices.Add(matrix.MultiplyPoint3x4(v[i]));
                _normals.Add(i < n.Length ? normalMatrix.MultiplyVector(n[i]).normalized : Vector3.up);
                _colors.Add(color);
            }
            int[] t = mesh.triangles;
            for (int i = 0; i < t.Length; i++) _triangles.Add(t[i] + offset);
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetColors(_colors);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
