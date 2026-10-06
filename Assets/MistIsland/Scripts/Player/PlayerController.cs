using UnityEngine;

namespace MistIsland
{
    /// <summary>
    /// 島を歩くプレイヤー。移動は仮想スティック（またはキーボード）、海には入れない。
    /// 倒れても少し待てば拠点で復活する。
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public Health Health { get; private set; }
        public PlayerCombat Combat { get; private set; }
        public PlayerStats Stats { get; private set; }
        public Transform Model { get; private set; }
        public float RespawnRemaining { get; private set; }

        CameraRig _rig;
        GameConfig _config;
        Transform _body;
        Renderer[] _renderers;
        MaterialPropertyBlock _block;
        float _flash;
        float _bob;

        public void Initialize(GameConfig config, CameraRig rig, PlayerStats stats)
        {
            _config = config;
            _rig = rig;
            Health = gameObject.AddComponent<Health>();
            Health.Radius = 0.45f;
            Health.Init(stats.maxHp);
            Health.Damaged += OnDamaged;
            Health.Died += OnDied;
            _block = new MaterialPropertyBlock();

            Model = new GameObject("Model").transform;
            Model.SetParent(transform, false);
            Combat = gameObject.AddComponent<PlayerCombat>();
            ApplyStats(stats);
            PlaceAtHall();
        }

        public void SetRig(CameraRig rig)
        {
            _rig = rig;
        }

        /// <summary>ジョブ・レベル・装備が変わったときに呼ぶ。</summary>
        public void ApplyStats(PlayerStats stats)
        {
            bool jobChanged = Stats.job != stats.job;
            Stats = stats;
            Health.SetMax(stats.maxHp);
            Health.DamageReduction = stats.damageReduction;
            if (jobChanged) BuildModel();
            Combat.Setup(this, stats, jobChanged);
        }

        void BuildModel()
        {
            for (int i = Model.childCount - 1; i >= 0; i--) Destroy(Model.GetChild(i).gameObject);
            JobDef job = Stats.job;
            var skin = new Color(0.98f, 0.86f, 0.76f);
            _body = Shapes.Create(PrimitiveType.Capsule, Model, new Vector3(0, 0.6f, 0), new Vector3(0.6f, 0.5f, 0.6f), job.color, "Body").transform;
            Shapes.Create(PrimitiveType.Sphere, Model, new Vector3(0, 1.25f, 0), Vector3.one * 0.45f, skin, "Head");
            Shapes.Create(PrimitiveType.Cube, Model, new Vector3(0, 1.42f, -0.05f), new Vector3(0.48f, 0.14f, 0.48f), job.color * 0.8f, "Hat");
            // 正面がわかるようにマントを背中側に
            Shapes.Create(PrimitiveType.Cube, Model, new Vector3(0, 0.75f, -0.28f), new Vector3(0.5f, 0.7f, 0.06f), job.color * 0.7f, "Cape");
            _renderers = Model.GetComponentsInChildren<Renderer>();
        }

        public void PlaceAtHall()
        {
            Vector3 p = new Vector3(0f, 0f, -3.2f);
            if (Island.Instance != null) p.y = Island.Instance.HeightAt(p.x, p.z);
            transform.position = p;
            transform.rotation = Quaternion.identity;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_flash > 0f)
            {
                _flash -= dt;
                Shapes.Tint(_renderers, _block, _flash > 0f ? 0.8f : 0f);
            }

            if (!Health.IsAlive)
            {
                RespawnRemaining -= dt;
                if (RespawnRemaining <= 0f) Respawn();
                return;
            }

            Vector2 input = InputBridge.Move;
            Vector3 dir = _rig != null ? _rig.ToWorldDirection(input) : new Vector3(input.x, 0, input.y);
            float speed = Stats.moveSpeed;
            if (Combat.IsCharging) speed *= _config.chargeMoveMultiplier;
            else if (Combat.IsAttacking) speed *= 0.7f;
            if (dir.sqrMagnitude > 0.0004f)
            {
                MoveBy(dir * speed * dt * SlopeFactor(dir));
                if (!Combat.IsAttacking) Face(dir, 14f);
                _bob += dt * 12f * dir.magnitude;
            }
            else
            {
                _bob = Mathf.MoveTowards(_bob, Mathf.Round(_bob / Mathf.PI) * Mathf.PI, dt * 8f);
            }
            Model.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(_bob)) * 0.08f, 0f);
        }

        /// <summary>上り坂では少し遅くなる。</summary>
        float SlopeFactor(Vector3 dir)
        {
            var island = Island.Instance;
            if (island == null) return 1f;
            Vector3 p = transform.position;
            Vector3 ahead = p + dir.normalized * 0.5f;
            float rise = island.HeightAt(ahead.x, ahead.z) - p.y;
            return Mathf.Clamp(1f - rise * 0.6f, 0.55f, 1.1f);
        }

        /// <summary>海に入らないように動く（溜め突きの踏み込みでも使う）。</summary>
        public void MoveBy(Vector3 delta)
        {
            var island = Island.Instance;
            Vector3 p = transform.position;
            Vector3 next = p + delta;
            // 海に入らないよう、軸ごとに試して滑らせる
            if (island != null && !island.IsWalkable(next.x, next.z))
            {
                Vector3 nx = p + new Vector3(delta.x, 0f, 0f);
                Vector3 nz = p + new Vector3(0f, 0f, delta.z);
                if (island.IsWalkable(nx.x, nx.z)) next = nx;
                else if (island.IsWalkable(nz.x, nz.z)) next = nz;
                else next = p;
            }
            if (island != null) next.y = island.HeightAt(next.x, next.z);
            transform.position = next;
        }

        public void Face(Vector3 dir, float sharpness)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            Quaternion target = Quaternion.LookRotation(dir);
            transform.rotation = sharpness <= 0f ? target : Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-sharpness * Time.deltaTime));
        }

        void OnDamaged(float amount)
        {
            _flash = 0.15f;
        }

        void OnDied()
        {
            RespawnRemaining = _config.respawnSeconds;
            Model.gameObject.SetActive(false);
            if (GameManager.Instance != null) GameManager.Instance.Toast("倒れてしまった… 拠点で復活します");
        }

        void Respawn()
        {
            Health.HealFull();
            Model.gameObject.SetActive(true);
            PlaceAtHall();
        }
    }
}
