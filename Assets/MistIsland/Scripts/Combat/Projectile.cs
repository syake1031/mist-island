using UnityEngine;

namespace MistIsland
{
    /// <summary>プレイヤーの弓や見張り塔が放つ矢。最初に触れた敵に当たる。</summary>
    public class Projectile : MonoBehaviour
    {
        Vector3 _velocity;
        float _damage;
        float _remaining;
        bool _fromPlayer;

        public static void Fire(Vector3 from, Vector3 direction, float speed, float range, float damage, bool fromPlayer)
        {
            // 当たり判定は水平距離で見る。見張り塔の矢は上から斜めに飛ぶ
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
            direction.Normalize();

            var go = new GameObject("Arrow");
            go.transform.position = from;
            go.transform.rotation = Quaternion.LookRotation(direction);
            Shapes.Create(PrimitiveType.Cube, go.transform, Vector3.zero, new Vector3(0.06f, 0.06f, 0.7f), new Color(0.45f, 0.35f, 0.28f));
            Shapes.Create(PrimitiveType.Cube, go.transform, new Vector3(0, 0, 0.36f), new Vector3(0.12f, 0.12f, 0.14f), new Color(0.85f, 0.85f, 0.88f));

            var p = go.AddComponent<Projectile>();
            p._velocity = direction * speed;
            p._damage = damage;
            p._remaining = range;
            p._fromPlayer = fromPlayer;
        }

        void Update()
        {
            Vector3 step = _velocity * Time.deltaTime;
            Vector3 pos = transform.position + step;
            _remaining -= step.magnitude;

            Enemy hit = Enemy.FindHit(pos, 0.45f);
            if (hit != null)
            {
                hit.TakeHit(_damage, _fromPlayer);
                Destroy(gameObject);
                return;
            }

            transform.position = pos;
            if (_remaining <= 0f) Destroy(gameObject);
        }
    }
}
