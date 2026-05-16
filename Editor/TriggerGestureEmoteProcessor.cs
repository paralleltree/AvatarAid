using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using Paltee.AvatarAid.Runtime;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Paltee.AvatarAid
{
    public class TriggerGestureEmoteProcessor
    {
        public void Process(BuildContext context)
        {
            var installerComponents = context.AvatarRootObject.GetComponentsInChildren<TriggerGestureEmoteInstaller>();
            foreach (var installer in installerComponents)
            {
                Apply(context, installer);
                UnityEngine.Object.DestroyImmediate(installer);
            }
        }

        public void Apply(BuildContext context, TriggerGestureEmoteInstaller installer)
        {
            var targetGameObject = installer.gameObject;

            ApplyMergeAnimator(installer, targetGameObject);
            ApplyMAParameters(installer, targetGameObject);
            ApplyMenuInstaller(installer, targetGameObject);
        }

        protected void ApplyMergeAnimator(TriggerGestureEmoteInstaller installer, GameObject targetGameObject)
        {
            var mergeAnimator = targetGameObject.AddComponent<ModularAvatarMergeAnimator>();
            mergeAnimator.animator = GenerateAnimatorController(installer);
            mergeAnimator.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
        }

        protected void ApplyMAParameters(TriggerGestureEmoteInstaller installer, GameObject targetGameObject)
        {
            var parameter = targetGameObject.AddComponent<ModularAvatarParameters>();
            parameter.parameters = new()
            {
                new ParameterConfig()
                {
                    nameOrPrefix = GetEnableTriggerControlParameterName(installer.TargetName),
                    defaultValue = 0f,
                    saved = false,
                    syncType = ParameterSyncType.Bool,
                },
                new ParameterConfig()
                {
                    nameOrPrefix = GetIsTriggerRightHandParameterName(installer.TargetName),
                    defaultValue = 0f,
                    saved = false,
                    syncType = ParameterSyncType.Bool,
                }
            };
        }

        protected void ApplyMenuInstaller(TriggerGestureEmoteInstaller installer, GameObject targetGameObject)
        {
            if (targetGameObject.GetComponentInParent<ModularAvatarMenuInstaller>() == null)
            {
                // TODO: fail building avatar with error message
                Debug.LogError($"TriggerGestureEmoteInstaller requires ModularAvatarMenuInstaller in parent hierarchy. TargetName: {installer.TargetName}");
                return;
            }

            var rootMenuItem = targetGameObject.GetComponent<ModularAvatarMenuItem>();
            if (rootMenuItem == null)
            {
                rootMenuItem = targetGameObject.AddComponent<ModularAvatarMenuItem>();
            }
            rootMenuItem.MenuSource = SubmenuSource.Children;
            rootMenuItem.Control = new VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control()
            {
                type = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType.SubMenu,
            };

            ModularAvatarMenuItem GenerateChildMenuItem(ModularAvatarMenuItem parent, string name)
            {
                var gameObject = new GameObject(name);
                gameObject.transform.parent = parent.transform;
                var menuItem = gameObject.AddComponent<ModularAvatarMenuItem>();
                menuItem.Control = new VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control();
                return menuItem;
            }

            ModularAvatarMenuItem AddToggleMenuItem(ModularAvatarMenuItem parent, string name, float value)
            {
                var menuItem = GenerateChildMenuItem(parent, name);
                menuItem.Control.type = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType.Toggle;
                menuItem.Control.parameter = new VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.Parameter()
                {
                    name = GetTriggerControlLocalIndexParameterName(installer.TargetName),
                };
                menuItem.Control.value = value;
                menuItem.isSynced = false;
                menuItem.isSaved = false;
                return menuItem;
            }

            AddToggleMenuItem(rootMenuItem, "Off", 0f);
            AddToggleMenuItem(rootMenuItem, "Right Hand", 2f);
            AddToggleMenuItem(rootMenuItem, "Left Hand", 1f);
            // リングメニューの位置調整用アイテム
            AddToggleMenuItem(rootMenuItem, "Off", 0f);
        }

        protected string GetEnableTriggerControlParameterName(string kind) => $"{kind}_EnableTriggerControl";
        protected string GetIsTriggerRightHandParameterName(string kind) => $"{kind}_IsTriggerRightHand";
        protected readonly string ResetTriggerControlParameterName = $"ResetTriggerControl";
        protected string GetTriggerControlLocalIndexParameterName(string kind) => $"{kind}_TriggerControlLocalIndex";
        protected readonly string GestureWeightPlaceholderParameterName = $"GestureWeightPlaceholder";

        protected AnimatorControllerParameter[] GenerateParameters(string kind)
        {
            return new AnimatorControllerParameter[]
            {
                new AnimatorControllerParameter()
                {
                    name = GetEnableTriggerControlParameterName(kind),
                    type = AnimatorControllerParameterType.Bool,
                },
                new AnimatorControllerParameter()
                {
                    name = GetIsTriggerRightHandParameterName(kind),
                    type = AnimatorControllerParameterType.Bool,
                },
                new AnimatorControllerParameter()
                {
                    name = ResetTriggerControlParameterName,
                    type = AnimatorControllerParameterType.Bool
                },
                new AnimatorControllerParameter()
                {
                    name = GetTriggerControlLocalIndexParameterName(kind),
                    type = AnimatorControllerParameterType.Int
                },
                new AnimatorControllerParameter()
                {
                    name = GestureWeightPlaceholderParameterName,
                    type = AnimatorControllerParameterType.Float
                },
            };
        }

        protected AnimatorController GenerateAnimatorController(TriggerGestureEmoteInstaller installer)
        {
            AnimatorState GenerateAnimatorState(string name) => new AnimatorState()
            {
                name = name,
                writeDefaultValues = true,
            };
            AnimatorState GenerateAnimatorStateWithAnimation(string name, AnimationClip clip) => new AnimatorState()
            {
                name = name,
                motion = clip,
                writeDefaultValues = true,
            };

            /// <summary>
            /// 実際にアニメーションを再生するレイヤー
            /// </summary>
            AnimatorControllerLayer GenerateTriggerControlLayer()
            {
                var layer = new AnimatorControllerLayer()
                {
                    name = $"{installer.TargetName}_TriggerControlLayer",
                    defaultWeight = 1f,
                    stateMachine = new AnimatorStateMachine(),
                    blendingMode = installer.IsAdditive ? AnimatorLayerBlendingMode.Additive : AnimatorLayerBlendingMode.Override,
                };

                AnimatorState GenerateTransitionDestinationState(AnimatorState idleState, HandSide triggerHand, AnimationClip clip, float transitionDuration)
                {
                    var destState = GenerateAnimatorStateWithAnimation($"{triggerHand}TriggerGesture", clip);
                    destState.timeParameter = $"Gesture{triggerHand}Weight";
                    destState.timeParameterActive = true;

                    var idleToDestTransition = idleState.AddTransitionForExpression(destState, transitionDuration);
                    idleToDestTransition.AddCondition(AnimatorConditionMode.Equals, 1f, GetEnableTriggerControlParameterName(installer.TargetName));
                    idleToDestTransition.AddCondition(AnimatorConditionMode.Equals, triggerHand == HandSide.Right ? 1 : 0, GetIsTriggerRightHandParameterName(installer.TargetName));

                    var destToIdleTransitionByDisabled = destState.AddTransitionForExpression(idleState, transitionDuration);
                    destToIdleTransitionByDisabled.AddCondition(AnimatorConditionMode.NotEqual, 1f, GetEnableTriggerControlParameterName(installer.TargetName));

                    var destToIdleTransitionByHandSideChange = destState.AddTransitionForExpression(idleState, transitionDuration);
                    destToIdleTransitionByHandSideChange.AddCondition(AnimatorConditionMode.NotEqual, triggerHand == HandSide.Right ? 1 : 0, GetIsTriggerRightHandParameterName(installer.TargetName));

                    return destState;
                }

                var idleState = GenerateAnimatorState("Idle");
                layer.stateMachine.AddState(idleState, new Vector3(0, 0));
                layer.stateMachine.defaultState = idleState;

                var leftTriggerGestureState = GenerateTransitionDestinationState(idleState, HandSide.Left, installer.Animation, installer.TransitionSeconds);
                var rightTriggerGestureState = GenerateTransitionDestinationState(idleState, HandSide.Right, installer.Animation, installer.TransitionSeconds);
                layer.stateMachine.AddState(leftTriggerGestureState, new Vector3(200, 0));
                layer.stateMachine.AddState(rightTriggerGestureState, new Vector3(200, 200));

                return layer;
            }

            /// <summary>
            /// 操作状態をローカルで管理するためのレイヤー
            /// </summary>
            /// <remarks>
            /// リングメニューの状態はこのレイヤーで操作(Intでメニュー選択状況を管理)する
            /// このレイヤーで実際にアニメーションを再生するTriggerControlLayerを駆動する
            /// TriggerControlLayer側で使う2bits分のパラメーターを変数同期することで、このレイヤーのIntは同期不要とし同期パラメーターを削減している
            /// </remarks>
            AnimatorControllerLayer GenerateConvertBitsLayer()
            {
                var layer = new AnimatorControllerLayer()
                {
                    name = $"{installer.TargetName}_ConvertBitsLayer",
                    defaultWeight = 1f,
                    stateMachine = new AnimatorStateMachine(),
                };

                AnimatorState GenerateSwitchTriggerEnabledState(AnimatorState idleState, HandSide handSide)
                {
                    var enableTriggerState = GenerateAnimatorState($"Enable{handSide}Trigger");

                    // --- Parameter Drivers
                    // Enabled状態にして左右の手の選択状況を反映する
                    var driver = enableTriggerState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                    driver.parameters = new List<VRC.SDKBase.VRC_AvatarParameterDriver.Parameter>()
                    {
                        new()
                        {
                            type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set,
                            name = GetEnableTriggerControlParameterName(installer.TargetName),
                            value = 1f,
                        },
                        new()
                        {
                            type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set,
                            name = GetIsTriggerRightHandParameterName(installer.TargetName),
                            value = handSide == HandSide.Right ? 1f : 0f,
                        }
                    };

                    // --- Transitions
                    var idleToEnableTransition = idleState.AddImmediateTransition(enableTriggerState);
                    idleToEnableTransition.AddCondition(AnimatorConditionMode.Equals, (float)handSide, GetTriggerControlLocalIndexParameterName(installer.TargetName));

                    var enableToIdleTransition = enableTriggerState.AddImmediateTransition(idleState);
                    enableToIdleTransition.AddCondition(AnimatorConditionMode.NotEqual, (float)handSide, GetTriggerControlLocalIndexParameterName(installer.TargetName));

                    return enableTriggerState;
                }

                var idleState = GenerateAnimatorState("Idle");
                layer.stateMachine.AddState(idleState, new Vector3(0, 0));
                layer.stateMachine.defaultState = idleState;

                // Idle時はEnabledを切る
                var idleParameterDriver = idleState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                idleParameterDriver.parameters = new List<VRC.SDKBase.VRC_AvatarParameterDriver.Parameter>()
                {
                    new()
                    {
                        type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set,
                        name = GetEnableTriggerControlParameterName(installer.TargetName),
                        value = 0f,
                    },
                };


                var leftEnableTriggerState = GenerateSwitchTriggerEnabledState(idleState, HandSide.Left);
                var rightEnableTriggerState = GenerateSwitchTriggerEnabledState(idleState, HandSide.Right);
                layer.stateMachine.AddState(leftEnableTriggerState, new Vector3(200, 0));
                layer.stateMachine.AddState(rightEnableTriggerState, new Vector3(200, 200));

                return layer;
            }

            AnimatorControllerLayer GenerateResetLayer()
            {
                var layer = new AnimatorControllerLayer()
                {
                    name = $"{installer.TargetName}_ResetLayer",
                    defaultWeight = 1f,
                    stateMachine = new AnimatorStateMachine(),
                };

                var idleState = GenerateAnimatorState("Idle");
                layer.stateMachine.AddState(idleState, new Vector3(0, 0));
                layer.stateMachine.defaultState = idleState;

                var resetState = GenerateAnimatorState("Reset");
                layer.stateMachine.AddState(resetState, new Vector3(200, 0));

                // ローカル管理用のパラメーターを0にセットしてTriggerのEnabled状態を切る
                var resetDriver = resetState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                resetDriver.parameters = new List<VRC.SDKBase.VRC_AvatarParameterDriver.Parameter>()
                {
                    new()
                    {
                        type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set,
                        name = GetTriggerControlLocalIndexParameterName(installer.TargetName),
                        value = 0f,
                    },
                };

                var idleToResetTransition = idleState.AddImmediateTransition(resetState);
                idleToResetTransition.AddCondition(AnimatorConditionMode.Equals, 1f, ResetTriggerControlParameterName);

                var resetToIdleTransition = resetState.AddImmediateTransition(idleState);
                resetToIdleTransition.AddCondition(AnimatorConditionMode.NotEqual, 1f, ResetTriggerControlParameterName);

                return layer;
            }

            var controller = new AnimatorController()
            {
                parameters = GenerateParameters(installer.TargetName),
                layers = new AnimatorControllerLayer[]
                {
                    GenerateTriggerControlLayer(),
                    GenerateConvertBitsLayer(),
                    GenerateResetLayer(),
                },
            };

            return controller;
        }
    }
}
