using System;
using Game.Sim.Pvp;
using NUnit.Framework;

namespace Game.Tests
{
    public class PvpTests
    {
        private static void AssertRecord(PvpRecord record, int wins, int losses, int draws)
        {
            Assert.AreEqual(wins, record.Wins, "wins");
            Assert.AreEqual(losses, record.Losses, "losses");
            Assert.AreEqual(draws, record.Draws, "draws");
        }

        // ---- Rules ----

        [Test]
        public void NewRecord_IsAllZero()
        {
            PvpRecord fresh = default;

            AssertRecord(fresh, 0, 0, 0);
        }

        [Test]
        public void RecordWin_IncrementsOnlyWins()
        {
            var start = new PvpRecord(3, 5, 7);

            AssertRecord(PvpRules.RecordWin(start), 4, 5, 7);
            AssertRecord(start, 3, 5, 7);
        }

        [Test]
        public void RecordLoss_IncrementsOnlyLosses()
        {
            var start = new PvpRecord(3, 5, 7);

            AssertRecord(PvpRules.RecordLoss(start), 3, 6, 7);
            AssertRecord(start, 3, 5, 7);
        }

        [Test]
        public void RecordDraw_IncrementsOnlyDraws()
        {
            var start = new PvpRecord(3, 5, 7);

            AssertRecord(PvpRules.RecordDraw(start), 3, 5, 8);
            AssertRecord(start, 3, 5, 7);
        }

        [Test]
        public void Rules_FromFreshRecord()
        {
            AssertRecord(PvpRules.RecordWin(default), 1, 0, 0);
            AssertRecord(PvpRules.RecordLoss(default), 0, 1, 0);
            AssertRecord(PvpRules.RecordDraw(default), 0, 0, 1);
        }

        [Test]
        public void Rules_ThrowInsteadOfWrappingAtIntMax()
        {
            Assert.Throws<OverflowException>(() => PvpRules.RecordWin(new PvpRecord(int.MaxValue, 0, 0)));
            Assert.Throws<OverflowException>(() => PvpRules.RecordLoss(new PvpRecord(0, int.MaxValue, 0)));
            Assert.Throws<OverflowException>(() => PvpRules.RecordDraw(new PvpRecord(0, 0, int.MaxValue)));
        }

        // ---- Serialization ----

        [Test]
        public void RoundTrip_RestoresAllFields()
        {
            var records = new[]
            {
                default(PvpRecord),
                new PvpRecord(1, 2, 3),
                new PvpRecord(42, 0, 9),
                new PvpRecord(int.MaxValue, int.MaxValue, int.MaxValue),
            };

            foreach (PvpRecord original in records)
            {
                string json = PvpJsonSerializer.Serialize(original);
                (int version, PvpRecord restored) = PvpJsonSerializer.Deserialize(json);

                Assert.AreEqual(PvpJsonSerializer.CurrentVersion, version);
                AssertRecord(restored, original.Wins, original.Losses, original.Draws);
            }
        }

        [Test]
        public void Serialize_WritesVersionField()
        {
            string json = PvpJsonSerializer.Serialize(new PvpRecord(4, 2, 1));

            StringAssert.Contains("\"version\": 1", json);
            StringAssert.Contains("\"wins\": 4", json);
            StringAssert.Contains("\"losses\": 2", json);
            StringAssert.Contains("\"draws\": 1", json);
        }

        [Test]
        public void Deserialize_RejectsBadInput()
        {
            // Wrong or missing version
            Assert.Throws<PvpJsonException>(() => PvpJsonSerializer.Deserialize("{\"version\": 2, \"wins\": 0, \"losses\": 0, \"draws\": 0}"));
            Assert.Throws<PvpJsonException>(() => PvpJsonSerializer.Deserialize("{\"wins\": 0, \"losses\": 0, \"draws\": 0}"));
            // Missing fields
            Assert.Throws<PvpJsonException>(() => PvpJsonSerializer.Deserialize("{\"version\": 1, \"losses\": 0, \"draws\": 0}"));
            Assert.Throws<PvpJsonException>(() => PvpJsonSerializer.Deserialize("{\"version\": 1, \"wins\": 0, \"draws\": 0}"));
            Assert.Throws<PvpJsonException>(() => PvpJsonSerializer.Deserialize("{\"version\": 1, \"wins\": 0, \"losses\": 0}"));
            // Negative counts
            Assert.Throws<PvpJsonException>(() => PvpJsonSerializer.Deserialize("{\"version\": 1, \"wins\": -1, \"losses\": 0, \"draws\": 0}"));
            Assert.Throws<PvpJsonException>(() => PvpJsonSerializer.Deserialize("{\"version\": 1, \"wins\": 0, \"losses\": -1, \"draws\": 0}"));
            Assert.Throws<PvpJsonException>(() => PvpJsonSerializer.Deserialize("{\"version\": 1, \"wins\": 0, \"losses\": 0, \"draws\": -1}"));
            // Beyond int range
            Assert.Throws<PvpJsonException>(() => PvpJsonSerializer.Deserialize("{\"version\": 1, \"wins\": 2147483648, \"losses\": 0, \"draws\": 0}"));
            // Wrong types
            Assert.Throws<PvpJsonException>(() => PvpJsonSerializer.Deserialize("{\"version\": 1, \"wins\": \"3\", \"losses\": 0, \"draws\": 0}"));
            Assert.Throws<PvpJsonException>(() => PvpJsonSerializer.Deserialize("{\"version\": 1, \"wins\": 1.5, \"losses\": 0, \"draws\": 0}"));
            // Malformed JSON or wrong root
            Assert.Throws<PvpJsonException>(() => PvpJsonSerializer.Deserialize("{\"version\": 1, "));
            Assert.Throws<PvpJsonException>(() => PvpJsonSerializer.Deserialize("[]"));
            Assert.Throws<ArgumentNullException>(() => PvpJsonSerializer.Deserialize(null));
        }
    }
}
