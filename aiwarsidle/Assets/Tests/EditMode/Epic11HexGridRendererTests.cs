using System.Collections.Generic;
using System.Reflection;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.PvP.Config;
using AIWarsIdle.PvP.Services;
using AIWarsIdle.UI.Pvp;
using NUnit.Framework;
using UnityEngine;

namespace AIWarsIdle.Tests.EditMode
{
    public sealed class Epic11HexGridRendererTests
    {
        [TestCase(0, 1)]
        [TestCase(1, 7)]
        [TestCase(2, 19)]
        [TestCase(3, 37)]
        [TestCase(4, 61)]
        public void HexGridMath_EnumerateAxial_ReturnsExpectedCellCount(int radius, int expectedCount)
        {
            var coords = HexGridMath.EnumerateAxial(radius);

            Assert.AreEqual(expectedCount, coords.Count);
        }

        [Test]
        public void HexGridMath_EnumerateAxial_ReturnsUniqueCoords_ForRadius4()
        {
            var coords = HexGridMath.EnumerateAxial(4);
            var unique = new HashSet<HexCoord>(coords);

            Assert.AreEqual(61, coords.Count);
            Assert.AreEqual(coords.Count, unique.Count);
        }

        [Test]
        public void HexGridMath_GetCornerCoords_ReturnsSixCorners_ForRadius4()
        {
            var corners = HexGridMath.GetCornerCoords(4);

            CollectionAssert.AreEquivalent(
                new[]
                {
                    new HexCoord(4, 0),
                    new HexCoord(0, 4),
                    new HexCoord(-4, 4),
                    new HexCoord(-4, 0),
                    new HexCoord(0, -4),
                    new HexCoord(4, -4)
                },
                corners);
        }

        [TestCase(1, 0, true)]
        [TestCase(7, 1, true)]
        [TestCase(19, 2, true)]
        [TestCase(61, 4, true)]
        [TestCase(62, 0, false)]
        public void HexGridRenderer_TryInferRadiusFromCellCount_Works(int count, int expectedRadius, bool expected)
        {
            var ok = HexGridRenderer.TryInferRadiusFromCellCount(count, out var radius);

            Assert.AreEqual(expected, ok);
            if (expected)
            {
                Assert.AreEqual(expectedRadius, radius);
            }
        }

        [Test]
        public void HexGridRenderer_BuildLiveScoreEntries_SortsByOwnedSectorsThenPower()
        {
            var mapConfig = CreateMapConfig();
            var state = new GameState
            {
                MapState = new MapState
                {
                    Sectors = new[]
                    {
                        new SectorState { SectorId = 0, OwnerPlayerId = 1 },
                        new SectorState { SectorId = 1, OwnerPlayerId = 1 },
                        new SectorState { SectorId = 2, OwnerPlayerId = 3 },
                        new SectorState { SectorId = 3, OwnerPlayerId = 3 },
                        new SectorState { SectorId = 4, OwnerPlayerId = 2 },
                    }
                }
            };

            var pvpConfig = ScriptableObject.CreateInstance<PvpConfig>();
            pvpConfig.PrestigePvpPowerPerPrestige = 0;
            pvpConfig.PermanentPvpPowerPerLevel = 0;
            pvpConfig.SectorPvpPowerPerSector = 1;
            var snapshots = new FactionSnapshotService(state, mapConfig, pvpConfig);

            var method = typeof(HexGridRenderer).GetMethod("BuildLiveScoreEntries", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(MapConfig), typeof(MapState), typeof(FactionSnapshotService) }, null);
            Assert.NotNull(method);

            var result = method.Invoke(null, new object[] { mapConfig, state.MapState, snapshots });
            var entries = (System.Collections.IList)result;

            Assert.AreEqual(3, entries.Count);
            Assert.AreEqual(2, GetEntryField<int>(entries[0], "Score"));
            Assert.AreEqual(2, GetEntryField<int>(entries[1], "Score"));
            Assert.AreEqual(1, GetEntryField<int>(entries[2], "Score"));
            Assert.AreEqual("Player 2", GetEntryField<string>(entries[2], "Name"));

            var firstPower = GetEntryField<double>(entries[0], "PvpPower");
            var secondPower = GetEntryField<double>(entries[1], "PvpPower");
            Assert.GreaterOrEqual(firstPower, secondPower);

            var firstName = GetEntryField<string>(entries[0], "Name");
            var secondName = GetEntryField<string>(entries[1], "Name");
            CollectionAssert.AreEquivalent(new[] { "You", "Player 3" }, new[] { firstName, secondName });

            ScriptableObject.DestroyImmediate(mapConfig);
            ScriptableObject.DestroyImmediate(pvpConfig);
        }

        [Test]
        public void HexGridRenderer_BuildLiveScoreTable_IncludesHeadersAndLocalLabel()
        {
            var method = typeof(HexGridRenderer).GetMethod("BuildLiveScoreTable", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            var text = (string)method.Invoke(null, new object[] { null });

            StringAssert.Contains("PLAYER", text);
            StringAssert.Contains("POWER", text);
            StringAssert.Contains("No active factions", text);
        }

        private static T GetEntryField<T>(object entry, string fieldName)
        {
            var field = entry.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            return (T)field.GetValue(entry);
        }

        private static MapConfig CreateMapConfig()
        {
            var config = ScriptableObject.CreateInstance<MapConfig>();
            config.MapSeasonLengthDays = 7;
            config.MapSeasonAnchorUnixSecondsUtc = 0;
            config.LocalPlayerId = 1;
            config.HomeSectorId = 0;
            config.HomeSectorIds = new[] { 0, 2, 4 };
            config.HomeSectorOwnerPlayerIds = new[] { 1, 2, 3 };
            config.SectorDefinitions = new[]
            {
                new MapConfig.SectorDefinition { SectorId = 0, Name = "A" },
                new MapConfig.SectorDefinition { SectorId = 1, Name = "B" },
                new MapConfig.SectorDefinition { SectorId = 2, Name = "C" },
                new MapConfig.SectorDefinition { SectorId = 3, Name = "D" },
                new MapConfig.SectorDefinition { SectorId = 4, Name = "E" },
            };
            config.EnforceSectorCountRange = false;
            config.RequireConnectedGraph = false;
            return config;
        }
    }
}
