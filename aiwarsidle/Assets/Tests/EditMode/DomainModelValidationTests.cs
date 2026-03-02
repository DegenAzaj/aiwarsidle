using System;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.GameCore.Validation;
using NUnit.Framework;

namespace AIWarsIdle.Tests
{
    public sealed class DomainModelValidationTests
    {
        [Test]
        public void SectorState_Validation_Allows_Valid_Values()
        {
            var sector = new SectorState
            {
                SectorId = 1,
                Stability = 0f,
                LastCombatUnixSeconds = 0,
                CapturedUnixSeconds = 0
            };

            Assert.DoesNotThrow(() => DomainValidation.ValidateSectorState(sector));
        }

        [Test]
        public void SectorState_Validation_Rejects_Invalid_Stability()
        {
            var sector = new SectorState
            {
                SectorId = 1,
                Stability = 101f
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => DomainValidation.ValidateSectorState(sector));
        }

        [Test]
        public void SectorState_Validation_Rejects_Negative_Timestamps()
        {
            var sector = new SectorState
            {
                SectorId = 1,
                Stability = 50f,
                LastCombatUnixSeconds = -1
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => DomainValidation.ValidateSectorState(sector));
        }

        [Test]
        public void OverclockState_Validation_Rejects_OutOfRange_Charges()
        {
            var state = new OverclockState
            {
                Charges = 3,
                ActiveUntilUnixSeconds = 0,
                NextChargeAtUnixSeconds = 0
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => DomainValidation.ValidateOverclockState(state, maxCharges: 2));
        }

        [Test]
        public void OverclockState_Validation_Rejects_Negative_Timestamps()
        {
            var state = new OverclockState
            {
                Charges = 1,
                ActiveUntilUnixSeconds = -1
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => DomainValidation.ValidateOverclockState(state, maxCharges: 2));
        }
    }
}

