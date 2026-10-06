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

    /// <summary>持っている装備と、その強化レベル。</summary>
    [Serializable]
    public class OwnedItem
    {
        public string id;
        public int level = 1;
    }

    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 3;

        public int version = CurrentVersion;
        public int coins;
        /// <summary>木材・石材・鉄・霧の結晶の所持数（ResourceType.Wood から順に）。</summary>
        public int[] materials = new int[4];
        public int level = 1;
        public int xp;
        public int jobIndex;
        public int expansion;
        public int day = 1;
        public float cycleTime;
        public long lastSavedUtcTicks;
        public List<BuildingSave> buildings = new List<BuildingSave>();

        public List<OwnedItem> weapons = new List<OwnedItem>();
        public List<OwnedItem> armors = new List<OwnedItem>();
        /// <summary>武器の種類（剣・槍・弓）ごとに装備している武器の ID。</summary>
        public string[] equippedWeapons = { "", "", "" };
        public string equippedArmor = "";

        public int Get(ResourceType type)
        {
            if (type == ResourceType.Coins) return coins;
            int i = (int)type - 1;
            return materials != null && i < materials.Length ? materials[i] : 0;
        }

        public void Add(ResourceType type, int amount)
        {
            if (type == ResourceType.Coins)
            {
                coins += amount;
                return;
            }
            int i = (int)type - 1;
            if (materials == null || materials.Length < 4)
            {
                var old = materials ?? new int[0];
                materials = new int[4];
                Array.Copy(old, materials, Math.Min(old.Length, 4));
            }
            materials[i] += amount;
        }

        public OwnedItem FindWeapon(string id)
        {
            return Find(weapons, id);
        }

        public OwnedItem FindArmor(string id)
        {
            return Find(armors, id);
        }

        static OwnedItem Find(List<OwnedItem> list, string id)
        {
            if (list == null || string.IsNullOrEmpty(id)) return null;
            foreach (var item in list)
                if (item.id == id) return item;
            return null;
        }

        public string EquippedWeaponId(WeaponType type)
        {
            int i = (int)type;
            if (equippedWeapons == null || equippedWeapons.Length <= i) return "";
            return equippedWeapons[i] ?? "";
        }

        public void SetEquippedWeapon(WeaponType type, string id)
        {
            if (equippedWeapons == null || equippedWeapons.Length < 3)
            {
                var old = equippedWeapons ?? new string[0];
                equippedWeapons = new[] { "", "", "" };
                Array.Copy(old, equippedWeapons, Math.Min(old.Length, 3));
            }
            equippedWeapons[(int)type] = id;
        }
    }
}
