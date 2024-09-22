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
using VRC.SDK3.Avatars.ScriptableObjects;
using nadena.dev.ndmf;
using nadena.dev.modular_avatar.core;

namespace Paltee.AvatarAid.Tests
{
    public class FaceEmoteProcessorTest
    {
        [Test]
        public void TestProcess_RemovesInstallerComponent()
        {
            var gameObject = new GameObject("Test Target");
            var installerComponent = gameObject.AddComponent<Runtime.FaceEmoteInstaller>();

            var context = new BuildContext(gameObject, "Assets/_TestingResources");

            // act
            var errors = ErrorReport.CaptureErrors(() => new FaceEmoteProcessor().Process(context));

            // assert
            Assert.Zero(errors.Count); // no error

            Assert.IsNull(gameObject.GetComponentInChildren<Runtime.FaceEmoteInstaller>());
        }

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
        public void TestProcess_GeneratesAnimatorWithParameters()
        {
            var gameObject = new GameObject("Test Target");
            var installerComponent = gameObject.AddComponent<Runtime.FaceEmoteInstaller>();

            var context = new BuildContext(gameObject, "Assets/_TestingResources");

            // act
            var errors = ErrorReport.CaptureErrors(() => new FaceEmoteProcessor().Process(context));

            // assert
            Assert.Zero(errors.Count); // no error

            var generatedMergeAnimatorComponent = gameObject.GetComponentInChildren<ModularAvatarMergeAnimator>();
            var animator = generatedMergeAnimatorComponent.animator as AnimatorController;

            var expectedParameters = new (string name, AnimatorControllerParameterType type)[]
            {
                ("GestureLeft", AnimatorControllerParameterType.Int),
                ("GestureRight", AnimatorControllerParameterType.Int),
                ("GestureLeftWeight", AnimatorControllerParameterType.Float),
                ("GestureRightWeight", AnimatorControllerParameterType.Float),
                ("ExpressionSet", AnimatorControllerParameterType.Int),
            };
            var actualParameters = animator.parameters.Select(param => (param.name, param.type));
            CollectionAssert.AreEquivalent(expectedParameters, actualParameters);
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
            var leftHand = generatedAnimator.layers.First(layer => layer.name == "Left Hand");
            ValidateHandLayer(leftHand, "Left");
            var rightHand = generatedAnimator.layers.First(layer => layer.name == "Right Hand");
            ValidateHandLayer(rightHand, "Right");


            // -- local methods

            void ValidateIdleLayer(AnimatorControllerLayer layer)
            {
                Assert.AreEqual(1f, layer.defaultWeight);

                var idleState = layer.stateMachine.defaultState;
                Assert.AreEqual(idleAnim, idleState.motion);
            }

            void ValidateHandLayer(AnimatorControllerLayer layer, string handSide)
            {
                Assert.AreEqual(1f, layer.defaultWeight);

                var initialState = layer.stateMachine.defaultState;
                ValidateEmoteSetStateMachineRoot(initialState, handSide);
            }

            void ValidateEmoteSetStateMachineRoot(AnimatorState root, string handSide)
            {
                Assert.AreEqual(idleAnim, root.motion);
                //Assert.AreEqual(false, root.writeDefaultValues);

                foreach (var trans in root.transitions)
                {
                    // get sub-set Idle state
                    var dest = trans.destinationState;
                    if (!dest.name.StartsWith("Idle "))
                    {
                        Assert.Fail($"unexpected transition from {root.name} to {dest.name}");
                    }
                    var indexMatch = Regex.Match(dest.name, @"Idle (\d+)");
                    if (!indexMatch.Success)
                    {
                        Assert.Fail($"unexpected state name: {trans.destinationState.name}");
                    }
                    int index = int.Parse(indexMatch.Groups[1].Value);

                    // assert layer root to each sub-set Idle transition
                    var rootToChildIdle = trans.conditions.Where(cond => cond.mode == AnimatorConditionMode.Equals && cond.parameter == "ExpressionSet" && cond.threshold == index).ToList();
                    Assert.AreEqual(1, rootToChildIdle.Count);
                    Assert.AreEqual(0, trans.duration);
                    Assert.AreEqual(false, trans.hasExitTime);
                    Assert.AreEqual(true, trans.hasFixedDuration);
                    ValidateEmoteSetStateMachineSubSetRoot(dest, index, handSide);
                }
            }

            void ValidateEmoteSetStateMachineSubSetRoot(AnimatorState node, int setIndex, string handSide)
            {
                // assert each expression set Idle to layer root
                var childIdleToGlobalIdle = node.transitions.Where(trans => trans.destinationState.name == "Idle").Single();
                Assert.AreEqual(0, childIdleToGlobalIdle.duration);
                Assert.AreEqual(false, childIdleToGlobalIdle.hasExitTime);
                Assert.AreEqual(true, childIdleToGlobalIdle.hasFixedDuration);
                Assert.AreEqual(1, childIdleToGlobalIdle.conditions.Length);
                Assert.AreEqual(new AnimatorCondition() { mode = AnimatorConditionMode.NotEqual, parameter = "ExpressionSet", threshold = setIndex }, childIdleToGlobalIdle.conditions[0]);


                foreach (var trans in node.transitions.Where(trans => trans.destinationState.name != "Idle"))
                {
                    // Idle to expression state
                    var cond = trans.conditions.Single();
                    Assert.AreEqual(AnimatorConditionMode.Equals, cond.mode);
                    Assert.AreEqual($"Gesture{handSide}", cond.parameter);

                    Assert.AreEqual(0.1f, trans.duration);
                    Assert.AreEqual(false, trans.hasExitTime);
                    Assert.AreEqual(true, trans.hasFixedDuration);
                    ValidateEmoteSetStateMachineSubSetEmote(trans.destinationState, setIndex, (int)cond.threshold, handSide);
                }
            }

            void ValidateEmoteSetStateMachineSubSetEmote(AnimatorState node, int setIndex, int gestureIndex, string handSide)
            {
                // assert selected motion
                var def = installerComponent.Definitions[setIndex];
                var expectedMotion = gestureIndex switch
                {
                    1 => def.Fist,
                    2 => def.Open,
                    3 => def.Point,
                    4 => def.Peace,
                    5 => def.RockNRoll,
                    6 => def.Gun,
                    7 => def.ThumbsUp,
                    _ => throw new System.ArgumentException()
                };
                if (expectedMotion != null)
                    Assert.AreEqual(expectedMotion, node.motion);
                else
                    Assert.IsTrue(((AnimationClip)node.motion).empty);

                var exitTransitions = node.transitions.Where(trans => trans.destinationState.name == $"Idle {setIndex}").ToList();
                // assert gesture changed transition
                var onGestureChanged = exitTransitions.Where(trans => trans.conditions.Any(cond => cond.mode == AnimatorConditionMode.NotEqual && cond.parameter == $"Gesture{handSide}" && cond.threshold == gestureIndex)).Single();
                Assert.AreEqual(0.1f, onGestureChanged.duration);
                Assert.AreEqual(false, onGestureChanged.hasExitTime);
                Assert.AreEqual(true, onGestureChanged.hasFixedDuration);

                // assert set index changed transition
                var onSetIndexChanged = exitTransitions.Where(trans => trans.conditions.Any(cond => cond.mode == AnimatorConditionMode.NotEqual && cond.parameter == $"ExpressionSet" && cond.threshold == setIndex)).Single();
                Assert.AreEqual(0.1f, onGestureChanged.duration);
                Assert.AreEqual(false, onGestureChanged.hasExitTime);
                Assert.AreEqual(true, onGestureChanged.hasFixedDuration);
            }
        }

        [Test]
        public void TestProcess_GeneratesMAParameters()
        {
            var gameObject = new GameObject("Test Target");
            var installerComponent = gameObject.AddComponent<Runtime.FaceEmoteInstaller>();

            var context = new BuildContext(gameObject, "Assets/_TestingResources");

            // act
            var errors = ErrorReport.CaptureErrors(() => new FaceEmoteProcessor().Process(context));

            // assert
            Assert.Zero(errors.Count); // no error

            var parametersComponent = gameObject.GetComponentInChildren<ModularAvatarParameters>();
            Assert.NotNull(parametersComponent);

            var expectedParams = new ParameterConfig[]
            {
                new ParameterConfig()
                {
                    nameOrPrefix = "ExpressionSet",
                    syncType = ParameterSyncType.Int,
                    saved = true,
                    defaultValue = 0,
                },
            };
            CollectionAssert.AreEquivalent(expectedParams, parametersComponent.parameters);
        }

        [Test]
        public void TestProcess_GeneratesMAMenuItem()
        {
            var gameObject = new GameObject("Test Target");
            var installerComponent = gameObject.AddComponent<Runtime.FaceEmoteInstaller>();

            installerComponent.Definitions = new List<Runtime.ExpressionSetDefinition>();
            installerComponent.Definitions.Add(new Runtime.ExpressionSetDefinition());
            installerComponent.Definitions.Add(new Runtime.ExpressionSetDefinition());

            var context = new BuildContext(gameObject, "Assets/_TestingResources");

            // act
            var errors = ErrorReport.CaptureErrors(() => new FaceEmoteProcessor().Process(context));

            // assert
            Assert.Zero(errors.Count); // no error

            var menuInstaller = gameObject.GetComponentInChildren<ModularAvatarMenuInstaller>();
            Assert.IsNotNull(menuInstaller);

            var rootMenu = menuInstaller.gameObject;
            Assert.IsNotNull(rootMenu);

            var rootMenuItem = rootMenu.GetComponent<ModularAvatarMenuItem>();
            Assert.AreEqual(SubmenuSource.Children, rootMenuItem.MenuSource);
            Assert.AreEqual(VRCExpressionsMenu.Control.ControlType.SubMenu, rootMenuItem.Control.type);

            var children = rootMenuItem.GetComponentsInChildren<ModularAvatarMenuItem>().Where(component => component.gameObject != rootMenu).ToList();
            Assert.IsTrue(children.All(item =>
                item.Control.parameter.name == "ExpressionSet" &&
                item.Control.type == VRCExpressionsMenu.Control.ControlType.Toggle &&
                item.gameObject.name == $"Set {item.Control.value}"
            ));

            var expectedIndexSet = Enumerable.Range(0, installerComponent.Definitions.Count);
            CollectionAssert.AreEquivalent(expectedIndexSet, children.Select(item => item.Control.value));
        }
    }
}
