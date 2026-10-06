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
            bool isNew = loaded == null;
            Data = loaded ?? new SaveData { coins = config.startingCoins };
            Sanitize();

            Clock = gameObject.AddComponent<DayCycle>();
            Clock.Initialize(config.Durations, Data.day, Data.cycleTime);

            Island = CreateChild<Island>("Island");
            Island.Build(config.islandSeed, config.IslandRadius(Data.expansion), config.landHeight);
            CreateChild<Sea>("Sea").Build(400f);

            Town = CreateChild<TownManager>("Town");
            Town.Initialize(config, Island, Data.buildings);

            Rig = CreateChild<CameraRig>("CameraRig");
            Player = CreateChild<PlayerController>("Player");
            Player.Initialize(config, Rig, PlayerStats.Compute(config, Data));
            Rig.Initialize(cam, Player.transform);

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
                    "・朝と昼：施設の収入を受け取り、建物や装備を強化する\n" +
                    "・夜：襲撃者が海から上陸してくる。自分の手で撃退しよう\n\n" +
                    "まずは光る空き地に近づいて「建てる」から銀行を建ててみよう。\n" +
                    "（左半分ドラッグで移動、右半分ドラッグでカメラ回転）");
                Save();
            }
            else
            {
                double elapsed = (DateTime.UtcNow.Ticks - Data.lastSavedUtcTicks) / (double)TimeSpan.TicksPerSecond;
                ApplyOffline(elapsed);
            }
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

        public void AddCoins(int amount, Vector3? at = null)
        {
            if (amount <= 0) return;
            Data.coins += amount;
            if (at.HasValue && Hud != null) Hud.FloatingText(at.Value, "+" + amount + " コイン", new Color(1f, 0.85f, 0.35f));
            NotifyChanged();
        }

        public void AddMaterials(int amount, Vector3? at = null)
        {
            if (amount <= 0) return;
            Data.materials += amount;
            if (at.HasValue && Hud != null) Hud.FloatingText(at.Value, "+" + amount + " 素材", new Color(0.65f, 0.85f, 1f));
            NotifyChanged();
        }

        public bool CanAfford(int coins, int materials)
        {
            return Data.coins >= coins && Data.materials >= materials;
        }

        public bool TrySpend(int coins, int materials)
        {
            if (!CanAfford(coins, materials)) return false;
            Data.coins -= coins;
            Data.materials -= materials;
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
            if (Config.armorUnlockLevel == level) list.Add("防具");
            for (int i = 0; i < Config.expansionUnlockLevels.Length; i++)
                if (Config.expansionUnlockLevels[i] == level) list.Add("島の拡張（" + (i + 1) + "段目）");
            return list;
        }

        public void OnEnemyKilled(Enemy enemy, bool byPlayer)
        {
            EnemyDef def = enemy.Def;
            AddXp(def.xp);
            Vector3 at = enemy.transform.position + Vector3.up * 2f;
            int mats = UnityEngine.Random.Range(def.materialMin, def.materialMax + 1);
            if (mats > 0) AddMaterials(mats, at);
            if (def.coins > 0) AddCoins(def.coins, mats > 0 ? at + Vector3.up * 0.6f : at);
        }

        // ---- ジョブ・装備 ----

        public PlayerStats CurrentStats { get { return PlayerStats.Compute(Config, Data); } }

        void RefreshPlayer()
        {
            if (Player != null) Player.ApplyStats(CurrentStats);
        }

        public bool TryChangeJob(int index, out string error)
        {
            error = null;
            JobDef job = Config.jobs[index];
            if (Data.level < job.unlockLevel) { error = "Lv" + job.unlockLevel + "で開放"; return false; }
            if (!IsPrepTime) { error = "夜はジョブを変えられません"; return false; }
            Data.jobIndex = index;
            RefreshPlayer();
            Toast(job.name + "になった（武器：" + Names.Of(job.weapon) + "）");
            NotifyChanged();
            return true;
        }

        public bool IsWeaponAvailable(WeaponType type)
        {
            foreach (var j in Config.jobs)
                if (j.weapon == type && j.unlockLevel <= Data.level) return true;
            return false;
        }

        public void WeaponUpgradeCost(WeaponType type, out int coins, out int materials)
        {
            int lv = Data.WeaponLevel(type);
            coins = Formulas.UpgradeCost(Config.weaponUpgradeCoinBase, Config.equipCostGrowth, lv);
            materials = Formulas.UpgradeCost(Config.weaponUpgradeMaterialBase, Config.equipCostGrowth, lv);
        }

        public bool TryUpgradeWeapon(WeaponType type, out string error)
        {
            error = null;
            if (!IsWeaponAvailable(type)) { error = "まだ使えない武器です"; return false; }
            if (Data.WeaponLevel(type) >= Config.maxEquipLevel) { error = "これ以上強化できません"; return false; }
            if (!IsPrepTime) { error = "夜は強化できません"; return false; }
            int c, m;
            WeaponUpgradeCost(type, out c, out m);
            if (!TrySpend(c, m)) { error = "コインか素材が足りません"; return false; }
            Data.SetWeaponLevel(type, Data.WeaponLevel(type) + 1);
            RefreshPlayer();
            Toast(Names.Of(type) + "を +" + (Data.WeaponLevel(type) - 1) + " に強化した");
            NotifyChanged();
            return true;
        }

        public void ArmorUpgradeCost(out int coins, out int materials)
        {
            coins = Formulas.UpgradeCost(Config.armorCoinBase, Config.equipCostGrowth, Data.armorLevel + 1);
            materials = Formulas.UpgradeCost(Config.armorMaterialBase, Config.equipCostGrowth, Data.armorLevel + 1);
        }

        public bool TryUpgradeArmor(out string error)
        {
            error = null;
            if (Data.level < Config.armorUnlockLevel) { error = "Lv" + Config.armorUnlockLevel + "で開放"; return false; }
            if (Data.armorLevel >= Config.maxEquipLevel) { error = "これ以上強化できません"; return false; }
            if (!IsPrepTime) { error = "夜は強化できません"; return false; }
            int c, m;
            ArmorUpgradeCost(out c, out m);
            if (!TrySpend(c, m)) { error = "コインか素材が足りません"; return false; }
            Data.armorLevel++;
            RefreshPlayer();
            Toast(Data.armorLevel == 1 ? "防具を手に入れた" : "防具を Lv" + Data.armorLevel + " に強化した");
            NotifyChanged();
            return true;
        }

        // ---- 島の拡張 ----

        public bool IsFullyExpanded { get { return Data.expansion >= Config.MaxExpansion; } }

        public int NextExpansionLevel
        {
            get { return IsFullyExpanded ? int.MaxValue : Config.expansionUnlockLevels[Data.expansion]; }
        }

        public void ExpansionCost(out int coins, out int materials)
        {
            coins = Formulas.UpgradeCost(Config.expansionBaseCost, Config.expansionCostGrowth, Data.expansion + 1);
            materials = Formulas.UpgradeCost(Config.expansionBaseMaterialCost, Config.expansionCostGrowth, Data.expansion + 1);
        }

        public bool TryExpand(out string error)
        {
            error = null;
            if (IsFullyExpanded) { error = "これ以上広げられません"; return false; }
            if (Data.level < NextExpansionLevel) { error = "Lv" + NextExpansionLevel + "で開放"; return false; }
            if (!IsPrepTime) { error = "夜は拡張できません"; return false; }
            int c, m;
            ExpansionCost(out c, out m);
            if (!TrySpend(c, m)) { error = "コインか素材が足りません"; return false; }
            Data.expansion++;
            Island.Build(Config.islandSeed, Config.IslandRadius(Data.expansion), Config.landHeight);
            Town.RefreshSlots();
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

            float coinsBefore, matsBefore;
            StoredTotals(out coinsBefore, out matsBefore);
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
            if (result.Materials > 0) AddMaterials(result.Materials);
            Clock.SetState(result.Day, (float)result.CycleTime);
            if (!Clock.IsNight || result.Day != dayBefore)
            {
                Town.RepairAll();
                Player.Health.HealFull();
            }

            float coinsAfter, matsAfter;
            StoredTotals(out coinsAfter, out matsAfter);
            int coins = Mathf.FloorToInt(coinsAfter - coinsBefore);
            int facilityMats = Mathf.FloorToInt(matsAfter - matsBefore);

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

            sb.Append("\n施設の収入：コイン +").Append(coins);
            if (facilityMats > 0) sb.Append("、素材 +").Append(facilityMats);
            sb.Append("（施設に近づくと受け取れます）");
            if (result.Materials > 0) sb.Append("\n撃退した敵の素材：+").Append(result.Materials);

            Hud.ShowDialog("おかえりなさい", sb.ToString());
            NotifyChanged();
            Save();
        }

        void StoredTotals(out float coins, out float materials)
        {
            coins = 0f;
            materials = 0f;
            foreach (var b in Town.Buildings)
            {
                if (!b.Def.IsFacility) continue;
                if (b.Def.producesMaterials) materials += b.Stored;
                else coins += b.Stored;
            }
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

        /// <summary>セーブを消して最初からやり直す（テスト用）。</summary>
        public void ResetProgress()
        {
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
