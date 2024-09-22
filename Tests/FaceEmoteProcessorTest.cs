using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using nadena.dev.ndmf;

namespace Paltee.AvatarAid.Tests
{
    public class FaceEmoteProcessorTest
    {
        [Test]
        public void TestAvatarEmoteBuilderSimplePasses()
        {
            var gameObject = new GameObject("Test Target");
            var context = new BuildContext(gameObject, "Assets/_TestingResources");

            // act
            var errors = ErrorReport.CaptureErrors(() => new FaceEmoteProcessor().Process(context));

            // assert
            Assert.Zero(errors.Count); // no error
        }
    }
}
