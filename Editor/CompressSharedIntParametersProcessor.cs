using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using nadena.dev.ndmf;

namespace Paltee.AvatarAid
{
    public class CompressSharedIntParametersProcessor
    {
        private const string IsLocalParamName = "IsLocal";

        public void Process(BuildContext context)
        {
            var marker = context.AvatarRootObject.GetComponent<Runtime.CompressSharedIntParameters>();
            if (marker == null) return;

            Apply(context);

            UnityEngine.Object.DestroyImmediate(marker);
        }

        protected void Apply(BuildContext context)
        {
            var descriptor = context.AvatarRootObject.GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null) return;

            var expressionParameters = descriptor.expressionParameters;
            if (expressionParameters == null) return;

            var fxController = GetFxAnimatorController(descriptor);
            if (fxController == null) return;

            var syncedIntParams = expressionParameters.parameters
                .Where(p => p.networkSynced && p.valueType == VRCExpressionParameters.ValueType.Int)
                .ToList();

            foreach (var expParam in syncedIntParams)
            {
                ProcessParameter(expParam, fxController, expressionParameters);
            }
        }

        protected AnimatorController GetFxAnimatorController(VRCAvatarDescriptor descriptor)
        {
            if (!descriptor.customizeAnimationLayers) return null;

            foreach (var layer in descriptor.baseAnimationLayers)
            {
                if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX && !layer.isDefault)
                {
                    return layer.animatorController as AnimatorController;
                }
            }
            return null;
        }

        protected void ProcessParameter(
            VRCExpressionParameters.Parameter expParam,
            AnimatorController controller,
            VRCExpressionParameters expressionParameters)
        {
            var paramName = expParam.name;
            var defaultValue = (int)expParam.defaultValue;

            var possibleValues = DetectPossibleValues(controller, paramName, defaultValue);
            if (possibleValues.Count == 0) return;

            int bitCount = CalculateBitCount(possibleValues.Count);
            if (bitCount >= 8) return;

            var sortedValues = possibleValues.OrderBy(v => v).ToList();
            var valueToIndex = new Dictionary<int, int>();
            for (int i = 0; i < sortedValues.Count; i++)
            {
                valueToIndex[sortedValues[i]] = i;
            }

            EnsureIsLocalParam(controller);
            for (int n = 0; n < bitCount; n++)
            {
                var bitParamName = $"{paramName}_bit{n}";
                if (controller.parameters.All(p => p.name != bitParamName))
                {
                    controller.AddParameter(bitParamName, AnimatorControllerParameterType.Bool);
                }
            }

            controller.AddLayer(GenerateEncoderLayer(paramName, valueToIndex, bitCount));
            controller.AddLayer(GenerateDecoderLayer(paramName, valueToIndex, bitCount));

            expParam.networkSynced = false;

            var paramsList = expressionParameters.parameters.ToList();
            for (int n = 0; n < bitCount; n++)
            {
                paramsList.Add(new VRCExpressionParameters.Parameter
                {
                    name = $"{paramName}_bit{n}",
                    valueType = VRCExpressionParameters.ValueType.Bool,
                    defaultValue = 0,
                    saved = false,
                    networkSynced = true,
                });
            }
            expressionParameters.parameters = paramsList.ToArray();
        }

        public HashSet<int> DetectPossibleValues(AnimatorController controller, string paramName, int defaultValue)
        {
            var values = new HashSet<int> { defaultValue };

            foreach (var layer in controller.layers)
            {
                CollectValuesFromStateMachine(layer.stateMachine, paramName, values);
            }

            return values;
        }

        private void CollectValuesFromStateMachine(AnimatorStateMachine stateMachine, string paramName, HashSet<int> values)
        {
            if (stateMachine == null) return;

            foreach (var transition in stateMachine.anyStateTransitions)
            {
                CollectValuesFromConditions(transition.conditions, paramName, values);
            }

            foreach (var stateInfo in stateMachine.states)
            {
                var state = stateInfo.state;
                foreach (var transition in state.transitions)
                {
                    CollectValuesFromConditions(transition.conditions, paramName, values);
                }

                foreach (var behaviour in state.behaviours)
                {
                    if (behaviour is VRCAvatarParameterDriver driver)
                    {
                        foreach (var driverParam in driver.parameters)
                        {
                            if (driverParam.name == paramName &&
                                driverParam.type == VRCAvatarParameterDriver.ChangeType.Set)
                            {
                                values.Add((int)driverParam.value);
                            }
                        }
                    }
                }
            }

            foreach (var subSM in stateMachine.stateMachines)
            {
                CollectValuesFromStateMachine(subSM.stateMachine, paramName, values);
            }
        }

        private void CollectValuesFromConditions(AnimatorCondition[] conditions, string paramName, HashSet<int> values)
        {
            foreach (var condition in conditions)
            {
                if (condition.parameter == paramName)
                {
                    values.Add((int)condition.threshold);
                }
            }
        }

        public int CalculateBitCount(int distinctCount)
        {
            if (distinctCount <= 1) return 1;
            int bits = 1;
            long capacity = 2;
            while (capacity < distinctCount)
            {
                bits++;
                capacity <<= 1;
            }
            return bits;
        }

        private void EnsureIsLocalParam(AnimatorController controller)
        {
            if (controller.parameters.All(p => p.name != IsLocalParamName))
            {
                controller.AddParameter(IsLocalParamName, AnimatorControllerParameterType.Bool);
            }
        }

        protected AnimatorControllerLayer GenerateEncoderLayer(
            string paramName,
            Dictionary<int, int> valueToIndex,
            int bitCount)
        {
            var stateMachine = new AnimatorStateMachine();
            var idleState = new AnimatorState { name = "Idle" };
            stateMachine.AddState(idleState, Vector3.zero);
            stateMachine.defaultState = idleState;

            foreach (var kvp in valueToIndex.OrderBy(x => x.Key))
            {
                int value = kvp.Key;
                int index = kvp.Value;

                var state = new AnimatorState { name = $"Encode_{value}" };
                stateMachine.AddState(state, new Vector3(200, index * 80));

                var driver = state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                driver.localOnly = false;
                driver.parameters = new List<VRCAvatarParameterDriver.Parameter>();
                for (int n = 0; n < bitCount; n++)
                {
                    int bitValue = (index >> n) & 1;
                    driver.parameters.Add(new VRCAvatarParameterDriver.Parameter
                    {
                        type = VRCAvatarParameterDriver.ChangeType.Set,
                        name = $"{paramName}_bit{n}",
                        value = bitValue,
                    });
                }

                var transition = stateMachine.AddAnyStateTransition(state);
                transition.canTransitionToSelf = false;
                transition.duration = 0;
                transition.hasExitTime = false;
                transition.hasFixedDuration = true;
                transition.AddCondition(AnimatorConditionMode.If, 1, IsLocalParamName);
                transition.AddCondition(AnimatorConditionMode.Equals, value, paramName);
            }

            return new AnimatorControllerLayer
            {
                name = $"{paramName}_Encoder",
                defaultWeight = 1f,
                stateMachine = stateMachine,
            };
        }

        protected AnimatorControllerLayer GenerateDecoderLayer(
            string paramName,
            Dictionary<int, int> valueToIndex,
            int bitCount)
        {
            var stateMachine = new AnimatorStateMachine();
            var idleState = new AnimatorState { name = "Idle" };
            stateMachine.AddState(idleState, Vector3.zero);
            stateMachine.defaultState = idleState;

            foreach (var kvp in valueToIndex.OrderBy(x => x.Key))
            {
                int value = kvp.Key;
                int index = kvp.Value;

                var state = new AnimatorState { name = $"Decode_{value}" };
                stateMachine.AddState(state, new Vector3(200, index * 80));

                var driver = state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                driver.localOnly = false;
                driver.parameters = new List<VRCAvatarParameterDriver.Parameter>
                {
                    new VRCAvatarParameterDriver.Parameter
                    {
                        type = VRCAvatarParameterDriver.ChangeType.Set,
                        name = paramName,
                        value = value,
                    }
                };

                var transition = stateMachine.AddAnyStateTransition(state);
                transition.canTransitionToSelf = false;
                transition.duration = 0;
                transition.hasExitTime = false;
                transition.hasFixedDuration = true;
                transition.AddCondition(AnimatorConditionMode.IfNot, 0, IsLocalParamName);
                for (int n = 0; n < bitCount; n++)
                {
                    int bitValue = (index >> n) & 1;
                    transition.AddCondition(
                        bitValue == 1 ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                        bitValue,
                        $"{paramName}_bit{n}");
                }
            }

            return new AnimatorControllerLayer
            {
                name = $"{paramName}_Decoder",
                defaultWeight = 1f,
                stateMachine = stateMachine,
            };
        }
    }
}
