using NUnit.Framework;

namespace MistIsland.Tests
{
    public class OfflineProgressTests
    {
        static OfflineInput Input(double elapsed, float defense, OfflineBreachRule rule = OfflineBreachRule.KeepUntilBreach)
        {
            return new OfflineInput
            {
                Durations = new DayDurations(180, 180, 180),
                Day = 1,
                CycleTime = 0,
                ElapsedSeconds = elapsed,
                MaxSeconds = 12 * 3600,
                DefensePower = defense,
                EnemyBaseStrength = 6,
                EnemyGrowthPerDay = 1.18f,
                MaterialsPerNightBase = 4,
                MaterialsPerDay = 1.5f,
                Rule = rule,
            };
        }

        [Test]
        public void 昼だけなら全部が収入になる()
        {
            var r = OfflineProgress.Simulate(Input(300, 0));
            Assert.AreEqual(300, r.ProductiveSeconds, 1e-6);
            Assert.AreEqual(1, r.Day);
            Assert.AreEqual(300, r.CycleTime, 1e-6);
            Assert.IsFalse(r.Breached);
        }

        [Test]
        public void 防衛力が足りれば夜を越えて素材も得る()
        {
            var r = OfflineProgress.Simulate(Input(540, 100));
            Assert.IsFalse(r.Breached);
            Assert.AreEqual(1, r.NightsSurvived);
            Assert.AreEqual(4, r.Materials);
            Assert.AreEqual(540, r.ProductiveSeconds, 1e-6);
            Assert.AreEqual(2, r.Day);
            Assert.AreEqual(0, r.CycleTime, 1e-6);
        }

        [Test]
        public void 壊されるまでの分は受け取れる()
        {
            var r = OfflineProgress.Simulate(Input(1000, 0, OfflineBreachRule.KeepUntilBreach));
            Assert.IsTrue(r.Breached);
            Assert.AreEqual(1, r.BreachedOnDay);
            Assert.AreEqual(360, r.ProductiveSeconds, 1e-6);
            Assert.AreEqual(0, r.Materials);
        }

        [Test]
        public void 壊されたら全部失うルール()
        {
            var r = OfflineProgress.Simulate(Input(1000, 0, OfflineBreachRule.LoseAll));
            Assert.IsTrue(r.Breached);
            Assert.AreEqual(0, r.ProductiveSeconds, 1e-6);
        }

        [Test]
        public void 日を重ねると敵が強くなり途中で破られる()
        {
            // 防衛力 8：1日目(6)と2日目(7.08)は耐え、3日目(8.35)で破られる
            var r = OfflineProgress.Simulate(Input(540 * 5, 8));
            Assert.IsTrue(r.Breached);
            Assert.AreEqual(3, r.BreachedOnDay);
            Assert.AreEqual(2, r.NightsSurvived);
            Assert.AreEqual(540 * 2 + 360, r.ProductiveSeconds, 1e-6);
            Assert.AreEqual(6, r.Day);
        }

        [Test]
        public void 上限時間で打ち切る()
        {
            var input = Input(100000, 1000);
            input.MaxSeconds = 600;
            var r = OfflineProgress.Simulate(input);
            Assert.AreEqual(600, r.SimulatedSeconds, 1e-6);
            Assert.AreEqual(2, r.Day);
            Assert.AreEqual(60, r.CycleTime, 1e-6);
        }

        [Test]
        public void 夜の途中から再開しても判定する()
        {
            var input = Input(140, 0);
            input.CycleTime = 400;
            var r = OfflineProgress.Simulate(input);
            Assert.IsTrue(r.Breached);
            Assert.AreEqual(0, r.ProductiveSeconds, 1e-6);
            Assert.AreEqual(2, r.Day);
            Assert.AreEqual(0, r.CycleTime, 1e-6);
        }
    }

    public class FormulasTests
    {
        [Test]
        public void 必要経験値は増えていく()
        {
            Assert.AreEqual(20, Formulas.XpToNext(1, 20, 1.35f));
            Assert.AreEqual(27, Formulas.XpToNext(2, 20, 1.35f));
            Assert.Greater(Formulas.XpToNext(10, 20, 1.35f), Formulas.XpToNext(9, 20, 1.35f));
        }

        [Test]
        public void 時間帯の判定()
        {
            var d = new DayDurations(180, 180, 180);
            Assert.AreEqual(Phase.Morning, d.PhaseAt(0));
            Assert.AreEqual(Phase.Day, d.PhaseAt(180));
            Assert.AreEqual(Phase.Night, d.PhaseAt(360));
            Assert.AreEqual(Phase.Night, d.PhaseAt(539.9));
            Assert.AreEqual(540, d.Total);
        }
    }
}
