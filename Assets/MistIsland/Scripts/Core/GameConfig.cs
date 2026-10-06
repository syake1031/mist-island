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
        [Tooltip("0 なら最初から持っている")]
        public int coinCost;
        public int materialCost;
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
        public int coinCost;
        public int materialCost;
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
        public int materialMin;
        public int materialMax;
        public int coins;
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
        public int baseCoinCost;
        public int baseMaterialCost;
        public float costGrowth = 1.6f;
        [Header("収入（施設）")]
        public float incomePerMinute;
        [Tooltip("コインではなく素材を生む")]
        public bool producesMaterials;
        [Header("耐久")]
        public float maxHp = 100f;
        public float hpPerLevel = 30f;
        [Header("防衛")]
        [Tooltip("1レベルあたりの防衛力。0なら防衛装置ではない")]
        public float defensePowerPerLevel;
        public float range;
        public float damage;
        public float fireInterval;

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

        [Header("島")]
        public int islandSeed = 7;
        public float baseIslandRadius = 30f;
        public float radiusPerExpansion = 6f;
        [Tooltip("建物を置ける範囲（町）の半径")]
        public float townBaseRadius = 12f;
        public float townRadiusPerExpansion = 4f;
        [Tooltip("拡張 n 段目が開放されるレベル")]
        public int[] expansionUnlockLevels = { 3, 6, 9, 12, 15 };
        public int expansionBaseCost = 200;
        public int expansionBaseMaterialCost = 20;
        public float expansionCostGrowth = 2f;
        public float landHeight = 1.4f;
        [Header("山")]
        public int mountainCount = 5;
        public float mountainMinHeight = 4f;
        public float mountainMaxHeight = 9f;
        [Tooltip("島の中心から山までの距離")]
        public float mountainMinDistance = 13f;
        public float mountainMaxDistance = 25f;
        public float mountainMinRadius = 4f;
        public float mountainMaxRadius = 7f;

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
            new WeaponItemDef { id = "sword_iron", name = "鉄の剣", type = WeaponType.Sword, damage = 18f, unlockLevel = 4, coinCost = 120, materialCost = 15 },
            new WeaponItemDef { id = "sword_mist", name = "霧鋼の剣", type = WeaponType.Sword, damage = 28f, unlockLevel = 9, coinCost = 450, materialCost = 50 },
            new WeaponItemDef { id = "spear_wood", name = "木の槍", type = WeaponType.Spear, damage = 15f, unlockLevel = 1 },
            new WeaponItemDef { id = "spear_iron", name = "鉄の槍", type = WeaponType.Spear, damage = 22f, unlockLevel = 5, coinCost = 150, materialCost = 18 },
            new WeaponItemDef { id = "spear_mist", name = "霧鋼の槍", type = WeaponType.Spear, damage = 34f, unlockLevel = 10, coinCost = 520, materialCost = 55 },
            new WeaponItemDef { id = "bow_short", name = "短弓", type = WeaponType.Bow, damage = 10f, unlockLevel = 1 },
            new WeaponItemDef { id = "bow_long", name = "長弓", type = WeaponType.Bow, damage = 15f, unlockLevel = 6, coinCost = 160, materialCost = 18 },
            new WeaponItemDef { id = "bow_mist", name = "霧の弓", type = WeaponType.Bow, damage = 24f, unlockLevel = 11, coinCost = 560, materialCost = 60 },
        };
        public ArmorItemDef[] armorItems =
        {
            new ArmorItemDef { id = "armor_cloth", name = "布の服", damageReduction = 0f, bonusHp = 0f, unlockLevel = 1 },
            new ArmorItemDef { id = "armor_leather", name = "革の鎧", damageReduction = 0.1f, bonusHp = 15f, unlockLevel = 2, coinCost = 60, materialCost = 8 },
            new ArmorItemDef { id = "armor_iron", name = "鉄の鎧", damageReduction = 0.2f, bonusHp = 40f, unlockLevel = 6, coinCost = 220, materialCost = 30 },
            new ArmorItemDef { id = "armor_mist", name = "霧鋼の鎧", damageReduction = 0.32f, bonusHp = 80f, unlockLevel = 10, coinCost = 600, materialCost = 70 },
        };
        public int maxEquipLevel = 10;
        public float weaponDamagePerLevel = 0.12f;
        public float armorPerLevel = 0.08f;
        public float maxArmorReduction = 0.6f;
        public int upgradeCoinBase = 30;
        public int upgradeMaterialBase = 4;
        public float equipCostGrowth = 1.5f;

        [Header("敵")]
        public EnemyDef[] enemies =
        {
            new EnemyDef { name = "霧の小鬼", unlockDay = 1, hp = 24f, damage = 6f, speed = 2.2f, attackRange = 0.9f, attackCooldown = 1.1f, scale = 0.8f, xp = 3, materialMin = 0, materialMax = 1, coins = 1, spawnWeight = 1f, color = new Color(0.35f, 0.33f, 0.48f) },
            new EnemyDef { name = "霧の走り屋", unlockDay = 3, hp = 16f, damage = 5f, speed = 3.6f, attackRange = 0.9f, attackCooldown = 0.8f, scale = 0.7f, xp = 4, materialMin = 0, materialMax = 2, coins = 1, spawnWeight = 0.8f, color = new Color(0.45f, 0.36f, 0.55f) },
            new EnemyDef { name = "霧の重装兵", unlockDay = 5, hp = 90f, damage = 14f, speed = 1.5f, attackRange = 1.2f, attackCooldown = 1.6f, scale = 1.2f, xp = 10, materialMin = 2, materialMax = 4, coins = 3, spawnWeight = 0.45f, prefersStructures = true, color = new Color(0.28f, 0.3f, 0.38f) },
            new EnemyDef { name = "霧の巨人", unlockDay = 10, hp = 320f, damage = 30f, speed = 1.2f, attackRange = 1.8f, attackCooldown = 2.2f, scale = 1.9f, xp = 40, materialMin = 6, materialMax = 10, coins = 10, spawnWeight = 0.15f, prefersStructures = true, color = new Color(0.22f, 0.22f, 0.3f) },
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
            new BuildingDef { type = BuildingType.Bank, name = "銀行", description = "コインが少しずつ貯まる", unlockLevel = 1, baseCoinCost = 30, baseMaterialCost = 0, costGrowth = 1.7f, incomePerMinute = 12f, maxHp = 120f, hpPerLevel = 30f },
            new BuildingDef { type = BuildingType.Watchtower, name = "見張り塔", description = "近づく敵に矢を放つ", unlockLevel = 2, baseCoinCost = 60, baseMaterialCost = 8, costGrowth = 1.6f, maxHp = 150f, hpPerLevel = 50f, defensePowerPerLevel = 5f, range = 8f, damage = 8f, fireInterval = 1.4f },
            new BuildingDef { type = BuildingType.Fence, name = "柵", description = "頑丈で敵を引きつける", unlockLevel = 4, baseCoinCost = 40, baseMaterialCost = 12, costGrowth = 1.5f, maxHp = 400f, hpPerLevel = 150f, defensePowerPerLevel = 3f },
            new BuildingDef { type = BuildingType.Farm, name = "畑", description = "銀行より多くのコインを生む", unlockLevel = 5, baseCoinCost = 150, baseMaterialCost = 10, costGrowth = 1.7f, incomePerMinute = 30f, maxHp = 100f, hpPerLevel = 30f },
            new BuildingDef { type = BuildingType.Mine, name = "鉱山", description = "素材が少しずつ貯まる", unlockLevel = 8, baseCoinCost = 400, baseMaterialCost = 30, costGrowth = 1.8f, incomePerMinute = 4f, producesMaterials = true, maxHp = 200f, hpPerLevel = 60f },
        };

        [Header("放置")]
        public float maxOfflineHours = 12f;
        public OfflineBreachRule offlineBreachRule = OfflineBreachRule.KeepUntilBreach;
        public float offlineEnemyBaseStrength = 6f;
        [Tooltip("1日ごとに夜の敵の強さが何倍になるか")]
        public float offlineEnemyGrowthPerDay = 1.18f;
        public float offlineMaterialsPerNightBase = 4f;
        public float offlineMaterialsPerDay = 1.5f;
        [Tooltip("これより短い不在は放置として扱わない（秒）")]
        public float minOfflineSeconds = 10f;

        [Header("セーブ")]
        public float autosaveSeconds = 20f;
        public int startingCoins = 30;

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
                if (w.type == type && w.coinCost <= 0 && w.materialCost <= 0) return w;
            foreach (var w in weaponItems)
                if (w.type == type) return w;
            return null;
        }

        public ArmorItemDef StarterArmor
        {
            get
            {
                foreach (var a in armorItems)
                    if (a.coinCost <= 0 && a.materialCost <= 0) return a;
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

        public float TownRadius(int expansion)
        {
            return townBaseRadius + townRadiusPerExpansion * expansion;
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
