using AIWarsIdle.GameCore.Domain;
using NUnit.Framework;
using Newtonsoft.Json;

namespace AIWarsIdle.Tests
{
    public sealed class DomainModelSerializationTests
    {
        [Test]
        public void GameState_Json_Roundtrip_Preserves_Key_Fields()
        {
            var state = new GameState
            {
                SoftCurrency = 123.45,
                PremiumCurrency = 7,
                PrestigeCount = 2,
                PermanentUpgradeLevel = 1,
                GlobalMultiplier = 1.5f,
                LeagueSeasonId = 10,
                League = 3,
                SeasonPoints = 42,
                PvpAttacksRemaining = 5,
                LastPvpAttackRegenUnixSeconds = 100,
                LastPvpAdAttackClaimUnixSeconds = 200,
                LastLoginUnixSeconds = 300
            };
            state.GeneratorLevels[0] = 11;
            state.MapState.MapSeasonId = 5;

            var json = JsonConvert.SerializeObject(state);
            var loaded = JsonConvert.DeserializeObject<GameState>(json);

            Assert.NotNull(loaded);
            Assert.AreEqual(state.SoftCurrency, loaded!.SoftCurrency);
            Assert.AreEqual(state.PremiumCurrency, loaded.PremiumCurrency);
            Assert.AreEqual(state.PrestigeCount, loaded.PrestigeCount);
            Assert.AreEqual(state.PermanentUpgradeLevel, loaded.PermanentUpgradeLevel);
            Assert.AreEqual(state.GlobalMultiplier, loaded.GlobalMultiplier);
            Assert.AreEqual(state.LeagueSeasonId, loaded.LeagueSeasonId);
            Assert.AreEqual(state.League, loaded.League);
            Assert.AreEqual(state.SeasonPoints, loaded.SeasonPoints);
            Assert.AreEqual(state.PvpAttacksRemaining, loaded.PvpAttacksRemaining);
            Assert.AreEqual(state.LastPvpAttackRegenUnixSeconds, loaded.LastPvpAttackRegenUnixSeconds);
            Assert.AreEqual(state.LastPvpAdAttackClaimUnixSeconds, loaded.LastPvpAdAttackClaimUnixSeconds);
            Assert.AreEqual(state.LastLoginUnixSeconds, loaded.LastLoginUnixSeconds);

            Assert.NotNull(loaded.GeneratorLevels);
            Assert.AreEqual(GameState.GeneratorCount, loaded.GeneratorLevels.Length);
            Assert.AreEqual(11, loaded.GeneratorLevels[0]);

            Assert.NotNull(loaded.MapState);
            Assert.AreEqual(5, loaded.MapState.MapSeasonId);
        }
    }
}

