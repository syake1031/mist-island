using System.Collections.Generic;
using UnityEngine;

namespace MistIsland
{
    /// <summary>ゆるく波打つ海。端は霧に溶けるので広い平面で十分。</summary>
    public class Sea : MonoBehaviour
    {
        public void Build(float size)
        {
            const int n = 72;
            float step = size / (n - 1);
            float half = size * 0.5f;
            var verts = new List<Vector3>(n * n);
            var tris = new List<int>((n - 1) * (n - 1) * 6);
            for (int iz = 0; iz < n; iz++)
                for (int ix = 0; ix < n; ix++)
                    verts.Add(new Vector3(-half + ix * step, 0f, -half + iz * step));
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
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            // 波で上下するぶん、カリングされないよう範囲を広げる
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(size, 2f, size));

            var mat = new Material(Shapes.Shader);
            mat.SetColor("_Color", new Color(0.56f, 0.74f, 0.78f));
            mat.SetFloat("_WaveAmp", 0.06f);
            Shapes.FromMesh(mesh, transform, mat, "SeaSurface");
        }
    }
}
