using System;
using System.Collections.Generic;

namespace MistIsland
{
    [Serializable]
    public class BuildingSave
    {
        public int slotId;
        public int type;
        public int level;
        public float stored;
    }

    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public int coins;
        public int materials;
        public int level = 1;
        public int xp;
        public int jobIndex;
        public int[] weaponLevels = { 1, 1, 1 };
        public int armorLevel;
        public int expansion;
        public int day = 1;
        public float cycleTime;
        public long lastSavedUtcTicks;
        public List<BuildingSave> buildings = new List<BuildingSave>();

        public int WeaponLevel(WeaponType type)
        {
            int i = (int)type;
            if (weaponLevels == null || weaponLevels.Length <= i) return 1;
            return Math.Max(1, weaponLevels[i]);
        }

        public void SetWeaponLevel(WeaponType type, int value)
        {
            int i = (int)type;
            if (weaponLevels == null || weaponLevels.Length < 3)
            {
                var old = weaponLevels ?? new int[0];
                weaponLevels = new[] { 1, 1, 1 };
                Array.Copy(old, weaponLevels, Math.Min(old.Length, 3));
            }
            weaponLevels[i] = value;
        }
    }
}
