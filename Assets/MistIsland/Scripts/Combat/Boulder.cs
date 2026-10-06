using UnityEngine;

namespace MistIsland
{
    /// <summary>投石台が投げる岩。弧を描いて飛び、着弾した地点の周りの敵にまとめてダメージを与える。</summary>
    public class Boulder : MonoBehaviour
    {
        Vector3 _from;
        Vector3 _to;
        float _damage;
        float _radius;
        float _time;
        const float FlightSeconds = 0.9f;

        public static void Throw(Vector3 from, Vector3 to, float damage, float radius)
        {
            var go = new GameObject("Boulder");
            go.transform.position = from;
            Shapes.Create(PrimitiveType.Sphere, go.transform, Vector3.zero, Vector3.one * 0.55f, new Color(0.62f, 0.6f, 0.62f));
            var b = go.AddComponent<Boulder>();
            b._from = from;
            b._to = to;
            b._damage = damage;
            b._radius = Mathf.Max(0.5f, radius);
        }

        void Update()
        {
            _time += Time.deltaTime;
            float u = Mathf.Clamp01(_time / FlightSeconds);
            Vector3 p = Vector3.Lerp(_from, _to, u);
            p.y += Mathf.Sin(u * Mathf.PI) * 4f;
            transform.position = p;
            transform.Rotate(300f * Time.deltaTime, 0f, 0f);
            if (u < 1f) return;

            foreach (var e in Enemy.All.ToArray())
            {
                if (e == null || !e.IsActive) continue;
                Vector3 d = e.transform.position - _to;
                d.y = 0f;
                if (d.magnitude <= _radius + e.Health.Radius) e.TakeHit(_damage, false);
            }
            Destroy(gameObject);
        }
    }
}
