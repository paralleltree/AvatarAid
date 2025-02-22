using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using nadena.dev.ndmf;

namespace Paltee.AvatarAid.Tests
{
    public class LiltoonDistanceFadeProcessorTest
    {
        [Test]
        public void TestProcess_SetsMaterialParameters()
        {
            var gameObject = new GameObject("Test Target");
            var shader = Shader.Find("Custom/MockLiltoon");
            var mat = new Material(shader);
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            Assert.AreEqual(0, mat.GetColor("_DistanceFade").b);

            var installerComponent = gameObject.AddComponent<Runtime.ApplyLiltoonDistanceFade>();

            var context = new BuildContext(gameObject, "Assets/_TestingResources");

            // act
            var errors = ErrorReport.CaptureErrors(() => new LiltoonDistanceFadeProcessor().Process(context));

            // assert
            Assert.Zero(errors.Count);
            Assert.IsNull(gameObject.GetComponentInChildren<Runtime.ApplyLiltoonDistanceFade>());

            // ensure the original material was not modified
            var originalFadeParamColor = mat.GetColor("_DistanceFade");
            Assert.AreEqual(0, originalFadeParamColor.b);
            // check modified parameter value
            var modifiedRenderer = gameObject.GetComponent<Renderer>();
            var fadeParamColor = modifiedRenderer.sharedMaterial.GetColor("_DistanceFade");
            Assert.AreEqual(1, fadeParamColor.b);
        }
    }
}
