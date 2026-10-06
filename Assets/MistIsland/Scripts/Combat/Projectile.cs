using System.Collections.Generic;
using UnityEngine;

namespace MistIsland
{
    /// <summary>プレイヤーの弓や見張り塔が放つ矢。最初に触れた敵に当たる（貫通矢は通り抜けて何体にも当たる）。</summary>
    public class Projectile : MonoBehaviour
    {
        Vector3 _velocity;
        float _damage;
        float _remaining;
        bool _fromPlayer;
        bool _pierce;
        HashSet<Enemy> _hits;

        public static void Fire(Vector3 from, Vector3 direction, float speed, float range, float damage, bool fromPlayer, bool pierce = false)
        {
            // 当たり判定は水平距離で見る。見張り塔の矢は上から斜めに飛ぶ
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
            direction.Normalize();

            var go = new GameObject("Arrow");
            go.transform.position = from;
            go.transform.rotation = Quaternion.LookRotation(direction);
            float thick = pierce ? 2.2f : 1f;
            Shapes.Create(PrimitiveType.Cube, go.transform, Vector3.zero, new Vector3(0.06f * thick, 0.06f * thick, 0.7f * thick), new Color(0.45f, 0.35f, 0.28f));
            Shapes.Create(PrimitiveType.Cube, go.transform, new Vector3(0, 0, 0.36f), new Vector3(0.12f, 0.12f, 0.14f), new Color(0.85f, 0.85f, 0.88f));

            var p = go.AddComponent<Projectile>();
            p._velocity = direction * speed;
            p._damage = damage;
            p._remaining = range;
            p._fromPlayer = fromPlayer;
            p._pierce = pierce;
            if (pierce)
            {
                p._hits = new HashSet<Enemy>();
                var glow = new MaterialPropertyBlock();
                glow.SetFloat("_Emission", 0.8f);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(glow);
            }
        }

        void Update()
        {
            Vector3 step = _velocity * Time.deltaTime;
            Vector3 pos = transform.position + step;
            _remaining -= step.magnitude;

            Enemy hit = Enemy.FindHit(pos, _pierce ? 0.7f : 0.45f, _hits);
            if (hit != null)
            {
                hit.TakeHit(_damage, _fromPlayer);
                if (!_pierce)
                {
                    Destroy(gameObject);
                    return;
                }
                _hits.Add(hit);
            }

            transform.position = pos;
            if (_remaining <= 0f) Destroy(gameObject);
        }
    }
}
