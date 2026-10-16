using BottleSort.Core;
using NUnit.Framework;

namespace BottleSort.Tests
{
    public class SmokeTests
    {
        [Test]
        public void CoreAssembly_IsReferenced()
        {
            Assert.AreEqual(60, GameInfo.TargetFrameRate);
        }
    }
}
