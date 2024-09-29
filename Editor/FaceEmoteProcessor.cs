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
            ApplyMAParameters(targetGameObject);
            ApplyMAMenuInstaller(installer, targetGameObject);
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
            var controller = new AnimatorController()
            {
                parameters = GenerateParameters(),
            };
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

            var idleState = GenerateAnimatorState("Idle", EmptyAnimation);
            layer.stateMachine.AddState(idleState, new Vector3(-100, 0));
            layer.stateMachine.defaultState = idleState;

            AnimatorState GenerateAnimatorState(string name, AnimationClip motion)
            {
                return new AnimatorState()
                {
                    name = name,
                    motion = motion ?? EmptyAnimation,
                    writeDefaultValues = false,
                };
            }

            if (installer.Definitions == null) installer.Definitions = new List<Runtime.ExpressionSetDefinition>();
            for (int i = 0; i < installer.Definitions.Count; i++)
            {
                var def = installer.Definitions[i];
                var initialState = GenerateAnimatorState($"Idle {i}", EmptyAnimation);

                layer.stateMachine.AddState(initialState, new Vector3(i * 100, 0));
                var gestureStates = new AnimatorState[]
                {
                    GenerateAnimatorState($"Fist {i}", def.Fist),
                    GenerateAnimatorState($"Open {i}", def.Open),
                    GenerateAnimatorState( $"Point {i}", def.Point),
                    GenerateAnimatorState($"Peace {i}", def.Peace),
                    GenerateAnimatorState($"RockNRoll {i}", def.RockNRoll),
                    GenerateAnimatorState($"Thumbs up {i}",def.ThumbsUp),
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
                    var initialToExpressionTrans = initialState.AddTransitionForExpression(gestureStates[j], installer.TransitionSeconds);
                    initialToExpressionTrans.AddCondition(AnimatorConditionMode.Equals, j + 1, gestureParamName);

                    // Expression -> Idle(not eq gesture or not eq expressionSet)
                    var expressionToInitialTransForGesture = gestureStates[j].AddTransitionForExpression(initialState, installer.TransitionSeconds);
                    expressionToInitialTransForGesture.AddCondition(AnimatorConditionMode.NotEqual, j + 1, gestureParamName);
                    var expressionToInitialTransForExpressionSet = gestureStates[j].AddTransitionForExpression(initialState, installer.TransitionSeconds);
                    expressionToInitialTransForExpressionSet.AddCondition(AnimatorConditionMode.NotEqual, i, ExpressionSetParameterName);
                }
            }

            return layer;
        }

        protected void ApplyMAParameters(GameObject target)
        {
            var parameter = target.AddComponent<ModularAvatarParameters>();
            var conf = new ParameterConfig()
            {
                nameOrPrefix = ExpressionSetParameterName,
                defaultValue = 0,
                saved = true,
                syncType = ParameterSyncType.Int
            };
            parameter.parameters.Add(conf);
        }

        protected void ApplyMAMenuInstaller(Runtime.FaceEmoteInstaller installer, GameObject target)
        {
            var gameObject = new GameObject("ExpressionSet");
            gameObject.transform.parent = target.transform;

            gameObject.AddComponent<ModularAvatarMenuInstaller>();
            var menuItem = gameObject.AddComponent<ModularAvatarMenuItem>();
            menuItem.MenuSource = SubmenuSource.Children;
            menuItem.Control = new VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control();
            menuItem.Control.type = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType.SubMenu;

            for (int i = 0; i < installer.Definitions.Count; i++)
            {
                var expressionSetItemObject = new GameObject($"Set {i}");
                expressionSetItemObject.transform.parent = gameObject.transform;
                var setMenuItem = expressionSetItemObject.AddComponent<ModularAvatarMenuItem>();
                setMenuItem.Control = new VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control();

                setMenuItem.Control.parameter = new VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.Parameter();
                setMenuItem.Control.parameter.name = ExpressionSetParameterName;
                setMenuItem.Control.value = i;
                setMenuItem.Control.type = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType.Toggle;
            }
        }

        protected AnimatorControllerParameter[] GenerateParameters()
        {
            return new AnimatorControllerParameter[]
            {
                 new AnimatorControllerParameter()
                 {
                     name = AnimatorParameters.GestureLeft,
                     type = AnimatorControllerParameterType.Int,
                 },
                 new AnimatorControllerParameter()
                 {
                     name = AnimatorParameters.GestureRight,
                     type = AnimatorControllerParameterType.Int,
                 },
                 new AnimatorControllerParameter()
                 {
                     name = AnimatorParameters.GestureLeftWeight,
                     type = AnimatorControllerParameterType.Float,
                 },
                 new AnimatorControllerParameter()
                 {
                     name = AnimatorParameters.GestureRightWeight,
                     type = AnimatorControllerParameterType.Float,
                 },
                 new AnimatorControllerParameter()
                 {
                     name = ExpressionSetParameterName,
                     type = AnimatorControllerParameterType.Int,
                 },
            };
        }
    }

    public static class AnimatorParameters
    {
        public const string GestureLeft = "GestureLeft";
        public const string GestureRight = "GestureRight";
        public const string GestureLeftWeight = "GestureLeftWeight";
        public const string GestureRightWeight = "GestureRightWeight";
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
