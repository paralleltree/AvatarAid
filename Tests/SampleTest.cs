using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using nadena.dev.ndmf;

namespace Paltee.AvatarAid.Tests
{
    public class SampleTest
    {
        // A Test behaves as an ordinary method
        [Test]
        public void SampleTestSimplePasses()
        {
            // Use the Assert class to test conditions
            var gameObject = new GameObject();
            var ctx = new BuildContext(gameObject, "Assets");
            Assert.NotNull(ctx);
        }
    }
}
