using System.Collections.Generic;
using UnityEngine;

namespace MistIsland
{
    /// <summary>
    /// 夜のあいだ、島の周りの海から敵を送り込む。ウェーブで区切らず、時間に応じて途切れなく来る。
    /// 日数を重ねるほど間隔が短く・数が多く・敵が強くなり、新しい種類も混ざる。
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        GameConfig _config;
        DayCycle _clock;
        Island _island;
        float _timer;

        public void Initialize(GameConfig config, DayCycle clock, Island island)
        {
            _config = config;
            _clock = clock;
            _island = island;
            _clock.PhaseChanged += OnPhaseChanged;
            _timer = config.firstSpawnDelay;
        }

        void OnDestroy()
        {
            if (_clock != null) _clock.PhaseChanged -= OnPhaseChanged;
        }

        void OnPhaseChanged(Phase phase)
        {
            if (phase == Phase.Night)
            {
                _timer = _config.firstSpawnDelay;
            }
            else if (phase == Phase.Morning)
            {
                // 夜明けで残った敵は海へ帰る
                foreach (var e in Enemy.All.ToArray()) e.Retreat();
            }
        }

        void Update()
        {
            if (_clock == null || !_clock.IsNight) return;
            if (_clock.PhaseRemaining < _config.stopSpawnBeforeDawn) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            int day = _clock.Day;
            _timer = Formulas.SpawnInterval(day, _config.spawnIntervalBase, _config.spawnIntervalMin, _config.spawnIntervalDayFactor);

            int groupSize = 1 + (day - 1) / 3 + (Random.value < 0.3f ? 1 : 0);
            float angle = Random.value * Mathf.PI * 2f;
            for (int i = 0; i < groupSize; i++)
            {
                if (ActiveCount() >= _config.maxAliveEnemies) break;
                EnemyDef def = PickType(day);
                if (def == null) break;
                float a = angle + Random.Range(-0.15f, 0.15f);
                Vector3 p = _island.OffshorePoint(a, Random.Range(0f, 2f));
                Enemy.Spawn(def, day, _config, p);
            }
        }

        static int ActiveCount()
        {
            int n = 0;
            foreach (var e in Enemy.All)
                if (e.IsActive) n++;
            return n;
        }

        EnemyDef PickType(int day)
        {
            var candidates = new List<EnemyDef>();
            float total = 0f;
            foreach (var e in _config.enemies)
            {
                if (e.unlockDay > day || e.spawnWeight <= 0f) continue;
                candidates.Add(e);
                total += e.spawnWeight;
            }
            if (candidates.Count == 0) return null;
            float r = Random.value * total;
            foreach (var e in candidates)
            {
                r -= e.spawnWeight;
                if (r <= 0f) return e;
            }
            return candidates[candidates.Count - 1];
        }

        public void ClearAll()
        {
            foreach (var e in Enemy.All.ToArray())
                if (e != null) Destroy(e.gameObject);
        }
    }
}
