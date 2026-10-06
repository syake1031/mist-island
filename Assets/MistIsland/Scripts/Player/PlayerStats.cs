using UnityEngine;

namespace MistIsland
{
    /// <summary>ジョブ・レベル・装備から決まるプレイヤーの能力値。</summary>
    public struct PlayerStats
    {
        public JobDef job;
        public WeaponDef weapon;
        public float maxHp;
        public float moveSpeed;
        public float damage;
        public float damageReduction;

        public static PlayerStats Compute(GameConfig config, SaveData data)
        {
            JobDef job = config.Job(data.jobIndex);
            WeaponDef weapon = config.Weapon(job.weapon);
            int level = Mathf.Max(1, data.level);
            int weaponLevel = data.WeaponLevel(job.weapon);

            return new PlayerStats
            {
                job = job,
                weapon = weapon,
                maxHp = (config.baseMaxHp + config.hpPerLevel * (level - 1)) * job.hpMultiplier,
                moveSpeed = config.baseMoveSpeed * job.speedMultiplier * (1f + config.speedPerLevel * (level - 1)),
                damage = weapon.damage * job.damageMultiplier
                         * (1f + config.weaponDamagePerLevel * (weaponLevel - 1))
                         * (1f + config.damagePerLevel * (level - 1)),
                damageReduction = Mathf.Min(config.maxArmorReduction, config.armorReductionPerLevel * data.armorLevel),
            };
        }
    }
}
