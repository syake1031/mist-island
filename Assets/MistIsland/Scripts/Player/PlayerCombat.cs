using UnityEngine;

namespace MistIsland
{
    /// <summary>
    /// プレイヤーの攻撃。スキルはなく、武器ごとに固定の攻撃モーションが1つだけある。
    /// 剣＝近距離の横なぎ、槍＝中距離の突き、弓＝遠距離の射撃。攻撃ボタンを押している間くり返す。
    /// 攻撃を始めるとき、間合いの近くにいる敵の方を自動で向く。
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        PlayerController _player;
        PlayerStats _stats;
        Transform _pivot;
        Transform _weapon;
        float _time = -1f;
        bool _hitDone;

        public bool IsAttacking { get { return _time >= 0f; } }

        public void Setup(PlayerController player, PlayerStats stats, bool rebuild)
        {
            bool weaponChanged = rebuild || _weapon == null || _stats.weapon == null || _stats.weapon.type != stats.weapon.type;
            _player = player;
            _stats = stats;
            if (weaponChanged) BuildWeapon();
        }

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
                    // 弓は体の前で縦に構える
                    _pivot.localPosition = new Vector3(0.1f, 0.85f, 0.35f);
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

        void Update()
        {
            if (_player == null || !_player.Health.IsAlive)
            {
                _time = -1f;
                return;
            }

            if (!IsAttacking && InputBridge.Attack) Begin();
            if (IsAttacking) Animate(Time.deltaTime);
        }

        void Begin()
        {
            _time = 0f;
            _hitDone = false;
            float aimRange = _stats.weapon.type == WeaponType.Bow ? _stats.weapon.range : _stats.weapon.range + 2.5f;
            Enemy target = Enemy.Nearest(transform.position, aimRange);
            if (target != null) _player.Face(target.transform.position - transform.position, 0f);
        }

        void Animate(float dt)
        {
            WeaponDef w = _stats.weapon;
            _time += dt;
            float u = Mathf.Clamp01(_time / w.motionSeconds);
            float hitU = w.hitTime / w.motionSeconds;

            switch (w.type)
            {
                case WeaponType.Sword:
                {
                    // 右に振りかぶって左へ横なぎ
                    float swing;
                    if (u < 0.25f) swing = Mathf.Lerp(70f, 90f, u / 0.25f);
                    else if (u < 0.7f) swing = Mathf.Lerp(90f, -80f, Ease((u - 0.25f) / 0.45f));
                    else swing = Mathf.Lerp(-80f, 70f, (u - 0.7f) / 0.3f);
                    _pivot.localRotation = Quaternion.Euler(10f, swing, 0f);
                    break;
                }
                case WeaponType.Spear:
                {
                    // 引いてから突く
                    float z;
                    if (u < hitU * 0.6f) z = Mathf.Lerp(0f, -0.35f, u / (hitU * 0.6f));
                    else if (u < hitU) z = Mathf.Lerp(-0.35f, 0.9f, (u - hitU * 0.6f) / (hitU * 0.4f));
                    else z = Mathf.Lerp(0.9f, 0f, Ease((u - hitU) / (1f - hitU)));
                    _pivot.localPosition = new Vector3(0.25f, 0.8f, z);
                    _pivot.localRotation = Quaternion.identity;
                    break;
                }
                case WeaponType.Bow:
                {
                    // 引き絞って放つ
                    float pull = u < hitU ? Ease(u / hitU) : 1f - Ease((u - hitU) / (1f - hitU));
                    _weapon.localPosition = new Vector3(0f, 0f, -0.1f * pull);
                    _pivot.localRotation = Quaternion.Euler(-6f * pull, 0f, 0f);
                    break;
                }
            }

            if (!_hitDone && _time >= w.hitTime)
            {
                _hitDone = true;
                Strike();
            }

            if (_time >= w.motionSeconds)
            {
                _time = -1f;
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
            if (_stats.weapon.type == WeaponType.Bow)
            {
                _pivot.localPosition = new Vector3(0.1f, 0.85f, 0.35f);
                _weapon.localPosition = Vector3.zero;
            }
            else if (_stats.weapon.type == WeaponType.Spear)
            {
                _pivot.localPosition = new Vector3(0.25f, 0.8f, 0f);
            }
            _pivot.localRotation = _stats.weapon.type == WeaponType.Sword ? Quaternion.Euler(10f, 70f, 0f) : Quaternion.identity;
        }

        void Strike()
        {
            WeaponDef w = _stats.weapon;
            Vector3 origin = transform.position;
            Vector3 forward = transform.forward;

            if (w.type == WeaponType.Bow)
            {
                Projectile.Fire(origin + Vector3.up * 0.9f + forward * 0.5f, forward, w.projectileSpeed, w.range, _stats.damage, true);
                return;
            }

            float halfArc = w.arcDegrees * 0.5f;
            foreach (var e in Enemy.All.ToArray())
            {
                if (e == null || !e.IsActive) continue;
                Vector3 to = e.transform.position - origin;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist > w.range + e.Health.Radius) continue;
                if (dist > 0.3f && Vector3.Angle(forward, to) > halfArc) continue;
                e.TakeHit(_stats.damage, true);
            }
        }
    }
}
