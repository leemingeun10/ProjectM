using System;
using Game.Sim.Cultivation;
using NUnit.Framework;

namespace Game.Tests
{
    public class CultivationTests
    {
        private const int Today = 20261019;

        private static void AssertStats(CultivationStats stats, int external, int internalPower, int lastDate)
        {
            Assert.AreEqual(external, stats.ExternalPower, "externalPower");
            Assert.AreEqual(internalPower, stats.InternalPower, "internalPower");
            Assert.AreEqual(lastDate, stats.LastTrainingDate, "lastTrainingDate");
        }

        private static string Json(string external, string internalPower, string date, string version = "1")
            => $"{{\"version\": {version}, \"externalPower\": {external}, \"internalPower\": {internalPower}, \"lastTrainingDate\": {date}}}";

        // ---- Constants ----

        [Test]
        public void Constants_AreOrderedAndPositive()
        {
            Assert.Greater(CultivationRules.DailyGain, 0);
            Assert.Greater(CultivationRules.Tier2Threshold, 0);
            Assert.Greater(CultivationRules.Tier3Threshold, CultivationRules.Tier2Threshold);
        }

        // ---- Defaults ----

        [Test]
        public void NewStats_AreAllZero()
        {
            CultivationStats fresh = default;

            AssertStats(fresh, 0, 0, 0);
            Assert.AreEqual(1, CultivationRules.GetTier(fresh.ExternalPower));
            Assert.AreEqual(1, CultivationRules.GetTier(fresh.InternalPower));
            Assert.IsFalse(CultivationRules.HasTrainedToday(fresh, Today));
        }

        // ---- Add ----

        [Test]
        public void AddExternalPower_ChangesOnlyExternalAndDate()
        {
            var start = new CultivationStats(30, 70, 20261018);

            AssertStats(CultivationRules.AddExternalPower(start, Today), 30 + CultivationRules.DailyGain, 70, Today);
            AssertStats(start, 30, 70, 20261018);
        }

        [Test]
        public void AddInternalPower_ChangesOnlyInternalAndDate()
        {
            var start = new CultivationStats(30, 70, 20261018);

            AssertStats(CultivationRules.AddInternalPower(start, Today), 30, 70 + CultivationRules.DailyGain, Today);
            AssertStats(start, 30, 70, 20261018);
        }

        [Test]
        public void Add_FromFreshStats()
        {
            AssertStats(CultivationRules.AddExternalPower(default, Today), CultivationRules.DailyGain, 0, Today);
            AssertStats(CultivationRules.AddInternalPower(default, Today), 0, CultivationRules.DailyGain, Today);
        }

        [Test]
        public void Add_UpToIntMax_Succeeds()
        {
            int justFits = int.MaxValue - CultivationRules.DailyGain;

            AssertStats(CultivationRules.AddExternalPower(new CultivationStats(justFits, 0, 0), Today), int.MaxValue, 0, Today);
            AssertStats(CultivationRules.AddInternalPower(new CultivationStats(0, justFits, 0), Today), 0, int.MaxValue, Today);
        }

        [Test]
        public void Add_ThrowsInsteadOfWrapping()
        {
            int overflows = int.MaxValue - CultivationRules.DailyGain + 1;

            Assert.Throws<OverflowException>(() => CultivationRules.AddExternalPower(new CultivationStats(overflows, 0, 0), Today));
            Assert.Throws<OverflowException>(() => CultivationRules.AddInternalPower(new CultivationStats(0, overflows, 0), Today));
            Assert.Throws<OverflowException>(() => CultivationRules.AddExternalPower(new CultivationStats(int.MaxValue, 0, 0), Today));
        }

        [Test]
        public void Add_RejectsInvalidToday()
        {
            foreach (int bad in new[] { 0, -1, 20261301, 20261000, 20260230, 2026101, 100000000 })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => CultivationRules.AddExternalPower(default, bad), bad.ToString());
                Assert.Throws<ArgumentOutOfRangeException>(() => CultivationRules.AddInternalPower(default, bad), bad.ToString());
            }
        }

        // ---- Tiers ----

        [Test]
        public void GetTier_Boundaries()
        {
            Assert.AreEqual(1, CultivationRules.GetTier(int.MinValue));
            Assert.AreEqual(1, CultivationRules.GetTier(-1));
            Assert.AreEqual(1, CultivationRules.GetTier(0));
            Assert.AreEqual(1, CultivationRules.GetTier(49));
            Assert.AreEqual(2, CultivationRules.GetTier(50));
            Assert.AreEqual(2, CultivationRules.GetTier(149));
            Assert.AreEqual(3, CultivationRules.GetTier(150));
            Assert.AreEqual(3, CultivationRules.GetTier(int.MaxValue));
        }

        [Test]
        public void GetTier_FollowsConstants()
        {
            Assert.AreEqual(1, CultivationRules.GetTier(CultivationRules.Tier2Threshold - 1));
            Assert.AreEqual(2, CultivationRules.GetTier(CultivationRules.Tier2Threshold));
            Assert.AreEqual(2, CultivationRules.GetTier(CultivationRules.Tier3Threshold - 1));
            Assert.AreEqual(3, CultivationRules.GetTier(CultivationRules.Tier3Threshold));
        }

        [Test]
        public void GetTier_ReachesTier2AfterFiveTrainings()
        {
            CultivationStats stats = default;
            int day = 20261001;
            for (int i = 0; i < 4; i++)
                stats = CultivationRules.AddExternalPower(stats, day++);
            Assert.AreEqual(1, CultivationRules.GetTier(stats.ExternalPower));

            stats = CultivationRules.AddExternalPower(stats, day);
            Assert.AreEqual(2, CultivationRules.GetTier(stats.ExternalPower));
        }

        // ---- Trained today ----

        [Test]
        public void HasTrainedToday_ComparesDateOnly()
        {
            var trained = new CultivationStats(10, 0, Today);

            Assert.IsTrue(CultivationRules.HasTrainedToday(trained, Today));
            Assert.IsFalse(CultivationRules.HasTrainedToday(trained, Today + 1));
            Assert.IsFalse(CultivationRules.HasTrainedToday(trained, Today - 1));
            Assert.IsTrue(CultivationRules.HasTrainedToday(CultivationRules.AddInternalPower(default, Today), Today));
        }

        // ---- Dates ----

        [Test]
        public void IsValidDate_Boundaries()
        {
            Assert.IsTrue(CultivationRules.IsValidDate(20261019));
            Assert.IsTrue(CultivationRules.IsValidDate(10000101));
            Assert.IsTrue(CultivationRules.IsValidDate(99991231));
            Assert.IsTrue(CultivationRules.IsValidDate(20261231));
            Assert.IsTrue(CultivationRules.IsValidDate(20260430));
            Assert.IsTrue(CultivationRules.IsValidDate(20240229)); // leap year
            Assert.IsTrue(CultivationRules.IsValidDate(20000229)); // divisible by 400

            Assert.IsFalse(CultivationRules.IsValidDate(0));
            Assert.IsFalse(CultivationRules.IsValidDate(-20261019));
            Assert.IsFalse(CultivationRules.IsValidDate(9991231));   // three-digit year
            Assert.IsFalse(CultivationRules.IsValidDate(100000101)); // five-digit year
            Assert.IsFalse(CultivationRules.IsValidDate(20260001));  // month 0
            Assert.IsFalse(CultivationRules.IsValidDate(20261301));  // month 13
            Assert.IsFalse(CultivationRules.IsValidDate(20261000));  // day 0
            Assert.IsFalse(CultivationRules.IsValidDate(20261032));  // day 32
            Assert.IsFalse(CultivationRules.IsValidDate(20260431));  // April has 30
            Assert.IsFalse(CultivationRules.IsValidDate(20260229));  // not a leap year
            Assert.IsFalse(CultivationRules.IsValidDate(21000229));  // divisible by 100, not 400
        }

        // ---- Serialization ----

        [Test]
        public void RoundTrip_RestoresAllFields()
        {
            var samples = new[]
            {
                default(CultivationStats),
                new CultivationStats(10, 0, Today),
                new CultivationStats(49, 150, 20240229),
                new CultivationStats(int.MaxValue, int.MaxValue, 99991231),
            };

            foreach (CultivationStats original in samples)
            {
                string json = CultivationJsonSerializer.Serialize(original);
                (int version, CultivationStats restored) = CultivationJsonSerializer.Deserialize(json);

                Assert.AreEqual(CultivationJsonSerializer.CurrentVersion, version);
                AssertStats(restored, original.ExternalPower, original.InternalPower, original.LastTrainingDate);
            }
        }

        [Test]
        public void Serialize_WritesVersionAndFields()
        {
            string json = CultivationJsonSerializer.Serialize(new CultivationStats(40, 20, Today));

            StringAssert.Contains("\"version\": 1", json);
            StringAssert.Contains("\"externalPower\": 40", json);
            StringAssert.Contains("\"internalPower\": 20", json);
            StringAssert.Contains("\"lastTrainingDate\": 20261019", json);
        }

        [Test]
        public void Deserialize_AcceptsCompactForm()
        {
            (_, CultivationStats stats) = CultivationJsonSerializer.Deserialize("{\"version\":1,\"externalPower\":7,\"internalPower\":3,\"lastTrainingDate\":20261019}");

            AssertStats(stats, 7, 3, Today);
        }

        [Test]
        public void Deserialize_RejectsWrongOrMissingVersion()
        {
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize(Json("0", "0", "0", "2")));
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize(Json("0", "0", "0", "0")));
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize("{\"externalPower\": 0, \"internalPower\": 0, \"lastTrainingDate\": 0}"));
        }

        [Test]
        public void Deserialize_RejectsMissingFields()
        {
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize("{\"version\": 1, \"internalPower\": 0, \"lastTrainingDate\": 0}"));
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize("{\"version\": 1, \"externalPower\": 0, \"lastTrainingDate\": 0}"));
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize("{\"version\": 1, \"externalPower\": 0, \"internalPower\": 0}"));
        }

        [Test]
        public void Deserialize_RejectsNegativeValues()
        {
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize(Json("-1", "0", "0")));
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize(Json("0", "-1", "0")));
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize(Json("0", "0", "-1")));
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize(Json("0", "0", "-20261019")));
        }

        [Test]
        public void Deserialize_RejectsFractions()
        {
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize(Json("1.5", "0", "0")));
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize(Json("0", "10.0", "0")));
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize(Json("0", "0", "20261019.5")));
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize(Json("1e2", "0", "0")));
        }

        [Test]
        public void Deserialize_RejectsOutOfRangeValues()
        {
            // Powers: int.MaxValue is the largest accepted value.
            Assert.DoesNotThrow(() => CultivationJsonSerializer.Deserialize(Json("2147483647", "2147483647", "0")));
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize(Json("2147483648", "0", "0")));
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize(Json("0", "2147483648", "0")));
            // Dates: 0 or a real YYYYMMDD date only.
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize(Json("0", "0", "20261301")));
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize(Json("0", "0", "20260230")));
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize(Json("0", "0", "2026101")));
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize(Json("0", "0", "100000101")));
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize(Json("0", "0", "2147483648")));
        }

        [Test]
        public void Deserialize_RejectsWrongTypes()
        {
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize(Json("\"10\"", "0", "0")));
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize(Json("0", "null", "0")));
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize(Json("0", "0", "true")));
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize(Json("0", "0", "\"20261019\"")));
        }

        [Test]
        public void Deserialize_RejectsMalformedJson()
        {
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize("{\"version\": 1, "));
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize(""));
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize("[]"));
            Assert.Throws<CultivationJsonException>(() => CultivationJsonSerializer.Deserialize(Json("0", "0", "0") + "x"));
            Assert.Throws<ArgumentNullException>(() => CultivationJsonSerializer.Deserialize(null));
        }
    }
}
