using System;
using UnityEngine;

namespace MistIsland
{
    [Serializable]
    public class JobDef
    {
        public string name;
        public WeaponType weapon;
        public int unlockLevel;
        public float hpMultiplier = 1f;
        public float speedMultiplier = 1f;
        public float damageMultiplier = 1f;
        public Color color = Color.white;
    }

    [Serializable]
    public class WeaponDef
    {
        public WeaponType type;
        public float range;
        [Tooltip("攻撃が当たる扇の角度（弓は無視）")]
        public float arcDegrees;
        [Tooltip("攻撃モーション1回の長さ（秒）")]
        public float motionSeconds;
        [Tooltip("モーション開始から当たり判定が出るまで（秒）")]
        public float hitTime;
        public float projectileSpeed;
    }

    /// <summary>作って装備する武器。種類（剣・槍・弓）はジョブで決まる。</summary>
    [Serializable]
    public class WeaponItemDef
    {
        public string id;
        public string name;
        public WeaponType type;
        public float damage;
        public int unlockLevel = 1;
        [Tooltip("無料なら最初から持っている")]
        public Cost cost = new Cost();
    }

    [Serializable]
    public class ArmorItemDef
    {
        public string id;
        public string name;
        [Tooltip("受けるダメージを減らす割合 0..1")]
        public float damageReduction;
        public float bonusHp;
        public int unlockLevel = 1;
        public Cost cost = new Cost();
    }

    [Serializable]
    public class EnemyDef
    {
        public string name;
        public int unlockDay = 1;
        public float hp;
        public float damage;
        public float speed;
        public float attackRange = 1f;
        public float attackCooldown = 1.2f;
        public float scale = 1f;
        public int xp;
        [Tooltip("倒したときの落とし物。コインはそのまま、素材は 0〜この数のあいだ")]
        public Cost drops = new Cost();
        public float spawnWeight = 1f;
        [Tooltip("プレイヤーより建物を優先して狙う")]
        public bool prefersStructures;
        public Color color = Color.gray;
    }

    [Serializable]
    public class BuildingDef
    {
        public BuildingType type;
        public string name;
        [TextArea] public string description;
        public int unlockLevel = 1;
        [Tooltip("建てるときのコスト。強化するたびに costGrowth 倍になる")]
        public Cost cost = new Cost();
        public float costGrowth = 1.6f;
        [Header("収入（施設）")]
        public float incomePerMinute;
        [Tooltip("何が貯まるか")]
        public ResourceType produces = ResourceType.Coins;
        [Header("耐久")]
        public float maxHp = 100f;
        public float hpPerLevel = 30f;
        [Header("防衛")]
        [Tooltip("1レベルあたりの防衛力。0なら防衛装置ではない")]
        public float defensePowerPerLevel;
        public float range;
        public float damage;
        public float fireInterval;
        [Tooltip("投石台：着弾したところの周りにもダメージ")]
        public float splashRadius;
        [Tooltip("霧払いの灯：範囲内の敵の速さの倍率（1 で変化なし）")]
        public float slowFactor = 1f;

        public bool IsDefense { get { return defensePowerPerLevel > 0f; } }
        public bool IsFacility { get { return incomePerMinute > 0f; } }
    }

    /// <summary>
    /// ゲームの数値をすべてここにまとめる。
    /// メニュー「MistIsland/セットアップ」で Resources/MistIsland/GameConfig.asset が作られ、
    /// インスペクターから調整できる。アセットがなければ下の初期値で動く。
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "MistIsland/Game Config")]
    public class GameConfig : ScriptableObject
    {
        public const string ResourcePath = "MistIsland/GameConfig";

        [Header("起動")]
        [Tooltip("シーンに GameBootstrap がなくても Play 時に自動でゲームを組み立てる")]
        public bool autoBootstrap = true;

        [Header("時間（秒）")]
        public float morningSeconds = 180f;
        public float daySeconds = 180f;
        public float nightSeconds = 180f;

        [Header("島（一画面に収まる大きさ。段々の台地と崖がある）")]
        public int islandSeed = 7;
        public float baseIslandRadius = 13f;
        public float radiusPerExpansion = 2.5f;
        [Tooltip("拡張 n 段目が開放されるレベル")]
        public int[] expansionUnlockLevels = { 3, 6, 9, 12, 15 };
        public Cost expansionCost = Cost.Of(150, wood: 20, stone: 20);
        public float expansionCostGrowth = 1.8f;
        [Tooltip("台地1段の高さ")]
        public float tierHeight = 1.3f;
        [Tooltip("台地の段数の目安（大きいほど高くなる）")]
        public float tierAmount = 3.2f;
        [Tooltip("崖の険しさ（0〜1、小さいほど切り立つ）")]
        public float cliffWidth = 0.25f;

        [Header("プレイヤー")]
        public float baseMoveSpeed = 4.5f;
        public float baseMaxHp = 100f;
        public float hpPerLevel = 12f;
        public float damagePerLevel = 0.05f;
        public float speedPerLevel = 0.01f;
        public float respawnSeconds = 5f;
        public float interactRadius = 2.4f;
        public float collectRadius = 2.6f;

        [Header("経験値")]
        public int xpToLevel2 = 20;
        public float xpGrowth = 1.35f;
        public int maxLevel = 30;

        [Header("ジョブ")]
        public JobDef[] jobs =
        {
            new JobDef { name = "剣士", weapon = WeaponType.Sword, unlockLevel = 1, hpMultiplier = 1.2f, speedMultiplier = 1.0f, damageMultiplier = 1.0f, color = new Color(0.86f, 0.42f, 0.38f) },
            new JobDef { name = "槍兵", weapon = WeaponType.Spear, unlockLevel = 3, hpMultiplier = 1.0f, speedMultiplier = 1.05f, damageMultiplier = 1.0f, color = new Color(0.36f, 0.56f, 0.82f) },
            new JobDef { name = "弓兵", weapon = WeaponType.Bow, unlockLevel = 5, hpMultiplier = 0.8f, speedMultiplier = 1.15f, damageMultiplier = 1.0f, color = new Color(0.42f, 0.7f, 0.45f) },
        };

        [Header("武器（固定の攻撃モーション1つ）")]
        public WeaponDef[] weapons =
        {
            new WeaponDef { type = WeaponType.Sword, range = 1.9f, arcDegrees = 130f, motionSeconds = 0.4f, hitTime = 0.16f },
            new WeaponDef { type = WeaponType.Spear, range = 3.3f, arcDegrees = 35f, motionSeconds = 0.6f, hitTime = 0.26f },
            new WeaponDef { type = WeaponType.Bow, range = 11f, arcDegrees = 0f, motionSeconds = 0.7f, hitTime = 0.38f, projectileSpeed = 20f },
        };

        [Header("攻撃")]
        [Tooltip("近くに敵がいたら自動で攻撃する")]
        public bool autoAttack = true;
        [Tooltip("画面長押しで溜め始めるまでの時間（秒）")]
        public float chargeStartDelay = 0.2f;
        [Tooltip("溜め攻撃になるまで溜める時間（秒）")]
        public float chargeSeconds = 0.7f;
        public float chargeDamageMultiplier = 3f;
        public float chargeRangeMultiplier = 1.6f;
        [Tooltip("溜めている間の移動速度の倍率")]
        public float chargeMoveMultiplier = 0.4f;

        [Header("装備（ベースキャンプで作る・強化する・装備する）")]
        public WeaponItemDef[] weaponItems =
        {
            new WeaponItemDef { id = "sword_wood", name = "木の剣", type = WeaponType.Sword, damage = 12f, unlockLevel = 1 },
            new WeaponItemDef { id = "sword_stone", name = "石の大剣", type = WeaponType.Sword, damage = 16f, unlockLevel = 3, cost = Cost.Of(60, wood: 5, stone: 12) },
            new WeaponItemDef { id = "sword_iron", name = "鉄の剣", type = WeaponType.Sword, damage = 22f, unlockLevel = 5, cost = Cost.Of(150, wood: 5, iron: 12) },
            new WeaponItemDef { id = "sword_mist", name = "霧鋼の剣", type = WeaponType.Sword, damage = 32f, unlockLevel = 9, cost = Cost.Of(400, iron: 25, crystal: 6) },
            new WeaponItemDef { id = "sword_crystal", name = "結晶の剣", type = WeaponType.Sword, damage = 46f, unlockLevel = 14, cost = Cost.Of(900, iron: 30, crystal: 25) },

            new WeaponItemDef { id = "spear_wood", name = "木の槍", type = WeaponType.Spear, damage = 15f, unlockLevel = 1 },
            new WeaponItemDef { id = "spear_stone", name = "石の槍", type = WeaponType.Spear, damage = 19f, unlockLevel = 4, cost = Cost.Of(80, wood: 10, stone: 10) },
            new WeaponItemDef { id = "spear_iron", name = "鉄の槍", type = WeaponType.Spear, damage = 26f, unlockLevel = 6, cost = Cost.Of(180, wood: 8, iron: 14) },
            new WeaponItemDef { id = "spear_mist", name = "霧鋼の槍", type = WeaponType.Spear, damage = 38f, unlockLevel = 10, cost = Cost.Of(450, iron: 28, crystal: 8) },
            new WeaponItemDef { id = "spear_crystal", name = "結晶の槍", type = WeaponType.Spear, damage = 54f, unlockLevel = 15, cost = Cost.Of(1000, iron: 32, crystal: 28) },

            new WeaponItemDef { id = "bow_short", name = "短弓", type = WeaponType.Bow, damage = 10f, unlockLevel = 1 },
            new WeaponItemDef { id = "bow_composite", name = "合成弓", type = WeaponType.Bow, damage = 13f, unlockLevel = 5, cost = Cost.Of(90, wood: 15, stone: 4) },
            new WeaponItemDef { id = "bow_long", name = "長弓", type = WeaponType.Bow, damage = 18f, unlockLevel = 7, cost = Cost.Of(200, wood: 20, iron: 8) },
            new WeaponItemDef { id = "bow_mist", name = "霧の弓", type = WeaponType.Bow, damage = 27f, unlockLevel = 11, cost = Cost.Of(500, wood: 20, crystal: 10) },
            new WeaponItemDef { id = "bow_crystal", name = "結晶の弓", type = WeaponType.Bow, damage = 40f, unlockLevel = 16, cost = Cost.Of(1100, iron: 20, crystal: 30) },
        };
        public ArmorItemDef[] armorItems =
        {
            new ArmorItemDef { id = "armor_cloth", name = "布の服", damageReduction = 0f, bonusHp = 0f, unlockLevel = 1 },
            new ArmorItemDef { id = "armor_leather", name = "革の鎧", damageReduction = 0.08f, bonusHp = 15f, unlockLevel = 2, cost = Cost.Of(50, wood: 6) },
            new ArmorItemDef { id = "armor_stone", name = "石鱗の鎧", damageReduction = 0.14f, bonusHp = 30f, unlockLevel = 4, cost = Cost.Of(100, stone: 15) },
            new ArmorItemDef { id = "armor_iron", name = "鉄の鎧", damageReduction = 0.22f, bonusHp = 50f, unlockLevel = 7, cost = Cost.Of(250, iron: 20) },
            new ArmorItemDef { id = "armor_mist", name = "霧鋼の鎧", damageReduction = 0.32f, bonusHp = 90f, unlockLevel = 11, cost = Cost.Of(600, iron: 30, crystal: 10) },
            new ArmorItemDef { id = "armor_crystal", name = "結晶の鎧", damageReduction = 0.42f, bonusHp = 140f, unlockLevel = 16, cost = Cost.Of(1200, iron: 30, crystal: 35) },
        };
        public int maxEquipLevel = 10;
        public float weaponDamagePerLevel = 0.12f;
        public float armorPerLevel = 0.08f;
        public float maxArmorReduction = 0.6f;
        [Tooltip("装備強化のコスト：作るときのコストのこの割合 ＋ 下の基本コスト（レベルごとに equipCostGrowth 倍）")]
        public float upgradeCostRatio = 0.3f;
        public Cost upgradeBaseCost = Cost.Of(30, wood: 3);
        public float equipCostGrowth = 1.5f;

        [Header("敵")]
        public EnemyDef[] enemies =
        {
            new EnemyDef { name = "霧の小鬼", unlockDay = 1, hp = 24f, damage = 6f, speed = 2.2f, attackRange = 0.9f, attackCooldown = 1.1f, scale = 0.8f, xp = 3, drops = Cost.Of(1, wood: 2, stone: 1), spawnWeight = 1f, color = new Color(0.35f, 0.33f, 0.48f) },
            new EnemyDef { name = "霧の走り屋", unlockDay = 3, hp = 16f, damage = 5f, speed = 3.6f, attackRange = 0.9f, attackCooldown = 0.8f, scale = 0.7f, xp = 4, drops = Cost.Of(1, wood: 3), spawnWeight = 0.8f, color = new Color(0.45f, 0.36f, 0.55f) },
            new EnemyDef { name = "霧の重装兵", unlockDay = 5, hp = 90f, damage = 14f, speed = 1.5f, attackRange = 1.2f, attackCooldown = 1.6f, scale = 1.2f, xp = 10, drops = Cost.Of(3, stone: 3, iron: 3), spawnWeight = 0.45f, prefersStructures = true, color = new Color(0.28f, 0.3f, 0.38f) },
            new EnemyDef { name = "霧の術師", unlockDay = 7, hp = 40f, damage = 10f, speed = 2.4f, attackRange = 1f, attackCooldown = 1.2f, scale = 0.9f, xp = 8, drops = Cost.Of(2, iron: 1, crystal: 1), spawnWeight = 0.35f, color = new Color(0.4f, 0.48f, 0.62f) },
            new EnemyDef { name = "霧の巨人", unlockDay = 10, hp = 320f, damage = 30f, speed = 1.2f, attackRange = 1.8f, attackCooldown = 2.2f, scale = 1.9f, xp = 40, drops = Cost.Of(10, stone: 6, iron: 6, crystal: 4), spawnWeight = 0.15f, prefersStructures = true, color = new Color(0.22f, 0.22f, 0.3f) },
        };

        [Header("敵の出現")]
        public float firstSpawnDelay = 6f;
        public float spawnIntervalBase = 7f;
        public float spawnIntervalMin = 1.5f;
        public float spawnIntervalDayFactor = 0.15f;
        [Tooltip("夜明け何秒前から新しい敵が来なくなるか")]
        public float stopSpawnBeforeDawn = 15f;
        public int maxAliveEnemies = 40;
        public float enemyHpGrowthPerDay = 0.2f;
        public float enemyDamageGrowthPerDay = 0.12f;
        public float enemyAggroRadius = 6f;

        [Header("施設・防衛装置")]
        public float hallMaxHp = 400f;
        public int buildingMaxLevel = 10;
        [Tooltip("施設に貯められる収入（何分ぶんか）")]
        public float facilityCapMinutes = 180f;
        public BuildingDef[] buildings =
        {
            new BuildingDef { type = BuildingType.Bank, name = "銀行", description = "コインが少しずつ貯まる", unlockLevel = 1, cost = Cost.Of(30), costGrowth = 1.7f, incomePerMinute = 12f, maxHp = 120f, hpPerLevel = 30f },
            new BuildingDef { type = BuildingType.LumberMill, name = "伐採所", description = "木材が少しずつ貯まる", unlockLevel = 1, cost = Cost.Of(40), costGrowth = 1.6f, incomePerMinute = 3f, produces = ResourceType.Wood, maxHp = 120f, hpPerLevel = 30f },
            new BuildingDef { type = BuildingType.Watchtower, name = "見張り塔", description = "近づく敵に矢を放つ", unlockLevel = 2, cost = Cost.Of(60, wood: 10), costGrowth = 1.6f, maxHp = 150f, hpPerLevel = 50f, defensePowerPerLevel = 5f, range = 8f, damage = 8f, fireInterval = 1.4f },
            new BuildingDef { type = BuildingType.Quarry, name = "石切り場", description = "石材が少しずつ貯まる", unlockLevel = 3, cost = Cost.Of(80, wood: 10), costGrowth = 1.6f, incomePerMinute = 3f, produces = ResourceType.Stone, maxHp = 160f, hpPerLevel = 40f },
            new BuildingDef { type = BuildingType.Fence, name = "柵", description = "頑丈で敵を引きつける", unlockLevel = 4, cost = Cost.Of(40, wood: 12, stone: 4), costGrowth = 1.5f, maxHp = 400f, hpPerLevel = 150f, defensePowerPerLevel = 3f },
            new BuildingDef { type = BuildingType.Farm, name = "畑", description = "銀行より多くのコインを生む", unlockLevel = 5, cost = Cost.Of(150, wood: 15), costGrowth = 1.7f, incomePerMinute = 30f, maxHp = 100f, hpPerLevel = 30f },
            new BuildingDef { type = BuildingType.Catapult, name = "投石台", description = "岩を投げ、着弾点の周りの敵をまとめて攻撃", unlockLevel = 7, cost = Cost.Of(200, wood: 25, stone: 20), costGrowth = 1.6f, maxHp = 180f, hpPerLevel = 60f, defensePowerPerLevel = 8f, range = 10f, damage = 18f, fireInterval = 3.2f, splashRadius = 2.2f },
            new BuildingDef { type = BuildingType.Mine, name = "鉱山", description = "鉄が少しずつ貯まる", unlockLevel = 8, cost = Cost.Of(300, wood: 20, stone: 25), costGrowth = 1.8f, incomePerMinute = 2f, produces = ResourceType.Iron, maxHp = 200f, hpPerLevel = 60f },
            new BuildingDef { type = BuildingType.Lantern, name = "霧払いの灯", description = "周りの敵の動きを遅くする", unlockLevel = 10, cost = Cost.Of(350, stone: 20, iron: 10), costGrowth = 1.6f, maxHp = 140f, hpPerLevel = 40f, defensePowerPerLevel = 6f, range = 6f, slowFactor = 0.5f },
            new BuildingDef { type = BuildingType.MistWell, name = "霧の井戸", description = "霧の結晶がほんの少しずつ貯まる", unlockLevel = 12, cost = Cost.Of(600, stone: 30, iron: 20), costGrowth = 1.8f, incomePerMinute = 0.8f, produces = ResourceType.Crystal, maxHp = 200f, hpPerLevel = 60f },
        };

        [Header("放置")]
        public float maxOfflineHours = 12f;
        public OfflineBreachRule offlineBreachRule = OfflineBreachRule.KeepUntilBreach;
        public float offlineEnemyBaseStrength = 6f;
        [Tooltip("1日ごとに夜の敵の強さが何倍になるか")]
        public float offlineEnemyGrowthPerDay = 1.18f;
        [Tooltip("放置中に夜を守り切ったときの素材（木材と石材に半分ずつ）")]
        public float offlineMaterialsPerNightBase = 4f;
        public float offlineMaterialsPerDay = 1.5f;
        [Tooltip("これより短い不在は放置として扱わない（秒）")]
        public float minOfflineSeconds = 10f;

        [Header("セーブ")]
        public float autosaveSeconds = 20f;
        public int startingCoins = 40;
        public int startingWood = 10;

        public DayDurations Durations
        {
            get { return new DayDurations(morningSeconds, daySeconds, nightSeconds); }
        }

        public JobDef Job(int index)
        {
            return jobs[Mathf.Clamp(index, 0, jobs.Length - 1)];
        }

        public WeaponDef Weapon(WeaponType type)
        {
            foreach (var w in weapons)
                if (w.type == type) return w;
            return weapons[0];
        }

        public WeaponItemDef WeaponItem(string id)
        {
            foreach (var w in weaponItems)
                if (w.id == id) return w;
            return null;
        }

        public ArmorItemDef ArmorItem(string id)
        {
            foreach (var a in armorItems)
                if (a.id == id) return a;
            return null;
        }

        /// <summary>その種類の最初から持っている武器。</summary>
        public WeaponItemDef StarterWeapon(WeaponType type)
        {
            foreach (var w in weaponItems)
                if (w.type == type && (w.cost == null || w.cost.IsFree)) return w;
            foreach (var w in weaponItems)
                if (w.type == type) return w;
            return null;
        }

        public ArmorItemDef StarterArmor
        {
            get
            {
                foreach (var a in armorItems)
                    if (a.cost == null || a.cost.IsFree) return a;
                return armorItems.Length > 0 ? armorItems[0] : null;
            }
        }

        public BuildingDef Building(BuildingType type)
        {
            foreach (var b in buildings)
                if (b.type == type) return b;
            return null;
        }

        public float IslandRadius(int expansion)
        {
            return baseIslandRadius + radiusPerExpansion * expansion;
        }

        public int MaxExpansion { get { return expansionUnlockLevels.Length; } }

        void OnValidate()
        {
            morningSeconds = Mathf.Max(1f, morningSeconds);
            daySeconds = Mathf.Max(1f, daySeconds);
            nightSeconds = Mathf.Max(1f, nightSeconds);
        }

        static GameConfig _cached;

        public static GameConfig Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<GameConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<GameConfig>();
                _cached.name = "GameConfig (初期値)";
            }
            return _cached;
        }
    }
}
