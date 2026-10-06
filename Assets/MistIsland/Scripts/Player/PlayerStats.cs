using UnityEngine;

namespace MistIsland
{
    /// <summary>ジョブ・レベル・装備から決まるプレイヤーの能力値。</summary>
    public struct PlayerStats
    {
        public JobDef job;
        public WeaponDef weapon;
        public WeaponItemDef weaponItem;
        public int weaponLevel;
        public ArmorItemDef armorItem;
        public int armorLevel;
        public float maxHp;
        public float moveSpeed;
        public float damage;
        public float damageReduction;

        public static PlayerStats Compute(GameConfig config, SaveData data)
        {
            JobDef job = config.Job(data.jobIndex);
            WeaponDef weapon = config.Weapon(job.weapon);
            int level = Mathf.Max(1, data.level);

            WeaponItemDef weaponItem = config.WeaponItem(data.EquippedWeaponId(job.weapon)) ?? config.StarterWeapon(job.weapon);
            OwnedItem ownedWeapon = weaponItem != null ? data.FindWeapon(weaponItem.id) : null;
            int weaponLevel = ownedWeapon != null ? Mathf.Max(1, ownedWeapon.level) : 1;

            ArmorItemDef armorItem = config.ArmorItem(data.equippedArmor) ?? config.StarterArmor;
            OwnedItem ownedArmor = armorItem != null ? data.FindArmor(armorItem.id) : null;
            int armorLevel = ownedArmor != null ? Mathf.Max(1, ownedArmor.level) : 1;

            float baseDamage = weaponItem != null ? weaponItem.damage : 10f;
            float armorScale = 1f + config.armorPerLevel * (armorLevel - 1);
            float reduction = armorItem != null ? armorItem.damageReduction * armorScale : 0f;
            float bonusHp = armorItem != null ? armorItem.bonusHp * armorScale : 0f;

            return new PlayerStats
            {
                job = job,
                weapon = weapon,
                weaponItem = weaponItem,
                weaponLevel = weaponLevel,
                armorItem = armorItem,
                armorLevel = armorLevel,
                maxHp = (config.baseMaxHp + config.hpPerLevel * (level - 1)) * job.hpMultiplier + bonusHp,
                moveSpeed = config.baseMoveSpeed * job.speedMultiplier * (1f + config.speedPerLevel * (level - 1)),
                damage = baseDamage * job.damageMultiplier
                         * (1f + config.weaponDamagePerLevel * (weaponLevel - 1))
                         * (1f + config.damagePerLevel * (level - 1)),
                damageReduction = Mathf.Min(config.maxArmorReduction, reduction),
            };
        }
    }
}
