using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using nadena.dev.ndmf;
using nadena.dev.modular_avatar.core;

namespace Paltee.AvatarAid
{
    public class FaceEmoteProcessor
    {
        private readonly AnimationClip EmptyAnimation = new AnimationClip() { name = "empty" };
        protected AnimationClip IdleAnimation => EmptyAnimation;

        protected readonly string ExpressionSetParameterName = "ExpressionSet";

        public void Process(BuildContext context)
        {
            var installerComponent = context.AvatarRootObject.GetComponent<Runtime.FaceEmoteInstaller>();
            if (installerComponent == null) return;

            Apply(context, installerComponent);

            UnityEngine.Object.DestroyImmediate(installerComponent);
        }

        protected void Apply(BuildContext buildContext, Runtime.FaceEmoteInstaller installer)
        {
            var targetGameObject = new GameObject("BuiltFaceEmote");
            targetGameObject.transform.parent = buildContext.AvatarRootObject.transform;

            ApplyMergeAnimator(installer, targetGameObject);
        }

        protected void ApplyMergeAnimator(Runtime.FaceEmoteInstaller installer, GameObject target)
        {
            var mergeAnimator = target.AddComponent<ModularAvatarMergeAnimator>();
            mergeAnimator.animator = GenerateAnimatorController(installer);
            mergeAnimator.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
        }

        protected AnimatorController GenerateAnimatorController(Runtime.FaceEmoteInstaller installer)
        {
            var controller = new AnimatorController();
            controller.AddLayer(GenerateIdleLayer(installer));
            controller.AddLayer(GenerateHandLayer(installer, "Left"));
            controller.AddLayer(GenerateHandLayer(installer, "Right"));
            return controller;
        }

        protected AnimatorControllerLayer GenerateIdleLayer(Runtime.FaceEmoteInstaller installer)
        {
            var layer = new AnimatorControllerLayer()
            {
                name = "Idle",
                defaultWeight = 1f,
                stateMachine = new AnimatorStateMachine()
            };

            var state = layer.stateMachine.AddState("Idle");
            state.writeDefaultValues = false;
            state.motion = installer.IdleMotion;

            return layer;
        }

        protected AnimatorControllerLayer GenerateHandLayer(Runtime.FaceEmoteInstaller installer, string handSide)
        {
            var layer = new AnimatorControllerLayer()
            {
                name = $"{handSide} Hand",
                defaultWeight = 1f,
                stateMachine = new AnimatorStateMachine(),
            };

            var idleState = new AnimatorState()
            {
                name = "Idle",
                motion = installer.IdleMotion,
            };
            layer.stateMachine.AddState(idleState, new Vector3(-100, 0));
            layer.stateMachine.defaultState = idleState;

            if (installer.Definitions == null) installer.Definitions = new List<Runtime.ExpressionSetDefinition>();
            for (int i = 0; i < installer.Definitions.Count; i++)
            {
                var def = installer.Definitions[i];
                var initialState = new AnimatorState()
                {
                    name = $"Idle {i}",
                    motion = IdleAnimation,
                };

                var fistState = new AnimatorState()
                {
                    name = $"Fist {i}",
                    motion = def.Fist ?? EmptyAnimation,
                };

                var openState = new AnimatorState()
                {
                    name = $"Open {i}",
                    motion = def.Open ?? EmptyAnimation,
                };

                var pointState = new AnimatorState()
                {
                    name = $"Point {i}",
                    motion = def.Point ?? EmptyAnimation,
                };

                var peaceState = new AnimatorState()
                {
                    name = $"Peace {i}",
                    motion = def.Peace ?? EmptyAnimation,
                };

                var rockNRollState = new AnimatorState()
                {
                    name = $"RockNRoll {i}",
                    motion = def.RockNRoll ?? EmptyAnimation,
                };

                var gunState = new AnimatorState()
                {
                    name = $"Gun {i}",
                    motion = def.Gun ?? EmptyAnimation,
                };

                var thumbsUpState = new AnimatorState()
                {
                    name = $"Thumbs up {i}",
                    motion = def.ThumbsUp ?? EmptyAnimation,
                };

                layer.stateMachine.AddState(initialState, new Vector3(i * 100, 0));
                var gestureStates = new AnimatorState[]
                {
                    fistState, openState, pointState, peaceState, rockNRollState, gunState, thumbsUpState,
                };

                // ExpressionSet切り替えの遷移
                var idleToExpressionSetInitialState = idleState.AddTransitionForExpression(initialState, 0);
                idleToExpressionSetInitialState.AddCondition(AnimatorConditionMode.Equals, i, ExpressionSetParameterName);
                // ExpressionSet切り替えの戻り
                var expressionSetInitialStateToIdle = initialState.AddTransitionForExpression(idleState, 0);
                expressionSetInitialStateToIdle.AddCondition(AnimatorConditionMode.NotEqual, i, ExpressionSetParameterName);

                // Idleと各表情間の遷移
                string gestureParamName = handSide == "Left" ? "GestureLeft" : "GestureRight"; // TODO: improve
                for (int j = 0; j < gestureStates.Length; j++)
                {
                    layer.stateMachine.AddState(gestureStates[j], new Vector3(i * 100, j * 100 + 100));

                    // Idle -> Expression
                    var initialToExpressionTrans = initialState.AddTransitionForExpression(gestureStates[j], 0.1f);
                    initialToExpressionTrans.AddCondition(AnimatorConditionMode.Equals, j + 1, gestureParamName);

                    // Expression -> Idle(not eq gesture or not eq expressionSet)
                    var expressionToInitialTransForGesture = gestureStates[j].AddTransitionForExpression(initialState, 0.1f);
                    expressionToInitialTransForGesture.AddCondition(AnimatorConditionMode.NotEqual, j + 1, gestureParamName);
                    var expressionToInitialTransForExpressionSet = gestureStates[j].AddTransitionForExpression(initialState, 0.1f);
                    expressionToInitialTransForExpressionSet.AddCondition(AnimatorConditionMode.NotEqual, i, ExpressionSetParameterName);
                }
            }

            return layer;
        }
    }

    public static class FaceEmoteExtensions
    {
        public static AnimatorStateTransition AddTransitionForExpression(this AnimatorState state, AnimatorState dest, float duration)
        {
            var trans = state.AddTransition(dest);
            trans.hasExitTime = false;
            trans.hasFixedDuration = true;
            trans.duration = duration;
            return trans;
        }
    }
}
