using System.Collections.Generic;
using UnityEngine;

namespace MistIsland
{
    /// <summary>
    /// 夜に海から上陸してくる襲撃者。近くのプレイヤーや建物を狙い、何もなければ拠点（町の中心）へ向かう。
    /// </summary>
    public class Enemy : MonoBehaviour
    {
        public static readonly List<Enemy> All = new List<Enemy>();

        public EnemyDef Def { get; private set; }
        public Health Health { get; private set; }

        float _damage;
        float _speed;
        float _attackTimer;
        float _retargetTimer;
        float _flash;
        float _lunge;
        bool _retreating;
        bool _dying;
        float _dieTimer;
        Health _target;
        Transform _model;
        Renderer[] _renderers;
        MaterialPropertyBlock _block;

        public static Enemy Spawn(EnemyDef def, int day, GameConfig config, Vector3 position)
        {
            var go = new GameObject(def.name);
            go.transform.position = position;
            var e = go.AddComponent<Enemy>();
            e.Setup(def, day, config);
            return e;
        }

        void Setup(EnemyDef def, int day, GameConfig config)
        {
            Def = def;
            Health = gameObject.AddComponent<Health>();
            Health.Init(def.hp * Formulas.DayMultiplier(day, config.enemyHpGrowthPerDay));
            Health.Radius = 0.45f * def.scale;
            Health.Died += OnDied;
            _damage = def.damage * Formulas.DayMultiplier(day, config.enemyDamageGrowthPerDay);
            _speed = def.speed * Random.Range(0.9f, 1.1f);
            _attackTimer = Random.Range(0f, def.attackCooldown);
            BuildModel(def);
            All.Add(this);
        }

        void BuildModel(EnemyDef def)
        {
            _model = new GameObject("Model").transform;
            _model.SetParent(transform, false);
            _model.localScale = Vector3.one * def.scale;
            var eye = new Color(1f, 0.85f, 0.55f);
            Shapes.Create(PrimitiveType.Capsule, _model, new Vector3(0, 0.55f, 0), new Vector3(0.75f, 0.55f, 0.75f), def.color, "Body");
            Shapes.Create(PrimitiveType.Sphere, _model, new Vector3(0, 1.15f, 0.05f), new Vector3(0.55f, 0.5f, 0.55f), def.color * 0.85f, "Head");
            var l = Shapes.Create(PrimitiveType.Sphere, _model, new Vector3(-0.12f, 1.2f, 0.28f), Vector3.one * 0.1f, eye, "EyeL");
            var r = Shapes.Create(PrimitiveType.Sphere, _model, new Vector3(0.12f, 1.2f, 0.28f), Vector3.one * 0.1f, eye, "EyeR");
            var glow = new MaterialPropertyBlock();
            glow.SetFloat("_Emission", 1.2f);
            l.GetComponent<Renderer>().SetPropertyBlock(glow);
            r.GetComponent<Renderer>().SetPropertyBlock(glow);
            _renderers = new[] { _model.Find("Body").GetComponent<Renderer>(), _model.Find("Head").GetComponent<Renderer>() };
            _block = new MaterialPropertyBlock();
        }

        void OnDestroy()
        {
            All.Remove(this);
        }

        public static Enemy FindHit(Vector3 point, float radius, HashSet<Enemy> exclude = null)
        {
            for (int i = 0; i < All.Count; i++)
            {
                Enemy e = All[i];
                if (!e.IsActive) continue;
                if (exclude != null && exclude.Contains(e)) continue;
                Vector3 d = e.transform.position - point;
                d.y = 0f;
                float r = radius + e.Health.Radius;
                if (d.sqrMagnitude <= r * r) return e;
            }
            return null;
        }

        public static Enemy Nearest(Vector3 point, float maxDistance)
        {
            Enemy best = null;
            float bestSq = maxDistance * maxDistance;
            for (int i = 0; i < All.Count; i++)
            {
                Enemy e = All[i];
                if (e._dying || e._retreating || !e.Health.IsAlive) continue;
                Vector3 d = e.transform.position - point;
                d.y = 0f;
                if (d.sqrMagnitude < bestSq)
                {
                    bestSq = d.sqrMagnitude;
                    best = e;
                }
            }
            return best;
        }

        public bool IsActive { get { return !_dying && !_retreating && Health.IsAlive; } }

        public void TakeHit(float damage, bool byPlayer)
        {
            if (!IsActive) return;
            _lastHitByPlayer = byPlayer;
            _flash = 0.12f;
            Health.TakeDamage(damage);
            var gm = GameManager.Instance;
            if (gm != null && gm.Hud != null)
                gm.Hud.FloatingText(transform.position + Vector3.up * 1.6f * Def.scale, Mathf.RoundToInt(damage).ToString(), new Color(1f, 0.95f, 0.8f));
        }

        bool _lastHitByPlayer;

        /// <summary>朝になったら海へ引き返す。</summary>
        public void Retreat()
        {
            if (_dying) return;
            _retreating = true;
        }

        void OnDied()
        {
            _dying = true;
            _dieTimer = 0.35f;
            if (GameManager.Instance != null) GameManager.Instance.OnEnemyKilled(this, _lastHitByPlayer);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            UpdateFlash(dt);

            if (_dying)
            {
                _dieTimer -= dt;
                _model.localScale = Vector3.one * Def.scale * Mathf.Max(0.01f, _dieTimer / 0.35f);
                if (_dieTimer <= 0f) Destroy(gameObject);
                return;
            }

            var island = Island.Instance;
            if (island == null) return;

            if (_retreating)
            {
                Vector3 outward = transform.position;
                outward.y = 0f;
                if (outward.sqrMagnitude < 0.01f) outward = Vector3.forward;
                Move(outward.normalized, _speed * 1.3f, island);
                if (island.NormalizedDistance(transform.position.x, transform.position.z) > 1.25f)
                {
                    _model.localPosition += Vector3.down * dt * 1.5f;
                    if (_model.localPosition.y < -2f) Destroy(gameObject);
                }
                return;
            }

            _retargetTimer -= dt;
            if (_retargetTimer <= 0f || _target == null || !_target.IsAlive)
            {
                _retargetTimer = 0.5f;
                _target = ChooseTarget();
            }

            _attackTimer -= dt;
            if (_target != null)
            {
                Vector3 to = _target.Position - transform.position;
                to.y = 0f;
                float reach = Def.attackRange + _target.Radius + Health.Radius;
                if (to.magnitude > reach)
                {
                    Move(to.normalized, _speed, island);
                }
                else
                {
                    Face(to);
                    if (_attackTimer <= 0f)
                    {
                        _attackTimer = Def.attackCooldown;
                        _lunge = 0.25f;
                        _target.TakeDamage(_damage);
                    }
                }
            }

            Separate();
            UpdateLunge(dt);
        }

        Health ChooseTarget()
        {
            var gm = GameManager.Instance;
            if (gm == null) return null;
            Vector3 pos = transform.position;
            float aggro = gm.Config.enemyAggroRadius;

            Health best = null;
            float bestScore = float.MaxValue;

            Health player = gm.Player != null ? gm.Player.Health : null;
            if (player != null && player.IsAlive)
            {
                float d = Flat(player.Position - pos);
                if (d < aggro)
                {
                    best = player;
                    bestScore = Def.prefersStructures ? d * 1.6f : d;
                }
            }

            if (gm.Town != null)
            {
                foreach (Health s in gm.Town.Targets())
                {
                    if (!s.IsAlive) continue;
                    float d = Flat(s.Position - pos) - s.Radius;
                    if (d > aggro) continue;
                    float score = Def.prefersStructures ? d * 0.6f : d;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = s;
                    }
                }

                if (best == null && gm.Town.Hall != null && gm.Town.Hall.IsAlive) best = gm.Town.Hall;
            }

            if (best == null && player != null && player.IsAlive) best = player;
            return best;
        }

        static float Flat(Vector3 v)
        {
            v.y = 0f;
            return v.magnitude;
        }

        void Move(Vector3 dir, float speed, Island island)
        {
            Vector3 p = transform.position + dir * speed * Time.deltaTime;
            p.y = island.SurfaceAt(p.x, p.z);
            transform.position = p;
            Face(dir);
        }

        void Face(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-10f * Time.deltaTime));
        }

        /// <summary>敵同士が重ならないように押し合う。</summary>
        void Separate()
        {
            Vector3 push = Vector3.zero;
            Vector3 pos = transform.position;
            for (int i = 0; i < All.Count; i++)
            {
                Enemy o = All[i];
                if (o == this || o._dying) continue;
                Vector3 d = pos - o.transform.position;
                d.y = 0f;
                float min = Health.Radius + o.Health.Radius;
                float sq = d.sqrMagnitude;
                if (sq < min * min && sq > 0.0001f)
                {
                    float dist = Mathf.Sqrt(sq);
                    push += d / dist * (min - dist);
                }
            }
            if (push != Vector3.zero)
            {
                Vector3 p = pos + push * 0.5f;
                if (Island.Instance != null) p.y = Island.Instance.SurfaceAt(p.x, p.z);
                transform.position = p;
            }
        }

        void UpdateLunge(float dt)
        {
            if (_lunge > 0f)
            {
                _lunge -= dt;
                float k = Mathf.Sin(Mathf.Clamp01(1f - _lunge / 0.25f) * Mathf.PI);
                _model.localPosition = new Vector3(0, 0, k * 0.45f);
            }
            else
            {
                _model.localPosition = Vector3.zero;
            }
        }

        void UpdateFlash(float dt)
        {
            if (_flash <= 0f) return;
            _flash -= dt;
            Shapes.Tint(_renderers, _block, _flash > 0f ? 1.2f : 0f);
        }
    }
}
