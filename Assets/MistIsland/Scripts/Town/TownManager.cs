using System.Collections.Generic;
using UnityEngine;

namespace MistIsland
{
    /// <summary>プレイヤーの近くで調べられるもの（空き地・建物・拠点）。</summary>
    public struct Interaction
    {
        public bool valid;
        public bool isHall;
        public SlotInfo slot;
        public Building building;
    }

    /// <summary>
    /// 町（拠点・施設・防衛装置）の管理。建設・強化・修理・収入の受け取り・防衛力の計算を受け持つ。
    /// </summary>
    public class TownManager : MonoBehaviour
    {
        public Health Hall { get; private set; }

        readonly Dictionary<int, Building> _buildings = new Dictionary<int, Building>();
        readonly Dictionary<int, GameObject> _markers = new Dictionary<int, GameObject>();
        readonly List<Health> _targets = new List<Health>();
        List<SlotInfo> _slots = new List<SlotInfo>();
        GameConfig _config;
        Island _island;
        Transform _hallVisual;
        float _collectTimer;

        public IEnumerable<Building> Buildings { get { return _buildings.Values; } }

        public void Initialize(GameConfig config, Island island, List<BuildingSave> saved)
        {
            _config = config;
            _island = island;
            BuildHall();
            RefreshSlots();

            if (saved != null)
            {
                foreach (var b in saved)
                {
                    BuildingDef def = config.Building((BuildingType)b.type);
                    if (def == null) continue;
                    SlotInfo slot;
                    if (!TryGetSlot(b.slotId, out slot)) continue;
                    Place(def, slot, b.level, b.stored);
                }
            }
            RefreshMarkers();
        }

        // ---- 拠点 ----

        void BuildHall()
        {
            var go = new GameObject("TownHall");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, _island.HeightAt(0f, 0f), 0f);
            Hall = go.AddComponent<Health>();
            Hall.Radius = TownLayout.HallRadius;
            Hall.Init(_config.hallMaxHp);
            Hall.Died += OnHallDestroyed;
            BuildHallVisual(false);
        }

        void BuildHallVisual(bool ruined)
        {
            if (_hallVisual != null) Destroy(_hallVisual.gameObject);
            _hallVisual = new GameObject("Visual").transform;
            _hallVisual.SetParent(Hall.transform, false);
            // ベースキャンプ：テント・たき火・道具箱・旗
            var canvas = new Color(0.95f, 0.9f, 0.8f);
            var stripe = new Color(0.86f, 0.52f, 0.44f);
            var wood = new Color(0.55f, 0.42f, 0.33f);
            Shapes.Create(PrimitiveType.Cylinder, _hallVisual, new Vector3(0, 0.03f, 0), new Vector3(4.2f, 0.03f, 4.2f), new Color(0.78f, 0.72f, 0.62f));
            Shapes.Cone(_hallVisual, new Vector3(0.6f, 0f, 0.8f), new Vector3(3f, 2.6f, 3f), canvas);
            Shapes.Cone(_hallVisual, new Vector3(0.6f, 1.6f, 0.8f), new Vector3(1.2f, 1.05f, 1.2f), stripe);
            Shapes.Create(PrimitiveType.Cube, _hallVisual, new Vector3(0.6f, 0.45f, -0.55f), new Vector3(0.7f, 0.9f, 0.1f), new Color(0.35f, 0.3f, 0.3f));
            // たき火
            for (int i = 0; i < 3; i++)
            {
                var log = Shapes.Create(PrimitiveType.Cylinder, _hallVisual, new Vector3(-1.1f, 0.1f, -0.9f), new Vector3(0.12f, 0.4f, 0.12f), wood);
                log.transform.localRotation = Quaternion.Euler(90f, i * 60f, 0f);
            }
            var fire = Shapes.Cone(_hallVisual, new Vector3(-1.1f, 0.12f, -0.9f), new Vector3(0.45f, 0.7f, 0.45f), new Color(1f, 0.6f, 0.3f), "Fire");
            var glow = new MaterialPropertyBlock();
            glow.SetFloat("_Emission", 1.5f);
            fire.GetComponent<Renderer>().SetPropertyBlock(glow);
            // 道具箱（装備を変える場所）
            Shapes.Create(PrimitiveType.Cube, _hallVisual, new Vector3(1.3f, 0.3f, -1.2f), new Vector3(0.9f, 0.6f, 0.6f), wood);
            Shapes.Create(PrimitiveType.Cube, _hallVisual, new Vector3(1.3f, 0.62f, -1.2f), new Vector3(0.95f, 0.08f, 0.65f), new Color(0.95f, 0.78f, 0.4f));
            // 旗
            Shapes.Create(PrimitiveType.Cylinder, _hallVisual, new Vector3(-1.4f, 1.6f, 1.2f), new Vector3(0.08f, 1.6f, 0.08f), wood);
            Shapes.Create(PrimitiveType.Cube, _hallVisual, new Vector3(-1.05f, 2.9f, 1.2f), new Vector3(0.7f, 0.42f, 0.04f), new Color(0.5f, 0.62f, 0.85f));
            if (ruined)
            {
                _hallVisual.localScale = new Vector3(1f, 0.5f, 1f);
                var block = new MaterialPropertyBlock();
                Shapes.Tint(_hallVisual.GetComponentsInChildren<Renderer>(), block, -0.45f);
            }
        }

        void OnHallDestroyed()
        {
            BuildHallVisual(true);
            int lost = 0;
            foreach (var b in _buildings.Values)
            {
                if (!b.Def.IsFacility) continue;
                lost += Mathf.FloorToInt(b.Stored);
                b.ClearStored();
            }
            var gm = GameManager.Instance;
            if (gm != null)
                gm.Toast(lost > 0 ? "拠点が壊された！施設に貯まっていた収入が奪われた" : "拠点が壊された！朝まで持ちこたえよう");
        }

        // ---- 空き地 ----

        public void RefreshSlots()
        {
            _slots = TownLayout.AvailableSlots(_island);
            if (Hall != null) Hall.transform.position = new Vector3(0f, _island.HeightAt(0f, 0f), 0f);
            foreach (var s in _slots)
            {
                Building b;
                if (_buildings.TryGetValue(s.id, out b)) b.MoveTo(s);
            }
            RefreshMarkers();
        }

        bool TryGetSlot(int id, out SlotInfo slot)
        {
            foreach (var s in _slots)
            {
                if (s.id == id)
                {
                    slot = s;
                    return true;
                }
            }
            slot = default(SlotInfo);
            return false;
        }

        void RefreshMarkers()
        {
            var keep = new HashSet<int>();
            foreach (var s in _slots)
            {
                if (_buildings.ContainsKey(s.id)) continue;
                keep.Add(s.id);
                GameObject marker;
                if (!_markers.TryGetValue(s.id, out marker) || marker == null)
                {
                    marker = Shapes.Create(PrimitiveType.Cylinder, transform, s.position + Vector3.up * 0.03f, new Vector3(2.2f, 0.03f, 2.2f), new Color(0.9f, 0.88f, 0.78f), "Slot" + s.id);
                    _markers[s.id] = marker;
                }
                else
                {
                    marker.transform.position = s.position + Vector3.up * 0.03f;
                }
            }

            var remove = new List<int>();
            foreach (var kv in _markers)
                if (!keep.Contains(kv.Key)) remove.Add(kv.Key);
            foreach (int id in remove)
            {
                if (_markers[id] != null) Destroy(_markers[id]);
                _markers.Remove(id);
            }
        }

        // ---- 建設・強化 ----

        Building Place(BuildingDef def, SlotInfo slot, int level, float stored)
        {
            var go = new GameObject(def.name);
            go.transform.SetParent(transform, false);
            var b = go.AddComponent<Building>();
            b.Setup(def, slot, level, stored, _config);
            _buildings[slot.id] = b;
            return b;
        }

        public static int BuildCoinCost(BuildingDef def) { return def.baseCoinCost; }
        public static int BuildMaterialCost(BuildingDef def) { return def.baseMaterialCost; }

        public bool TryBuild(SlotInfo slot, BuildingDef def, out string error)
        {
            var gm = GameManager.Instance;
            error = null;
            if (_buildings.ContainsKey(slot.id)) { error = "ここにはもう建っています"; return false; }
            if (!gm.IsPrepTime) { error = "夜は建てられません"; return false; }
            if (gm.Data.level < def.unlockLevel) { error = "Lv" + def.unlockLevel + "で開放"; return false; }
            if (!gm.TrySpend(BuildCoinCost(def), BuildMaterialCost(def))) { error = "コインか素材が足りません"; return false; }

            Place(def, slot, 1, 0f);
            RefreshMarkers();
            gm.NotifyChanged();
            gm.Toast(def.name + "を建てた");
            return true;
        }

        public bool TryUpgrade(Building b, out string error)
        {
            var gm = GameManager.Instance;
            error = null;
            if (b.IsMaxLevel) { error = "これ以上強化できません"; return false; }
            if (!gm.IsPrepTime) { error = "夜は強化できません"; return false; }
            if (!gm.TrySpend(b.NextCoinCost, b.NextMaterialCost)) { error = "コインか素材が足りません"; return false; }

            b.SetLevel(b.Level + 1);
            gm.NotifyChanged();
            gm.Toast(b.Def.name + "を Lv" + b.Level + " に強化した");
            return true;
        }

        public void RepairAll()
        {
            if (!Hall.IsAlive || Hall.Current < Hall.Max)
            {
                Hall.HealFull();
                BuildHallVisual(false);
            }
            foreach (var b in _buildings.Values)
                if (b.Ruined || b.Health.Current < b.Health.Max) b.Repair();
        }

        // ---- 戦闘・放置 ----

        /// <summary>敵が狙える建物（拠点を除く）。</summary>
        public List<Health> Targets()
        {
            _targets.Clear();
            foreach (var b in _buildings.Values)
                if (!b.Ruined) _targets.Add(b.Health);
            return _targets;
        }

        /// <summary>拠点の防衛装置レベルから決まる防衛力。放置中の夜の判定に使う。</summary>
        public float DefensePower
        {
            get
            {
                float p = 0f;
                foreach (var b in _buildings.Values)
                    if (b.Def.IsDefense) p += b.DefensePower;
                return p;
            }
        }

        public void AddOfflineProduction(double seconds)
        {
            foreach (var b in _buildings.Values) b.AddProduction(seconds);
        }

        public Interaction FindInteraction(Vector3 pos, float radius)
        {
            var result = new Interaction();
            float best = radius;

            Vector3 dh = Hall.Position - pos;
            dh.y = 0f;
            if (dh.magnitude - TownLayout.HallRadius < best)
            {
                best = dh.magnitude - TownLayout.HallRadius;
                result = new Interaction { valid = true, isHall = true };
            }

            foreach (var s in _slots)
            {
                Vector3 d = s.position - pos;
                d.y = 0f;
                float dist = d.magnitude - 1f;
                if (dist >= best) continue;
                best = dist;
                Building b;
                _buildings.TryGetValue(s.id, out b);
                result = new Interaction { valid = true, slot = s, building = b };
            }
            return result;
        }

        void Update()
        {
            // 施設に近づくと貯まった収入を受け取る
            _collectTimer -= Time.deltaTime;
            if (_collectTimer > 0f) return;
            _collectTimer = 0.2f;

            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null || !gm.Player.Health.IsAlive) return;
            Vector3 p = gm.Player.transform.position;
            float r = _config.collectRadius;
            foreach (var b in _buildings.Values)
            {
                if (!b.Def.IsFacility || b.Ruined || b.Stored < 1f) continue;
                Vector3 d = b.transform.position - p;
                d.y = 0f;
                if (d.magnitude > r + 1f) continue;
                int amount = b.TakeStored();
                if (amount <= 0) continue;
                Vector3 at = b.transform.position + Vector3.up * 3f;
                if (b.Def.producesMaterials) gm.AddMaterials(amount, at);
                else gm.AddCoins(amount, at);
            }
        }

        public void WriteSave(SaveData data)
        {
            data.buildings.Clear();
            foreach (var b in _buildings.Values)
            {
                data.buildings.Add(new BuildingSave
                {
                    slotId = b.Slot.id,
                    type = (int)b.Def.type,
                    level = b.Level,
                    stored = b.Stored,
                });
            }
        }
    }
}
