using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MistIsland
{
    /// <summary>
    /// ゲーム全体の進行役。所持品・経験値・ジョブ・装備・島の拡張・セーブ・放置中の進行をまとめて扱う。
    /// 見た目やオブジェクトは GameBootstrap がすべてコードで組み立てる。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameConfig Config { get; private set; }
        public SaveData Data { get; private set; }
        public DayCycle Clock { get; private set; }
        public Island Island { get; private set; }
        public TownManager Town { get; private set; }
        public PlayerController Player { get; private set; }
        public CameraRig Rig { get; private set; }
        public EnemySpawner Spawner { get; private set; }
        public Hud Hud { get; private set; }

        /// <summary>所持品・レベル・装備などが変わったら通知する（UI の更新用）。</summary>
        public event Action Changed;

        float _autosaveTimer;
        bool _pausedSinceSave;
        bool _saveDisabled;

        public bool IsPrepTime { get { return Clock != null && !Clock.IsNight; } }

        public void Initialize(GameConfig config, Camera cam)
        {
            Instance = this;
            Config = config;
            InputBridge.Reset();

            SaveData loaded = SaveSystem.Load();
            // 形式が古いセーブは引き継げないので最初から
            if (loaded != null && loaded.version < SaveData.CurrentVersion) loaded = null;
            bool isNew = loaded == null;
            Data = loaded ?? NewGame(config);
            Sanitize();

            Clock = gameObject.AddComponent<DayCycle>();
            Clock.Initialize(config.Durations, Data.day, Data.cycleTime);

            Island = CreateChild<Island>("Island");
            Island.Build(config, Data.expansion);
            CreateChild<Sea>("Sea").Build(400f);

            Town = CreateChild<TownManager>("Town");
            Town.Initialize(config, Island, Data.buildings);

            Rig = CreateChild<CameraRig>("CameraRig");
            Player = CreateChild<PlayerController>("Player");
            Player.Initialize(config, Rig, PlayerStats.Compute(config, Data));
            Rig.Initialize(cam, Player.transform);
            Rig.FitRadius = Island.Radius;

            gameObject.AddComponent<MistVisuals>().Initialize(Clock, cam, Rig);

            Spawner = CreateChild<EnemySpawner>("EnemySpawner");
            Spawner.Initialize(config, Clock, Island);

            Clock.PhaseChanged += OnPhaseChanged;
            Clock.DayStarted += OnDayStarted;

            Hud = CreateChild<Hud>("UI");
            Hud.Initialize(this);

            _autosaveTimer = config.autosaveSeconds;

            if (isNew)
            {
                Hud.ShowDialog("霧の島へようこそ",
                    "霧の海に浮かぶ小さな島で町を育てよう。\n\n" +
                    "・朝と昼：施設の収入を受け取り、建物を建てて町を育てる\n" +
                    "・夜：襲撃者が海から上陸してくる。近づくと自動で攻撃する\n" +
                    "・画面右側を長押しして離すと溜め攻撃\n" +
                    "・すぐ近くのベースキャンプで、ジョブや装備を変えられる\n" +
                    "・ベースキャンプが壊されると最初からやり直し\n\n" +
                    "（左半分ドラッグで移動、右半分ドラッグでカメラ回転）");
                Save();
            }
            else
            {
                double elapsed = (DateTime.UtcNow.Ticks - Data.lastSavedUtcTicks) / (double)TimeSpan.TicksPerSecond;
                ApplyOffline(elapsed);
            }
        }

        static SaveData NewGame(GameConfig config)
        {
            var data = new SaveData { coins = config.startingCoins };
            data.Add(ResourceType.Wood, config.startingWood);
            return data;
        }

        T CreateChild<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        void Sanitize()
        {
            Data.level = Mathf.Clamp(Data.level, 1, Config.maxLevel);
            Data.expansion = Mathf.Clamp(Data.expansion, 0, Config.MaxExpansion);
            Data.day = Mathf.Max(1, Data.day);
            if (Data.buildings == null) Data.buildings = new List<BuildingSave>();
            if (Data.jobIndex < 0 || Data.jobIndex >= Config.jobs.Length || Config.jobs[Data.jobIndex].unlockLevel > Data.level)
                Data.jobIndex = 0;
            EnsureStarterItems();
            float total = Config.Durations.Total;
            if (Data.cycleTime < 0f || Data.cycleTime >= total) Data.cycleTime = 0f;
        }

        void OnDestroy()
        {
            if (Clock != null)
            {
                Clock.PhaseChanged -= OnPhaseChanged;
                Clock.DayStarted -= OnDayStarted;
            }
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        void Update()
        {
            _autosaveTimer -= Time.unscaledDeltaTime;
            if (_autosaveTimer <= 0f)
            {
                _autosaveTimer = Config.autosaveSeconds;
                Save();
            }
        }

        // ---- 所持品 ----

        public void NotifyChanged()
        {
            if (Changed != null) Changed();
        }

        public static Color ResourceTextColor(ResourceType type)
        {
            return type == ResourceType.Coins ? new Color(1f, 0.85f, 0.35f) : Building.ResourceColor(type) * 1.25f;
        }

        public void AddResource(ResourceType type, int amount, Vector3? at = null)
        {
            if (amount <= 0) return;
            Data.Add(type, amount);
            if (at.HasValue && Hud != null) Hud.FloatingText(at.Value, "+" + amount + " " + Names.Of(type), ResourceTextColor(type));
            NotifyChanged();
        }

        public void AddCoins(int amount, Vector3? at = null)
        {
            AddResource(ResourceType.Coins, amount, at);
        }

        public bool CanAfford(Cost cost)
        {
            if (cost == null) return true;
            foreach (var t in Cost.All)
                if (Data.Get(t) < cost.Get(t)) return false;
            return true;
        }

        public bool TrySpend(Cost cost)
        {
            if (!CanAfford(cost)) return false;
            if (cost != null)
                foreach (var t in Cost.All)
                    Data.Add(t, -cost.Get(t));
            NotifyChanged();
            return true;
        }

        // ---- 経験値・レベル ----

        public int XpToNext { get { return Formulas.XpToNext(Data.level, Config.xpToLevel2, Config.xpGrowth); } }
        public bool IsMaxLevel { get { return Data.level >= Config.maxLevel; } }

        public void AddXp(int amount)
        {
            if (amount <= 0 || IsMaxLevel) return;
            Data.xp += amount;
            var unlocked = new List<string>();
            int gained = 0;
            while (!IsMaxLevel && Data.xp >= XpToNext)
            {
                Data.xp -= XpToNext;
                Data.level++;
                gained++;
                unlocked.AddRange(UnlocksAt(Data.level));
            }
            if (IsMaxLevel) Data.xp = 0;

            if (gained > 0)
            {
                RefreshPlayer();
                Player.Health.HealFull();
                string msg = "レベルアップ！ Lv" + Data.level;
                if (unlocked.Count > 0) msg += "\n開放：" + string.Join("、", unlocked.ToArray());
                Toast(msg, 4f);
            }
            NotifyChanged();
        }

        /// <summary>そのレベルで開放されるもの。</summary>
        public List<string> UnlocksAt(int level)
        {
            var list = new List<string>();
            foreach (var j in Config.jobs)
                if (j.unlockLevel == level && level > 1) list.Add("ジョブ「" + j.name + "」");
            foreach (var b in Config.buildings)
                if (b.unlockLevel == level && level > 1) list.Add((b.IsDefense ? "防衛装置「" : "施設「") + b.name + "」");
            foreach (var w in Config.weaponItems)
                if (w.unlockLevel == level && level > 1) list.Add("武器「" + w.name + "」");
            foreach (var a in Config.armorItems)
                if (a.unlockLevel == level && level > 1) list.Add("防具「" + a.name + "」");
            for (int i = 0; i < Config.expansionUnlockLevels.Length; i++)
                if (Config.expansionUnlockLevels[i] == level) list.Add("島の拡張（" + (i + 1) + "段目）");
            return list;
        }

        public void OnEnemyKilled(Enemy enemy, bool byPlayer)
        {
            EnemyDef def = enemy.Def;
            AddXp(def.xp);
            Vector3 at = enemy.transform.position + Vector3.up * 2f;
            if (def.drops == null) return;
            foreach (var t in Cost.All)
            {
                int max = def.drops.Get(t);
                if (max <= 0) continue;
                int amount = t == ResourceType.Coins ? max : UnityEngine.Random.Range(0, max + 1);
                if (amount <= 0) continue;
                AddResource(t, amount, at);
                at += Vector3.up * 0.6f;
            }
        }

        // ---- ジョブ・装備 ----

        public PlayerStats CurrentStats { get { return PlayerStats.Compute(Config, Data); } }

        void RefreshPlayer()
        {
            if (Player != null) Player.ApplyStats(CurrentStats);
        }

        public bool IsJobUnlocked(JobDef job)
        {
            return Data.level >= job.unlockLevel;
        }

        public bool TryChangeJob(int index, out string error)
        {
            error = null;
            JobDef job = Config.jobs[index];
            if (!IsJobUnlocked(job)) { error = "Lv" + job.unlockLevel + "で開放"; return false; }
            Data.jobIndex = index;
            RefreshPlayer();
            Toast(job.name + "になった（武器：" + Names.Of(job.weapon) + "）");
            NotifyChanged();
            return true;
        }

        /// <summary>最初から持っている装備を持たせ、装備欄が空なら埋める。</summary>
        void EnsureStarterItems()
        {
            if (Data.weapons == null) Data.weapons = new List<OwnedItem>();
            if (Data.armors == null) Data.armors = new List<OwnedItem>();
            foreach (WeaponType type in new[] { WeaponType.Sword, WeaponType.Spear, WeaponType.Bow })
            {
                WeaponItemDef starter = Config.StarterWeapon(type);
                if (starter == null) continue;
                if (Data.FindWeapon(starter.id) == null) Data.weapons.Add(new OwnedItem { id = starter.id });
                string equipped = Data.EquippedWeaponId(type);
                WeaponItemDef def = Config.WeaponItem(equipped);
                if (def == null || def.type != type || Data.FindWeapon(equipped) == null) Data.SetEquippedWeapon(type, starter.id);
            }
            ArmorItemDef armor = Config.StarterArmor;
            if (armor != null)
            {
                if (Data.FindArmor(armor.id) == null) Data.armors.Add(new OwnedItem { id = armor.id });
                if (Config.ArmorItem(Data.equippedArmor) == null || Data.FindArmor(Data.equippedArmor) == null) Data.equippedArmor = armor.id;
            }
        }

        /// <summary>装備を level から level+1 に強化するコスト。</summary>
        public Cost ItemUpgradeCost(Cost itemCost, int level)
        {
            Cost baseCost = (itemCost ?? new Cost()).Scaled(Config.upgradeCostRatio);
            foreach (var t in Cost.All)
                baseCost.Set(t, baseCost.Get(t) + (Config.upgradeBaseCost != null ? Config.upgradeBaseCost.Get(t) : 0));
            return baseCost.Scaled(Math.Pow(Config.equipCostGrowth, Math.Max(0, level - 1)));
        }

        public bool TryCraftWeapon(WeaponItemDef def, out string error)
        {
            error = null;
            if (Data.FindWeapon(def.id) != null) { error = "もう持っています"; return false; }
            if (Data.level < def.unlockLevel) { error = "Lv" + def.unlockLevel + "で作れる"; return false; }
            if (!TrySpend(def.cost)) { error = "コインか素材が足りません"; return false; }
            Data.weapons.Add(new OwnedItem { id = def.id });
            Data.SetEquippedWeapon(def.type, def.id);
            RefreshPlayer();
            Toast(def.name + "を作って装備した");
            NotifyChanged();
            return true;
        }

        public bool TryCraftArmor(ArmorItemDef def, out string error)
        {
            error = null;
            if (Data.FindArmor(def.id) != null) { error = "もう持っています"; return false; }
            if (Data.level < def.unlockLevel) { error = "Lv" + def.unlockLevel + "で作れる"; return false; }
            if (!TrySpend(def.cost)) { error = "コインか素材が足りません"; return false; }
            Data.armors.Add(new OwnedItem { id = def.id });
            Data.equippedArmor = def.id;
            RefreshPlayer();
            Toast(def.name + "を作って装備した");
            NotifyChanged();
            return true;
        }

        public bool TryUpgradeWeapon(WeaponItemDef def, out string error)
        {
            error = null;
            OwnedItem owned = Data.FindWeapon(def.id);
            if (owned == null) { error = "持っていません"; return false; }
            if (owned.level >= Config.maxEquipLevel) { error = "これ以上強化できません"; return false; }
            if (!TrySpend(ItemUpgradeCost(def.cost, owned.level))) { error = "コインか素材が足りません"; return false; }
            owned.level++;
            RefreshPlayer();
            Toast(def.name + "を +" + (owned.level - 1) + " に強化した");
            NotifyChanged();
            return true;
        }

        public bool TryUpgradeArmor(ArmorItemDef def, out string error)
        {
            error = null;
            OwnedItem owned = Data.FindArmor(def.id);
            if (owned == null) { error = "持っていません"; return false; }
            if (owned.level >= Config.maxEquipLevel) { error = "これ以上強化できません"; return false; }
            if (!TrySpend(ItemUpgradeCost(def.cost, owned.level))) { error = "コインか素材が足りません"; return false; }
            owned.level++;
            RefreshPlayer();
            Toast(def.name + "を +" + (owned.level - 1) + " に強化した");
            NotifyChanged();
            return true;
        }

        public void EquipWeapon(WeaponItemDef def)
        {
            if (Data.FindWeapon(def.id) == null) return;
            Data.SetEquippedWeapon(def.type, def.id);
            RefreshPlayer();
            NotifyChanged();
        }

        public void EquipArmor(ArmorItemDef def)
        {
            if (Data.FindArmor(def.id) == null) return;
            Data.equippedArmor = def.id;
            RefreshPlayer();
            NotifyChanged();
        }

        // ---- ベースキャンプ ----

        public bool IsInCamp { get; private set; }

        /// <summary>ベースキャンプに入る。中にいる間は時間が止まる。</summary>
        public void EnterCamp()
        {
            if (IsInCamp) return;
            IsInCamp = true;
            Time.timeScale = 0f;
            InputBridge.Reset();
            Player.Health.HealFull();
            NotifyChanged();
        }

        public void LeaveCamp()
        {
            if (!IsInCamp) return;
            IsInCamp = false;
            Time.timeScale = 1f;
            InputBridge.Reset();
            Save();
            NotifyChanged();
        }

        // ---- 島の拡張 ----

        public bool IsFullyExpanded { get { return Data.expansion >= Config.MaxExpansion; } }

        public int NextExpansionLevel
        {
            get { return IsFullyExpanded ? int.MaxValue : Config.expansionUnlockLevels[Data.expansion]; }
        }

        public Cost ExpansionCost
        {
            get { return (Config.expansionCost ?? new Cost()).Scaled(Math.Pow(Config.expansionCostGrowth, Data.expansion)); }
        }

        public bool TryExpand(out string error)
        {
            error = null;
            if (IsFullyExpanded) { error = "これ以上広げられません"; return false; }
            if (Data.level < NextExpansionLevel) { error = "Lv" + NextExpansionLevel + "で開放"; return false; }
            if (!IsPrepTime) { error = "夜は拡張できません"; return false; }
            if (!TrySpend(ExpansionCost)) { error = "コインか素材が足りません"; return false; }
            Data.expansion++;
            Island.Build(Config, Data.expansion);
            Town.RefreshSlots();
            Rig.FitRadius = Island.Radius;
            Vector3 p = Player.transform.position;
            p.y = Island.HeightAt(p.x, p.z);
            Player.transform.position = p;
            Toast("島が広がった！新しい空き地が使えるようになった");
            NotifyChanged();
            Save();
            return true;
        }

        // ---- 時間帯 ----

        void OnPhaseChanged(Phase phase)
        {
            switch (phase)
            {
                case Phase.Morning:
                    Town.RepairAll();
                    if (Player.Health.IsAlive) Player.Health.HealFull();
                    Toast("朝になった。襲撃者は霧の向こうへ帰っていく");
                    break;
                case Phase.Day:
                    Toast("昼になった。夜に備えよう");
                    break;
                case Phase.Night:
                    Toast("夜が来た！襲撃者が上陸してくる");
                    break;
            }
            NotifyChanged();
            Save();
        }

        void OnDayStarted(int day)
        {
            Toast(day + "日目の朝");
        }

        public void Toast(string message, float seconds = 2.5f)
        {
            if (Hud != null) Hud.Toast(message, seconds);
        }

        // ---- 放置 ----

        void ApplyOffline(double elapsedSeconds)
        {
            if (elapsedSeconds < Config.minOfflineSeconds) return;

            Cost storedBefore = StoredTotals();
            float defense = Town.DefensePower;
            int dayBefore = Clock.Day;

            var result = OfflineProgress.Simulate(new OfflineInput
            {
                Durations = Config.Durations,
                Day = Clock.Day,
                CycleTime = Clock.Time,
                ElapsedSeconds = elapsedSeconds,
                MaxSeconds = Config.maxOfflineHours * 3600.0,
                DefensePower = defense,
                EnemyBaseStrength = Config.offlineEnemyBaseStrength,
                EnemyGrowthPerDay = Config.offlineEnemyGrowthPerDay,
                MaterialsPerNightBase = Config.offlineMaterialsPerNightBase,
                MaterialsPerDay = Config.offlineMaterialsPerDay,
                Rule = Config.offlineBreachRule,
            });

            Spawner.ClearAll();
            Town.AddOfflineProduction(result.ProductiveSeconds);
            // 夜に撃退した敵の素材は木材と石材に半分ずつ
            int offlineWood = (result.Materials + 1) / 2;
            int offlineStone = result.Materials / 2;
            AddResource(ResourceType.Wood, offlineWood);
            AddResource(ResourceType.Stone, offlineStone);
            Clock.SetState(result.Day, (float)result.CycleTime);
            if (!Clock.IsNight || result.Day != dayBefore)
            {
                Town.RepairAll();
                Player.Health.HealFull();
            }

            Cost storedAfter = StoredTotals();
            var gained = new Cost();
            foreach (var t in Cost.All) gained.Set(t, Mathf.Max(0, storedAfter.Get(t) - storedBefore.Get(t)));

            var sb = new StringBuilder();
            sb.Append("留守にしていた時間：").Append(FormatDuration(elapsedSeconds));
            if (elapsedSeconds > result.SimulatedSeconds + 1)
                sb.Append("\n（放置で進むのは最大 ").Append(FormatDuration(result.SimulatedSeconds)).Append(" まで）");
            sb.Append("\n\n");
            if (result.Day > dayBefore) sb.Append(dayBefore).Append("日目 → ").Append(result.Day).Append("日目\n");
            sb.Append("防衛力：").Append(Mathf.RoundToInt(defense)).Append("\n");
            if (result.NightsSurvived > 0) sb.Append("夜を ").Append(result.NightsSurvived).Append(" 回守り切った\n");

            if (result.Breached)
            {
                sb.Append("\n").Append(result.BreachedOnDay).Append("日目の夜、防衛装置が壊されてしまった…\n");
                sb.Append(Config.offlineBreachRule == OfflineBreachRule.LoseAll
                    ? "留守のあいだの報酬は手に入らなかった。\n"
                    : "壊されるまでに貯まった分だけ受け取れる。\n");
                sb.Append("防衛装置を強化すると、長く放置しても安全になる。\n");
            }

            sb.Append("\n施設に貯まった分：").Append(gained.IsFree ? "なし" : gained.ToText());
            sb.Append("（施設に近づくと受け取れます）");
            if (result.Materials > 0) sb.Append("\n撃退した敵の素材：木材 +").Append(offlineWood).Append("、石材 +").Append(offlineStone);

            Hud.ShowDialog("おかえりなさい", sb.ToString());
            NotifyChanged();
            Save();
        }

        /// <summary>施設に貯まっている量の合計（整数に切り捨て）。</summary>
        Cost StoredTotals()
        {
            var total = new Cost();
            foreach (var b in Town.Buildings)
            {
                if (!b.Def.IsFacility) continue;
                total.Set(b.Def.produces, total.Get(b.Def.produces) + Mathf.FloorToInt(b.Stored));
            }
            return total;
        }

        static string FormatDuration(double seconds)
        {
            var t = TimeSpan.FromSeconds(seconds);
            if (t.TotalHours >= 1) return (int)t.TotalHours + "時間" + t.Minutes + "分";
            if (t.TotalMinutes >= 1) return t.Minutes + "分" + t.Seconds + "秒";
            return t.Seconds + "秒";
        }

        // ---- セーブ ----

        public void Save()
        {
            if (_saveDisabled || Data == null || Clock == null) return;
            Data.day = Clock.Day;
            Data.cycleTime = Clock.Time;
            Town.WriteSave(Data);
            SaveSystem.Save(Data);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Save();
                _pausedSinceSave = true;
            }
            else if (_pausedSinceSave)
            {
                _pausedSinceSave = false;
                double elapsed = (DateTime.UtcNow.Ticks - Data.lastSavedUtcTicks) / (double)TimeSpan.TicksPerSecond;
                ApplyOffline(elapsed);
            }
        }

        void OnApplicationQuit()
        {
            Save();
        }

        public bool IsGameOver { get; private set; }

        /// <summary>拠点が壊された。セーブを消して、最初からやり直してもらう。</summary>
        public void GameOver()
        {
            if (IsGameOver) return;
            IsGameOver = true;
            _saveDisabled = true;
            SaveSystem.Delete();
            Time.timeScale = 0f;
            InputBridge.Reset();
            Hud.ShowGameOver(
                "ベースキャンプが襲撃者に壊されてしまった…\n\n" +
                Clock.Day + "日目の夜、Lv" + Data.level + " まで守り抜いた。\n\n" +
                "島は霧に飲まれた。最初からやり直そう。",
                ResetProgress);
        }

        /// <summary>セーブを消して最初からやり直す。</summary>
        public void ResetProgress()
        {
            Time.timeScale = 1f;
            _saveDisabled = true;
            SaveSystem.Delete();
            GameBootstrap.Restart();
        }

        /// <summary>テスト用：次の時間帯まで進める。</summary>
        public void DebugSkipPhase()
        {
            Clock.SkipToNextPhase();
        }
    }
}
