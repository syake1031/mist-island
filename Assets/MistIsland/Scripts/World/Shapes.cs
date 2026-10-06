using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MistIsland
{
    /// <summary>
    /// プリミティブとマテリアルを作るための小物。
    /// 試作段階なのでモデルは用意せず、基本形状の組み合わせで見た目を作る。
    /// </summary>
    public static class Shapes
    {
        static Shader _shader;
        static readonly Dictionary<Color32, Material> _materials = new Dictionary<Color32, Material>();
        static Material _vertexColorMaterial;
        static Mesh _cone;

        public static Shader Shader
        {
            get
            {
                if (_shader == null)
                {
                    _shader = Resources.Load<Shader>("MistIsland/MistLit");
                    if (_shader == null) _shader = Shader.Find("MistIsland/MistLit");
                    if (_shader == null)
                    {
                        Debug.LogError("[MistIsland] MistLit シェーダーが見つかりません。Resources/MistIsland/MistLit.shader を確認してください。");
                        _shader = Shader.Find("Unlit/Color");
                    }
                }
                return _shader;
            }
        }

        public static Material Material(Color color)
        {
            Color32 key = color;
            Material mat;
            if (_materials.TryGetValue(key, out mat) && mat != null) return mat;
            mat = new Material(Shader);
            mat.SetColor("_Color", color);
            _materials[key] = mat;
            return mat;
        }

        public static Material VertexColorMaterial
        {
            get
            {
                if (_vertexColorMaterial == null)
                {
                    _vertexColorMaterial = new Material(Shader);
                    _vertexColorMaterial.SetColor("_Color", Color.white);
                    _vertexColorMaterial.SetFloat("_UseVertexColor", 1f);
                }
                return _vertexColorMaterial;
            }
        }

        public static GameObject Create(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Color color, string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            if (name != null) go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            Setup(go, parent, localPos, localScale, Material(color));
            return go;
        }

        public static GameObject Cone(Transform parent, Vector3 localPos, Vector3 localScale, Color color, string name = "Cone")
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = ConeMesh;
            go.AddComponent<MeshRenderer>();
            Setup(go, parent, localPos, localScale, Material(color));
            return go;
        }

        public static GameObject FromMesh(Mesh mesh, Transform parent, Material material, string name)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>();
            Setup(go, parent, Vector3.zero, Vector3.one, material);
            return go;
        }

        static void Setup(GameObject go, Transform parent, Vector3 localPos, Vector3 localScale, Material material)
        {
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        /// <summary>高さ1・底面の直径1の円錐。原点は底面の中心。</summary>
        public static Mesh ConeMesh
        {
            get
            {
                if (_cone != null) return _cone;
                const int segments = 10;
                var verts = new List<Vector3>();
                var tris = new List<int>();
                // 側面はフラットシェーディング風に面ごとに頂点を分ける
                for (int i = 0; i < segments; i++)
                {
                    float a0 = i * Mathf.PI * 2f / segments;
                    float a1 = (i + 1) * Mathf.PI * 2f / segments;
                    int b = verts.Count;
                    verts.Add(new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f));
                    verts.Add(new Vector3(0f, 1f, 0f));
                    verts.Add(new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f));
                    tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                }
                int center = verts.Count;
                verts.Add(Vector3.zero);
                for (int i = 0; i < segments; i++)
                {
                    float a0 = i * Mathf.PI * 2f / segments;
                    float a1 = (i + 1) * Mathf.PI * 2f / segments;
                    int b = verts.Count;
                    verts.Add(new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f));
                    verts.Add(new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f));
                    tris.Add(center); tris.Add(b); tris.Add(b + 1);
                }
                _cone = new Mesh { name = "MistCone" };
                _cone.SetVertices(verts);
                _cone.SetTriangles(tris, 0);
                _cone.RecalculateNormals();
                _cone.RecalculateBounds();
                return _cone;
            }
        }

        static Material _shadowMaterial;

        /// <summary>足元の丸い影。parent の足元（ローカル原点）に置く。</summary>
        public static GameObject BlobShadow(Transform parent, float radius)
        {
            if (_shadowMaterial == null)
            {
                Shader shader = Resources.Load<Shader>("MistIsland/BlobShadow");
                if (shader == null) shader = Shader.Find("MistIsland/BlobShadow");
                if (shader == null) return null;
                _shadowMaterial = new Material(shader);
            }
            var go = new GameObject("BlobShadow");
            go.AddComponent<MeshFilter>().sharedMesh = PrimitiveMesh(PrimitiveType.Quad);
            go.AddComponent<MeshRenderer>();
            Setup(go, parent, new Vector3(0f, 0.04f, 0f), new Vector3(radius * 2f, radius * 2f, 1f), _shadowMaterial);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return go;
        }

        static readonly Dictionary<PrimitiveType, Mesh> _primitiveMeshes = new Dictionary<PrimitiveType, Mesh>();

        /// <summary>Unity 標準プリミティブのメッシュだけを取り出す。</summary>
        public static Mesh PrimitiveMesh(PrimitiveType type)
        {
            Mesh mesh;
            if (_primitiveMeshes.TryGetValue(type, out mesh) && mesh != null) return mesh;
            var go = GameObject.CreatePrimitive(type);
            mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(go);
            _primitiveMeshes[type] = mesh;
            return mesh;
        }

        /// <summary>被弾時の点滅などで、マテリアルを複製せずに色を変える。</summary>
        public static void Tint(Renderer[] renderers, MaterialPropertyBlock block, float emission)
        {
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetFloat("_Emission", emission);
                r.SetPropertyBlock(block);
            }
        }
    }
}
