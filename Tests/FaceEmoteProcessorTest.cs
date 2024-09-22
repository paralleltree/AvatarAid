using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine.TestTools;
using VRC.SDK3.Avatars.Components;
using nadena.dev.ndmf;
using nadena.dev.modular_avatar.core;

namespace Paltee.AvatarAid.Tests
{
    public class FaceEmoteProcessorTest
    {
        [Test]
        public void TestProcess_GeneratesMAMergeAnimator()
        {
            var gameObject = new GameObject("Test Target");
            var installerComponent = gameObject.AddComponent<Runtime.FaceEmoteInstaller>();

            var fistAnim = new AnimationClip();
            var fistAnimEvent = new AnimationEvent() { stringParameter = "testFist", floatParameter = 10f };
            AnimationUtility.SetAnimationEvents(fistAnim, new[] { fistAnimEvent });

            var setDef = new Runtime.ExpressionSetDefinition()
            {
                Fist = fistAnim,
            };

            installerComponent.Definitions = new List<Runtime.ExpressionSetDefinition>();
            installerComponent.Definitions.Add(setDef);

            var context = new BuildContext(gameObject, "Assets/_TestingResources");

            // act
            var errors = ErrorReport.CaptureErrors(() => new FaceEmoteProcessor().Process(context));

            // assert
            Assert.Zero(errors.Count); // no error

            var generatedMergeAnimatorComponent = gameObject.GetComponentInChildren<ModularAvatarMergeAnimator>();
            Assert.IsNotNull(generatedMergeAnimatorComponent);

            Assert.AreEqual(VRCAvatarDescriptor.AnimLayerType.FX, generatedMergeAnimatorComponent.layerType);
            Assert.AreEqual(MergeAnimatorPathMode.Absolute, generatedMergeAnimatorComponent.pathMode);
        }
    }
}
