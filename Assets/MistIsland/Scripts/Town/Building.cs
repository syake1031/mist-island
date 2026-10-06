using UnityEngine;

namespace MistIsland
{
    /// <summary>
    /// 島に建てる施設・防衛装置。施設は時間とともに収入を貯め、プレイヤーが近づくと受け取れる。
    /// 防衛装置は夜に敵を攻撃したり引きつけたりする。壊されても翌朝には直る。
    /// </summary>
    public class Building : MonoBehaviour
    {
        public BuildingDef Def { get; private set; }
        public SlotInfo Slot { get; private set; }
        public int Level { get; private set; }
        public float Stored { get; private set; }
        public Health Health { get; private set; }
        public bool Ruined { get { return !Health.IsAlive; } }

        GameConfig _config;
        Transform _visual;
        Transform _coinMarker;
        Renderer[] _renderers;
        MaterialPropertyBlock _block;
        float _fireTimer;
        float _flash;

        public void Setup(BuildingDef def, SlotInfo slot, int level, float stored, GameConfig config)
        {
            Def = def;
            Slot = slot;
            _config = config;
            transform.position = slot.position;
            Health = gameObject.AddComponent<Health>();
            Health.Radius = def.type == BuildingType.Fence ? 1.4f : 1.2f;
            Health.Damaged += OnDamaged;
            Health.Died += OnRuined;
            _block = new MaterialPropertyBlock();
            Level = Mathf.Max(1, level);
            Health.Init(MaxHp);
            Stored = Mathf.Clamp(stored, 0f, Capacity);
            BuildVisual();
        }

        public float MaxHp { get { return Def.maxHp + Def.hpPerLevel * (Level - 1); } }
        public float LevelFactor { get { return 1f + 0.75f * (Level - 1); } }
        public float IncomePerSecond { get { return Def.incomePerMinute * LevelFactor / 60f; } }
        public float Capacity { get { return Def.incomePerMinute * LevelFactor * _config.facilityCapMinutes; } }
        public float DefensePower { get { return Def.defensePowerPerLevel * Level; } }
        public float Damage { get { return Def.damage * LevelFactor; } }
        public float Range { get { return Def.range + 0.5f * (Level - 1); } }
        public bool IsMaxLevel { get { return Level >= _config.buildingMaxLevel; } }

        /// <summary>次のレベルに上げるコスト（建てるときのコストの costGrowth^レベル 倍）。</summary>
        public Cost NextCost { get { return (Def.cost ?? new Cost()).Scaled(System.Math.Pow(Def.costGrowth, Level)); } }

        public void SetLevel(int level)
        {
            Level = level;
            Health.SetMax(MaxHp);
            if (!Ruined) Health.HealFull();
            BuildVisual();
        }

        /// <summary>島の拡張で地形の高さが変わったときに置き直す。</summary>
        public void MoveTo(SlotInfo slot)
        {
            Slot = slot;
            transform.position = slot.position;
        }

        public void Repair()
        {
            Health.HealFull();
            BuildVisual();
        }

        /// <summary>貯まった収入を取り出す（整数ぶんだけ）。</summary>
        public int TakeStored()
        {
            int amount = Mathf.FloorToInt(Stored);
            Stored -= amount;
            UpdateCoinMarker();
            return amount;
        }

        public void ClearStored()
        {
            Stored = 0f;
            UpdateCoinMarker();
        }

        public void AddProduction(double seconds)
        {
            if (!Def.IsFacility || Ruined) return;
            Stored = Mathf.Min(Capacity, Stored + (float)(IncomePerSecond * seconds));
            UpdateCoinMarker();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_flash > 0f)
            {
                _flash -= dt;
                Shapes.Tint(_renderers, _block, Ruined ? -0.45f : (_flash > 0f ? 0.6f : 0f));
            }
            if (Ruined) return;

            if (Def.IsFacility)
            {
                AddProduction(dt);
                if (_coinMarker != null && _coinMarker.gameObject.activeSelf)
                {
                    _coinMarker.localRotation = Quaternion.Euler(0f, Time.time * 90f, 0f);
                    _coinMarker.localPosition = new Vector3(0f, 3.1f + Mathf.Sin(Time.time * 2f) * 0.15f, 0f);
                }
            }

            if (Def.type == BuildingType.Catapult)
            {
                _fireTimer -= dt;
                if (_fireTimer <= 0f)
                {
                    Enemy target = Enemy.Nearest(transform.position, Range);
                    if (target != null)
                    {
                        _fireTimer = Def.fireInterval;
                        Boulder.Throw(transform.position + Vector3.up * 1.6f, target.transform.position, Damage, Def.splashRadius);
                    }
                    else
                    {
                        _fireTimer = 0.25f;
                    }
                }
            }

            if (Def.type == BuildingType.Watchtower)
            {
                _fireTimer -= dt;
                if (_fireTimer <= 0f)
                {
                    Enemy target = Enemy.Nearest(transform.position, Range);
                    if (target != null)
                    {
                        _fireTimer = Def.fireInterval;
                        Vector3 from = transform.position + Vector3.up * 3.4f;
                        Vector3 to = target.transform.position + Vector3.up * 0.6f;
                        Projectile.Fire(from, to - from, 18f, Range + 2f, Damage, false);
                    }
                    else
                    {
                        _fireTimer = 0.25f;
                    }
                }
            }
        }

        void OnDamaged(float amount)
        {
            _flash = 0.12f;
        }

        void OnRuined()
        {
            BuildVisual();
            if (GameManager.Instance != null)
                GameManager.Instance.Toast(Def.name + "が壊された！（朝に直ります）");
        }

        // ---- 見た目 ----

        static readonly Color Wall = new Color(0.95f, 0.92f, 0.84f);
        static readonly Color Roof = new Color(0.84f, 0.52f, 0.44f);
        static readonly Color Wood = new Color(0.62f, 0.48f, 0.38f);
        static readonly Color DarkWood = new Color(0.45f, 0.36f, 0.3f);
        static readonly Color Gold = new Color(0.98f, 0.82f, 0.36f);
        static readonly Color Stone = new Color(0.66f, 0.66f, 0.68f);

        void BuildVisual()
        {
            if (_visual != null) Destroy(_visual.gameObject);
            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);
            _visual.localRotation = Quaternion.Euler(0f, Slot.yaw, 0f);
            _coinMarker = null;

            float grow = 1f + 0.06f * (Level - 1);
            Transform v = _visual;
            switch (Def.type)
            {
                case BuildingType.Bank:
                    Shapes.Create(PrimitiveType.Cube, v, new Vector3(0, 0.6f * grow, 0), new Vector3(2f, 1.2f * grow, 2f), Wall);
                    Shapes.Cone(v, new Vector3(0, 1.2f * grow, 0), new Vector3(2.8f, 1.1f, 2.8f), Roof);
                    Shapes.Create(PrimitiveType.Cylinder, v, new Vector3(0, 0.75f * grow, -1.01f), new Vector3(0.6f, 0.02f, 0.6f), Gold).transform.localRotation = Quaternion.Euler(90, 0, 0);
                    break;
                case BuildingType.Farm:
                    Shapes.Create(PrimitiveType.Cube, v, new Vector3(0, 0.08f, 0), new Vector3(3f, 0.16f, 3f), new Color(0.66f, 0.52f, 0.4f));
                    for (int i = -1; i <= 1; i++)
                        Shapes.Create(PrimitiveType.Cube, v, new Vector3(i * 0.9f, 0.3f, 0), new Vector3(0.45f, 0.35f * grow, 2.6f), new Color(0.56f, 0.76f, 0.42f));
                    Shapes.Create(PrimitiveType.Cube, v, new Vector3(1.2f, 0.5f, 1.2f), new Vector3(0.7f, 1f, 0.7f), Wall);
                    break;
                case BuildingType.Mine:
                    Shapes.Create(PrimitiveType.Sphere, v, new Vector3(0, 0.5f, 0.3f), new Vector3(2.6f, 1.6f * grow, 2.2f), Stone);
                    Shapes.Create(PrimitiveType.Sphere, v, new Vector3(1f, 0.3f, -0.4f), new Vector3(1.2f, 0.8f, 1.2f), Stone * 0.92f);
                    Shapes.Create(PrimitiveType.Cube, v, new Vector3(0, 0.45f, -0.75f), new Vector3(0.9f, 0.9f, 0.4f), new Color(0.25f, 0.24f, 0.26f));
                    Shapes.Create(PrimitiveType.Cube, v, new Vector3(0, 0.95f, -0.85f), new Vector3(1.1f, 0.14f, 0.3f), Wood);
                    break;
                case BuildingType.Watchtower:
                {
                    float h = 3f * grow;
                    foreach (var off in new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f) })
                        Shapes.Create(PrimitiveType.Cube, v, off + new Vector3(0, h * 0.5f, 0), new Vector3(0.18f, h, 0.18f), Wood);
                    Shapes.Create(PrimitiveType.Cube, v, new Vector3(0, h, 0), new Vector3(1.6f, 0.2f, 1.6f), DarkWood);
                    Shapes.Create(PrimitiveType.Cube, v, new Vector3(0, h + 0.35f, 0), new Vector3(1.5f, 0.5f, 1.5f), Wood * 1.05f);
                    Shapes.Cone(v, new Vector3(0, h + 0.7f, 0), new Vector3(2f, 0.9f, 2f), Roof);
                    break;
                }
                case BuildingType.Fence:
                {
                    // 外向きに立てる柵。幅はレベルで少しずつ伸びる
                    float width = 3.6f * Mathf.Min(1.3f, grow);
                    int posts = 7;
                    for (int i = 0; i < posts; i++)
                    {
                        float x = Mathf.Lerp(-width * 0.5f, width * 0.5f, i / (float)(posts - 1));
                        Shapes.Create(PrimitiveType.Cube, v, new Vector3(x, 0.6f, 0.6f), new Vector3(0.22f, 1.2f + 0.1f * (i % 2), 0.22f), Wood);
                        Shapes.Cone(v, new Vector3(x, 1.2f + 0.1f * (i % 2), 0.6f), new Vector3(0.24f, 0.25f, 0.24f), Wood);
                    }
                    Shapes.Create(PrimitiveType.Cube, v, new Vector3(0, 0.45f, 0.6f), new Vector3(width, 0.14f, 0.12f), DarkWood);
                    Shapes.Create(PrimitiveType.Cube, v, new Vector3(0, 0.85f, 0.6f), new Vector3(width, 0.14f, 0.12f), DarkWood);
                    break;
                }
                case BuildingType.LumberMill:
                    Shapes.Create(PrimitiveType.Cube, v, new Vector3(0, 0.6f, 0.5f), new Vector3(1.8f, 1.2f, 1.2f), Wood);
                    Shapes.Cone(v, new Vector3(0, 1.2f, 0.5f), new Vector3(2.4f, 0.8f, 1.8f), Roof);
                    for (int i = 0; i < 3; i++)
                    {
                        var log = Shapes.Create(PrimitiveType.Cylinder, v, new Vector3(-0.5f + i * 0.5f, 0.2f + (i == 1 ? 0.35f : 0f), -0.8f), new Vector3(0.35f, 0.7f * grow, 0.35f), new Color(0.6f, 0.44f, 0.3f));
                        log.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    }
                    break;
                case BuildingType.Quarry:
                    Shapes.Create(PrimitiveType.Cube, v, new Vector3(-0.5f, 0.35f, 0.3f), new Vector3(0.9f, 0.7f * grow, 0.9f), Stone);
                    Shapes.Create(PrimitiveType.Cube, v, new Vector3(0.5f, 0.3f, 0.5f), new Vector3(0.8f, 0.6f, 0.8f), Stone * 0.92f);
                    Shapes.Create(PrimitiveType.Cube, v, new Vector3(0.1f, 0.85f, 0.4f), new Vector3(0.7f, 0.5f, 0.7f), Stone * 1.05f);
                    Shapes.Create(PrimitiveType.Cube, v, new Vector3(0.6f, 0.9f, -0.7f), new Vector3(0.12f, 1.8f, 0.12f), Wood);
                    break;
                case BuildingType.MistWell:
                {
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI * 2f / 8f;
                        Shapes.Create(PrimitiveType.Cube, v, new Vector3(Mathf.Cos(a) * 0.9f, 0.3f, Mathf.Sin(a) * 0.9f), new Vector3(0.45f, 0.6f, 0.45f), Stone);
                    }
                    var crystal = Shapes.Cone(v, new Vector3(0, 0.4f, 0), new Vector3(0.5f, 1.4f * grow, 0.5f), new Color(0.7f, 0.85f, 1f), "Crystal");
                    var glow = new MaterialPropertyBlock();
                    glow.SetFloat("_Emission", 0.8f);
                    crystal.GetComponent<Renderer>().SetPropertyBlock(glow);
                    break;
                }
                case BuildingType.Catapult:
                {
                    Shapes.Create(PrimitiveType.Cube, v, new Vector3(0, 0.25f, 0), new Vector3(1.6f, 0.3f, 1.2f), Wood);
                    Shapes.Create(PrimitiveType.Cube, v, new Vector3(-0.6f, 0.8f, 0), new Vector3(0.15f, 1f, 0.15f), DarkWood);
                    Shapes.Create(PrimitiveType.Cube, v, new Vector3(0.6f, 0.8f, 0), new Vector3(0.15f, 1f, 0.15f), DarkWood);
                    var arm = Shapes.Create(PrimitiveType.Cube, v, new Vector3(0, 1.1f, 0.2f), new Vector3(0.14f, 0.14f, 2f * grow), Wood);
                    arm.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
                    Shapes.Create(PrimitiveType.Sphere, v, new Vector3(0, 1.75f, 1.1f), Vector3.one * 0.4f, Stone);
                    break;
                }
                case BuildingType.Lantern:
                {
                    Shapes.Create(PrimitiveType.Cube, v, new Vector3(0, 0.15f, 0), new Vector3(0.9f, 0.3f, 0.9f), Stone);
                    Shapes.Create(PrimitiveType.Cylinder, v, new Vector3(0, 1.3f * grow, 0), new Vector3(0.15f, 1.2f * grow, 0.15f), DarkWood);
                    var light = Shapes.Create(PrimitiveType.Sphere, v, new Vector3(0, 2.6f * grow, 0), Vector3.one * 0.6f, new Color(1f, 0.92f, 0.65f), "Light");
                    var glow = new MaterialPropertyBlock();
                    glow.SetFloat("_Emission", 1.5f);
                    light.GetComponent<Renderer>().SetPropertyBlock(glow);
                    Shapes.Cone(v, new Vector3(0, 2.85f * grow, 0), new Vector3(0.8f, 0.5f, 0.8f), Roof);
                    break;
                }
            }

            if (Ruined)
            {
                _visual.localScale = new Vector3(1f, 0.45f, 1f);
                _visual.localRotation *= Quaternion.Euler(8f, 0f, 6f);
            }

            _renderers = _visual.GetComponentsInChildren<Renderer>();
            Shapes.Tint(_renderers, _block, Ruined ? -0.45f : 0f);

            if (Def.IsFacility)
            {
                Color markerColor = ResourceColor(Def.produces);
                _coinMarker = Shapes.Create(PrimitiveType.Cylinder, _visual, new Vector3(0, 3.1f, 0), new Vector3(0.6f, 0.06f, 0.6f), markerColor, "CoinMarker").transform;
                var r = _coinMarker.GetComponent<Renderer>();
                var glow = new MaterialPropertyBlock();
                glow.SetFloat("_Emission", 0.4f);
                r.SetPropertyBlock(glow);
                UpdateCoinMarker();
            }

        }

        public static Color ResourceColor(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Wood: return new Color(0.72f, 0.52f, 0.36f);
                case ResourceType.Stone: return new Color(0.75f, 0.75f, 0.78f);
                case ResourceType.Iron: return new Color(0.55f, 0.62f, 0.72f);
                case ResourceType.Crystal: return new Color(0.7f, 0.85f, 1f);
                default: return Gold;
            }
        }

        void UpdateCoinMarker()
        {
            if (_coinMarker == null) return;
            bool show = Stored >= 1f && !Ruined;
            if (_coinMarker.gameObject.activeSelf != show) _coinMarker.gameObject.SetActive(show);
            float s = 0.45f + 0.45f * Mathf.Clamp01(Stored / Mathf.Max(1f, Capacity));
            _coinMarker.localScale = new Vector3(s, 0.06f, s);
        }
    }
}
