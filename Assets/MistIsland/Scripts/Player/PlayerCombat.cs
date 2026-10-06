using UnityEngine;

namespace MistIsland
{
    /// <summary>
    /// プレイヤーの攻撃。スキルはなく、武器ごとに固定の攻撃モーションが1つ。
    /// ・近くに敵がいると自動で攻撃する（オート攻撃）
    /// ・画面を長押しすると溜め、離すと溜め攻撃（剣＝回転斬り、槍＝踏み込み突き、弓＝貫通矢）
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        enum State { Idle, Attacking, Charging }

        PlayerController _player;
        PlayerStats _stats;
        GameConfig _config;
        Transform _pivot;
        Transform _weapon;
        Transform _ring;
        Renderer _ringRenderer;
        MaterialPropertyBlock _ringBlock;

        State _state = State.Idle;
        float _time;
        float _chargeTime;
        bool _hitDone;
        bool _charged;
        Vector3 _dashDirection;

        public bool IsAttacking { get { return _state == State.Attacking; } }
        public bool IsCharging { get { return _state == State.Charging; } }
        public bool IsFullyCharged { get { return IsCharging && _chargeTime >= _config.chargeSeconds; } }
        public float ChargeRatio { get { return IsCharging ? Mathf.Clamp01(_chargeTime / _config.chargeSeconds) : 0f; } }

        public void Setup(PlayerController player, PlayerStats stats, bool rebuild)
        {
            bool weaponChanged = rebuild || _weapon == null || _stats.weapon == null || _stats.weapon.type != stats.weapon.type;
            _player = player;
            _stats = stats;
            _config = GameConfig.Load();
            if (weaponChanged) BuildWeapon();
            if (_ring == null) BuildRing();
        }

        // ---- 見た目 ----

        void BuildWeapon()
        {
            if (_pivot != null) Destroy(_pivot.gameObject);
            _pivot = new GameObject("WeaponPivot").transform;
            _pivot.SetParent(_player.Model, false);
            _pivot.localPosition = new Vector3(0.32f, 0.75f, 0.05f);

            var metal = new Color(0.88f, 0.9f, 0.94f);
            var wood = new Color(0.6f, 0.46f, 0.36f);
            _weapon = new GameObject("Weapon").transform;
            _weapon.SetParent(_pivot, false);
            switch (_stats.weapon.type)
            {
                case WeaponType.Sword:
                    Shapes.Create(PrimitiveType.Cube, _weapon, new Vector3(0, 0, 0.55f), new Vector3(0.1f, 0.04f, 0.9f), metal);
                    Shapes.Create(PrimitiveType.Cube, _weapon, new Vector3(0, 0, 0.08f), new Vector3(0.3f, 0.06f, 0.06f), wood);
                    break;
                case WeaponType.Spear:
                    Shapes.Create(PrimitiveType.Cube, _weapon, new Vector3(0, 0, 0.6f), new Vector3(0.06f, 0.06f, 1.9f), wood);
                    Shapes.Cone(_weapon, new Vector3(0, 0, 1.55f), new Vector3(0.16f, 0.35f, 0.16f), metal).transform.localRotation = Quaternion.Euler(90, 0, 0);
                    break;
                case WeaponType.Bow:
                    for (int i = -2; i <= 2; i++)
                    {
                        float y = i * 0.18f;
                        float z = -Mathf.Abs(i) * 0.06f;
                        Shapes.Create(PrimitiveType.Cube, _weapon, new Vector3(0, y, z), new Vector3(0.05f, 0.2f, 0.05f), wood);
                    }
                    Shapes.Create(PrimitiveType.Cube, _weapon, new Vector3(0, 0, -0.14f), new Vector3(0.015f, 0.72f, 0.015f), new Color(0.95f, 0.95f, 0.9f));
                    break;
            }
            ResetPose();
        }

        /// <summary>足元に出る溜めの輪。</summary>
        void BuildRing()
        {
            _ring = Shapes.Create(PrimitiveType.Cylinder, _player.transform, new Vector3(0, 0.05f, 0), new Vector3(1f, 0.01f, 1f), new Color(1f, 0.85f, 0.5f), "ChargeRing").transform;
            _ringRenderer = _ring.GetComponent<Renderer>();
            _ringBlock = new MaterialPropertyBlock();
            _ring.gameObject.SetActive(false);
        }

        void UpdateRing()
        {
            bool show = IsCharging;
            if (_ring.gameObject.activeSelf != show) _ring.gameObject.SetActive(show);
            if (!show) return;
            float r = ChargeRatio;
            float s = Mathf.Lerp(0.6f, 2.4f, r);
            if (IsFullyCharged) s += Mathf.Sin(Time.time * 18f) * 0.08f;
            _ring.localScale = new Vector3(s, 0.01f, s);
            _ringRenderer.GetPropertyBlock(_ringBlock);
            _ringBlock.SetFloat("_Emission", IsFullyCharged ? 1.2f : 0.2f);
            _ringRenderer.SetPropertyBlock(_ringBlock);
        }

        // ---- 毎フレーム ----

        void Update()
        {
            if (_player == null) return;
            float dt = Time.deltaTime;

            if (!_player.Health.IsAlive)
            {
                _state = State.Idle;
                _chargeTime = 0f;
                ResetPose();
                UpdateRing();
                return;
            }

            bool hold = InputBridge.Charge;

            switch (_state)
            {
                case State.Attacking:
                    Animate(dt);
                    break;

                case State.Charging:
                    if (hold)
                    {
                        _chargeTime += dt;
                        ChargePose();
                    }
                    else
                    {
                        bool full = _chargeTime >= _config.chargeSeconds;
                        _chargeTime = 0f;
                        _state = State.Idle;
                        ResetPose();
                        if (full) Begin(true);
                    }
                    break;

                default:
                    if (hold)
                    {
                        _state = State.Charging;
                        _chargeTime = 0f;
                    }
                    else if (_config.autoAttack && Enemy.Nearest(transform.position, AutoRange) != null)
                    {
                        Begin(false);
                    }
                    break;
            }

            UpdateRing();
        }

        /// <summary>オート攻撃を始める距離。</summary>
        float AutoRange
        {
            get
            {
                WeaponDef w = _stats.weapon;
                return w.type == WeaponType.Bow ? w.range : w.range + 0.6f;
            }
        }

        void Begin(bool charged)
        {
            _state = State.Attacking;
            _time = 0f;
            _hitDone = false;
            _charged = charged;

            float aimRange = AutoRange * (charged ? _config.chargeRangeMultiplier : 1f) + 1.5f;
            Enemy target = Enemy.Nearest(transform.position, aimRange);
            if (target != null) _player.Face(target.transform.position - transform.position, 0f);
            _dashDirection = transform.forward;
        }

        float MotionSeconds
        {
            get { return _stats.weapon.motionSeconds * (_charged ? 1.3f : 1f); }
        }

        float HitTime
        {
            get { return _stats.weapon.hitTime * (_charged ? 1.3f : 1f); }
        }

        // ---- モーション ----

        void ChargePose()
        {
            // 溜め中は武器を引いて構える
            float k = Ease(ChargeRatio);
            switch (_stats.weapon.type)
            {
                case WeaponType.Sword:
                    _pivot.localRotation = Quaternion.Euler(10f - 30f * k, 70f + 40f * k, 0f);
                    break;
                case WeaponType.Spear:
                    _pivot.localPosition = new Vector3(0.25f, 0.8f, -0.45f * k);
                    break;
                case WeaponType.Bow:
                    _weapon.localPosition = new Vector3(0f, 0f, -0.12f * k);
                    break;
            }
        }

        void Animate(float dt)
        {
            WeaponDef w = _stats.weapon;
            _time += dt;
            float motion = MotionSeconds;
            float u = Mathf.Clamp01(_time / motion);
            float hitU = HitTime / motion;

            switch (w.type)
            {
                case WeaponType.Sword:
                    if (_charged)
                    {
                        // 回転斬り：体ごと1回転
                        _player.Model.localRotation = Quaternion.Euler(0f, 360f * Ease(u), 0f);
                        _pivot.localRotation = Quaternion.Euler(10f, 90f, 0f);
                    }
                    else
                    {
                        float swing;
                        if (u < 0.25f) swing = Mathf.Lerp(70f, 90f, u / 0.25f);
                        else if (u < 0.7f) swing = Mathf.Lerp(90f, -80f, Ease((u - 0.25f) / 0.45f));
                        else swing = Mathf.Lerp(-80f, 70f, (u - 0.7f) / 0.3f);
                        _pivot.localRotation = Quaternion.Euler(10f, swing, 0f);
                    }
                    break;

                case WeaponType.Spear:
                {
                    float reach = _charged ? 1.4f : 0.9f;
                    float z;
                    if (u < hitU * 0.6f) z = Mathf.Lerp(0f, -0.35f, u / (hitU * 0.6f));
                    else if (u < hitU) z = Mathf.Lerp(-0.35f, reach, (u - hitU * 0.6f) / (hitU * 0.4f));
                    else z = Mathf.Lerp(reach, 0f, Ease((u - hitU) / (1f - hitU)));
                    _pivot.localPosition = new Vector3(0.25f, 0.8f, z);
                    _pivot.localRotation = Quaternion.identity;
                    // 溜め突きは前に踏み込む
                    if (_charged && u > hitU * 0.6f && u < hitU) _player.MoveBy(_dashDirection * 9f * dt);
                    break;
                }

                case WeaponType.Bow:
                {
                    float pull = u < hitU ? Ease(u / hitU) : 1f - Ease((u - hitU) / (1f - hitU));
                    _weapon.localPosition = new Vector3(0f, 0f, -0.1f * pull);
                    _pivot.localRotation = Quaternion.Euler(-6f * pull, 0f, 0f);
                    break;
                }
            }

            if (!_hitDone && _time >= HitTime)
            {
                _hitDone = true;
                Strike();
            }

            if (_time >= motion)
            {
                _state = State.Idle;
                ResetPose();
            }
        }

        static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        void ResetPose()
        {
            if (_pivot == null) return;
            _player.Model.localRotation = Quaternion.identity;
            _weapon.localPosition = Vector3.zero;
            switch (_stats.weapon.type)
            {
                case WeaponType.Bow:
                    _pivot.localPosition = new Vector3(0.1f, 0.85f, 0.35f);
                    _pivot.localRotation = Quaternion.identity;
                    break;
                case WeaponType.Spear:
                    _pivot.localPosition = new Vector3(0.25f, 0.8f, 0f);
                    _pivot.localRotation = Quaternion.identity;
                    break;
                default:
                    _pivot.localPosition = new Vector3(0.32f, 0.75f, 0.05f);
                    _pivot.localRotation = Quaternion.Euler(10f, 70f, 0f);
                    break;
            }
        }

        // ---- 当たり判定 ----

        void Strike()
        {
            WeaponDef w = _stats.weapon;
            Vector3 origin = transform.position;
            Vector3 forward = transform.forward;
            float damage = _stats.damage * (_charged ? _config.chargeDamageMultiplier : 1f);
            float range = w.range * (_charged ? _config.chargeRangeMultiplier : 1f);

            if (w.type == WeaponType.Bow)
            {
                float speed = w.projectileSpeed * (_charged ? 1.4f : 1f);
                Projectile.Fire(origin + Vector3.up * 0.9f + forward * 0.5f, forward, speed, range, damage, true, _charged);
                return;
            }

            // 溜めた剣は全方向、それ以外は前方の扇
            float halfArc = _charged && w.type == WeaponType.Sword ? 180f : w.arcDegrees * 0.5f;
            foreach (var e in Enemy.All.ToArray())
            {
                if (e == null || !e.IsActive) continue;
                Vector3 to = e.transform.position - origin;
                float dy = Mathf.Abs(to.y);
                to.y = 0f;
                float dist = to.magnitude;
                if (dist > range + e.Health.Radius || dy > 2.5f) continue;
                if (dist > 0.3f && Vector3.Angle(forward, to) > halfArc) continue;
                e.TakeHit(damage, true);
            }
        }
    }
}
