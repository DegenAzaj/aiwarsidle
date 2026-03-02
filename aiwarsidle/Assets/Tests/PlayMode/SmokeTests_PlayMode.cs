using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace AIWarsIdle.Tests
{
    public sealed class SmokeTests_PlayMode
    {
        [UnityTest]
        public IEnumerator PlayMode_TestHarness_IsWorking()
        {
            yield return null;
            Assert.Pass("PlayMode test harness OK");
        }
    }
}

