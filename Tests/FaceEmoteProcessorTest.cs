using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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

        [Test]
        public void TestProcess_GeneratesAnimatorBasedOnFaceEmoteDefinition()
        {

            var gameObject = new GameObject("Test Target");
            var installerComponent = gameObject.AddComponent<Runtime.FaceEmoteInstaller>();

            var idleAnim = new AnimationClip();
            var idleAnimEvent = new AnimationEvent() { stringParameter = "testFist", time = 10f, floatParameter = 0f };
            AnimationUtility.SetAnimationEvents(idleAnim, new[] { idleAnimEvent });
            var fistAnim = new AnimationClip();
            var fistAnimEvent = new AnimationEvent() { stringParameter = "testFist", time = 10f, floatParameter = 0f };
            AnimationUtility.SetAnimationEvents(fistAnim, new[] { fistAnimEvent });
            var openAnim = new AnimationClip();
            var openAnimEvent = new AnimationEvent() { stringParameter = "testOpen", time = 0f, floatParameter = 1f };
            AnimationUtility.SetAnimationEvents(openAnim, new[] { openAnimEvent });

            var setDef0 = new Runtime.ExpressionSetDefinition()
            {
                Fist = fistAnim,
            };
            var setDef1 = new Runtime.ExpressionSetDefinition()
            {
                Open = fistAnim,
            };

            installerComponent.IdleMotion = idleAnim;
            installerComponent.Definitions = new List<Runtime.ExpressionSetDefinition>();
            installerComponent.Definitions.Add(setDef0);
            installerComponent.Definitions.Add(setDef1);

            var context = new BuildContext(gameObject, "Assets/_TestingResources");

            // act
            var errors = ErrorReport.CaptureErrors(() => new FaceEmoteProcessor().Process(context));

            // assert
            Assert.Zero(errors.Count); // no error

            var generatedMergeAnimatorComponent = gameObject.GetComponentInChildren<ModularAvatarMergeAnimator>();
            var generatedAnimator = generatedMergeAnimatorComponent.animator as AnimatorController;
            Assert.IsNotNull(generatedAnimator);

            CollectionAssert.AreEqual(new[] { "Idle", "Left Hand", "Right Hand" }, generatedAnimator.layers.Select(layer => layer.name));
            var idle = generatedAnimator.layers.First(layer => layer.name == "Idle");
            ValidateIdleLayer(idle);

            // -- local methods

            void ValidateIdleLayer(AnimatorControllerLayer layer)
            {
                Assert.AreEqual(1f, layer.defaultWeight);

                var idleState = layer.stateMachine.defaultState;
                Assert.AreEqual(idleAnim, idleState.motion);
            }
        }
    }
}
