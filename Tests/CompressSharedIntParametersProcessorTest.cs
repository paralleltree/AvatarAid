using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using nadena.dev.ndmf;

namespace Paltee.AvatarAid.Tests
{
    public class CompressSharedIntParametersProcessorTest
    {
        [Test]
        public void TestProcess_RemovesMarkerComponent()
        {
            var gameObject = new GameObject("Test Avatar");
            gameObject.AddComponent<Runtime.CompressSharedIntParameters>();

            var context = new BuildContext(gameObject, "Assets/_TestingResources");

            var errors = ErrorReport.CaptureErrors(() => new CompressSharedIntParametersProcessor().Process(context));

            Assert.Zero(errors.Count);
            Assert.IsNull(gameObject.GetComponent<Runtime.CompressSharedIntParameters>());
        }

        [Test]
        public void TestProcess_SkipsWhenNoSyncedIntParams()
        {
            var gameObject = new GameObject("Test Avatar");
            var descriptor = gameObject.AddComponent<VRCAvatarDescriptor>();

            var expressionParams = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            expressionParams.parameters = new[]
            {
                new VRCExpressionParameters.Parameter
                {
                    name = "TestFloat",
                    valueType = VRCExpressionParameters.ValueType.Float,
                    defaultValue = 0,
                    networkSynced = true,
                }
            };
            descriptor.expressionParameters = expressionParams;
            descriptor.customExpressions = true;

            var fxController = new AnimatorController();
            SetupFxLayer(descriptor, fxController);

            gameObject.AddComponent<Runtime.CompressSharedIntParameters>();

            var context = new BuildContext(gameObject, "Assets/_TestingResources");

            var errors = ErrorReport.CaptureErrors(() => new CompressSharedIntParametersProcessor().Process(context));

            Assert.Zero(errors.Count);
            Assert.IsNull(gameObject.GetComponent<Runtime.CompressSharedIntParameters>());
            Assert.IsFalse(fxController.layers.Any(l => l.name.Contains("Encoder") || l.name.Contains("Decoder")));
        }

        [Test]
        [TestCase(1, 1)]
        [TestCase(2, 1)]
        [TestCase(3, 2)]
        [TestCase(4, 2)]
        [TestCase(5, 3)]
        [TestCase(8, 3)]
        [TestCase(9, 4)]
        [TestCase(128, 7)]
        [TestCase(256, 8)]
        public void TestCalculateBitCount_ReturnsCorrectCount(int distinctCount, int expectedBits)
        {
            var processor = new CompressSharedIntParametersProcessor();
            Assert.AreEqual(expectedBits, processor.CalculateBitCount(distinctCount));
        }

        [Test]
        public void TestProcess_CompressesThreeValueIntParameter()
        {
            // Int param "TestInt" default=0, transitions use values 1 and 2 → distinct values {0,1,2} → 2 bits
            var (gameObject, _, fxController, expressionParams) = SetupAvatar("TestInt", 0, new[] { 1, 2 });
            gameObject.AddComponent<Runtime.CompressSharedIntParameters>();

            var context = new BuildContext(gameObject, "Assets/_TestingResources");

            var errors = ErrorReport.CaptureErrors(() => new CompressSharedIntParametersProcessor().Process(context));

            Assert.Zero(errors.Count);
            Assert.IsNull(gameObject.GetComponent<Runtime.CompressSharedIntParameters>());

            // Encoder and Decoder layers should be added
            var encoderLayer = fxController.layers.FirstOrDefault(l => l.name == "TestInt_Encoder");
            var decoderLayer = fxController.layers.FirstOrDefault(l => l.name == "TestInt_Decoder");
            Assert.IsNotNull(encoderLayer.stateMachine, "Encoder layer not found");
            Assert.IsNotNull(decoderLayer.stateMachine, "Decoder layer not found");

            // 3 any-state transitions (one per distinct value)
            Assert.AreEqual(3, encoderLayer.stateMachine.anyStateTransitions.Length);
            Assert.AreEqual(3, decoderLayer.stateMachine.anyStateTransitions.Length);

            // Int param → not synced
            var intParam = expressionParams.parameters.First(p => p.name == "TestInt");
            Assert.IsFalse(intParam.networkSynced);

            // Bool params added and synced
            var bit0Param = expressionParams.parameters.FirstOrDefault(p => p.name == "TestInt_bit0");
            var bit1Param = expressionParams.parameters.FirstOrDefault(p => p.name == "TestInt_bit1");
            Assert.IsNotNull(bit0Param, "bit0 param not found");
            Assert.IsNotNull(bit1Param, "bit1 param not found");
            Assert.IsTrue(bit0Param.networkSynced);
            Assert.IsTrue(bit1Param.networkSynced);
            Assert.AreEqual(VRCExpressionParameters.ValueType.Bool, bit0Param.valueType);
            Assert.AreEqual(VRCExpressionParameters.ValueType.Bool, bit1Param.valueType);

            // No bit2 should exist (only 2 bits needed)
            Assert.IsNull(expressionParams.parameters.FirstOrDefault(p => p.name == "TestInt_bit2"));
        }

        [Test]
        public void TestProcess_GeneratesCorrectEncoderLayerBitValues()
        {
            // values {0,1,2}: index 0→bits 00, index 1→bits 01, index 2→bits 10
            var (gameObject, _, fxController, _) = SetupAvatar("TestInt", 0, new[] { 1, 2 });
            gameObject.AddComponent<Runtime.CompressSharedIntParameters>();

            var context = new BuildContext(gameObject, "Assets/_TestingResources");
            ErrorReport.CaptureErrors(() => new CompressSharedIntParametersProcessor().Process(context));

            var encoderLayer = fxController.layers.First(l => l.name == "TestInt_Encoder");

            // value=0, index=0 → bit0=0, bit1=0
            var state0 = encoderLayer.stateMachine.states.First(s => s.state.name == "Encode_0").state;
            var driver0 = state0.behaviours.OfType<VRCAvatarParameterDriver>().Single();
            Assert.AreEqual(0f, driver0.parameters.Find(p => p.name == "TestInt_bit0").value);
            Assert.AreEqual(0f, driver0.parameters.Find(p => p.name == "TestInt_bit1").value);

            // value=1, index=1 → bit0=1, bit1=0
            var state1 = encoderLayer.stateMachine.states.First(s => s.state.name == "Encode_1").state;
            var driver1 = state1.behaviours.OfType<VRCAvatarParameterDriver>().Single();
            Assert.AreEqual(1f, driver1.parameters.Find(p => p.name == "TestInt_bit0").value);
            Assert.AreEqual(0f, driver1.parameters.Find(p => p.name == "TestInt_bit1").value);

            // value=2, index=2 → bit0=0, bit1=1
            var state2 = encoderLayer.stateMachine.states.First(s => s.state.name == "Encode_2").state;
            var driver2 = state2.behaviours.OfType<VRCAvatarParameterDriver>().Single();
            Assert.AreEqual(0f, driver2.parameters.Find(p => p.name == "TestInt_bit0").value);
            Assert.AreEqual(1f, driver2.parameters.Find(p => p.name == "TestInt_bit1").value);
        }

        [Test]
        public void TestProcess_GeneratesCorrectEncoderLayerTransitionConditions()
        {
            var (gameObject, _, fxController, _) = SetupAvatar("TestInt", 0, new[] { 1, 2 });
            gameObject.AddComponent<Runtime.CompressSharedIntParameters>();

            var context = new BuildContext(gameObject, "Assets/_TestingResources");
            ErrorReport.CaptureErrors(() => new CompressSharedIntParametersProcessor().Process(context));

            var encoderLayer = fxController.layers.First(l => l.name == "TestInt_Encoder");

            // Transition to Encode_2: IsLocal=true AND TestInt==2
            var transitionToEncode2 = encoderLayer.stateMachine.anyStateTransitions
                .First(t => t.destinationState.name == "Encode_2");
            Assert.IsFalse(transitionToEncode2.canTransitionToSelf);
            Assert.AreEqual(0f, transitionToEncode2.duration);
            Assert.IsFalse(transitionToEncode2.hasExitTime);
            Assert.IsTrue(transitionToEncode2.conditions.Any(c =>
                c.parameter == "IsLocal" && c.mode == AnimatorConditionMode.If));
            Assert.IsTrue(transitionToEncode2.conditions.Any(c =>
                c.parameter == "TestInt" && c.mode == AnimatorConditionMode.Equals && (int)c.threshold == 2));
        }

        [Test]
        public void TestProcess_GeneratesCorrectDecoderLayerDriverAndConditions()
        {
            // value=2, index=2 → decoder sets TestInt=2 when IsLocal=false AND bit0=false AND bit1=true
            var (gameObject, _, fxController, _) = SetupAvatar("TestInt", 0, new[] { 1, 2 });
            gameObject.AddComponent<Runtime.CompressSharedIntParameters>();

            var context = new BuildContext(gameObject, "Assets/_TestingResources");
            ErrorReport.CaptureErrors(() => new CompressSharedIntParametersProcessor().Process(context));

            var decoderLayer = fxController.layers.First(l => l.name == "TestInt_Decoder");

            // ParameterDriver for Decode_2 must set TestInt=2
            var decodeState2 = decoderLayer.stateMachine.states.First(s => s.state.name == "Decode_2").state;
            var decoderDriver = decodeState2.behaviours.OfType<VRCAvatarParameterDriver>().Single();
            Assert.AreEqual(1, decoderDriver.parameters.Count);
            Assert.AreEqual("TestInt", decoderDriver.parameters[0].name);
            Assert.AreEqual(2f, decoderDriver.parameters[0].value);

            // Transition conditions: IsLocal=false, bit0=false (index 2=10), bit1=true
            var transitionToDecode2 = decoderLayer.stateMachine.anyStateTransitions
                .First(t => t.destinationState.name == "Decode_2");
            Assert.IsFalse(transitionToDecode2.canTransitionToSelf);
            Assert.AreEqual(0f, transitionToDecode2.duration);
            Assert.IsFalse(transitionToDecode2.hasExitTime);
            Assert.IsTrue(transitionToDecode2.conditions.Any(c =>
                c.parameter == "IsLocal" && c.mode == AnimatorConditionMode.IfNot));
            Assert.IsTrue(transitionToDecode2.conditions.Any(c =>
                c.parameter == "TestInt_bit0" && c.mode == AnimatorConditionMode.IfNot));
            Assert.IsTrue(transitionToDecode2.conditions.Any(c =>
                c.parameter == "TestInt_bit1" && c.mode == AnimatorConditionMode.If));
        }

        [Test]
        public void TestProcess_DetectsPossibleValuesFromParameterDriver()
        {
            var fxController = CreateFxControllerWithParameterDriver("TestInt", drivenValue: 3);
            var expressionParams = CreateExpressionParameters("TestInt", defaultValue: 0);
            var processor = new CompressSharedIntParametersProcessor();

            var values = processor.DetectPossibleValues(fxController, "TestInt", 0);

            // Should detect both 0 (default) and 3 (from ParameterDriver)
            Assert.IsTrue(values.Contains(0));
            Assert.IsTrue(values.Contains(3));
        }

        // --- Helpers ---

        private (GameObject, VRCAvatarDescriptor, AnimatorController, VRCExpressionParameters) SetupAvatar(
            string intParamName,
            int intDefaultValue,
            int[] transitionValues)
        {
            var gameObject = new GameObject("Test Avatar");
            var descriptor = gameObject.AddComponent<VRCAvatarDescriptor>();

            var expressionParams = CreateExpressionParameters(intParamName, intDefaultValue);
            descriptor.expressionParameters = expressionParams;
            descriptor.customExpressions = true;

            var fxController = CreateFxControllerWithTransitions(intParamName, transitionValues);
            SetupFxLayer(descriptor, fxController);

            return (gameObject, descriptor, fxController, expressionParams);
        }

        private VRCExpressionParameters CreateExpressionParameters(string paramName, int defaultValue = 0)
        {
            var expressionParams = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            expressionParams.parameters = new[]
            {
                new VRCExpressionParameters.Parameter
                {
                    name = paramName,
                    valueType = VRCExpressionParameters.ValueType.Int,
                    defaultValue = defaultValue,
                    saved = false,
                    networkSynced = true,
                }
            };
            return expressionParams;
        }

        private AnimatorController CreateFxControllerWithTransitions(string intParamName, int[] transitionValues)
        {
            var controller = new AnimatorController();
            controller.AddParameter(intParamName, AnimatorControllerParameterType.Int);

            var stateMachine = new AnimatorStateMachine { name = "Base Layer" };
            var idleState = new AnimatorState { name = "Idle" };
            stateMachine.AddState(idleState, Vector3.zero);
            stateMachine.defaultState = idleState;

            foreach (var value in transitionValues)
            {
                var state = new AnimatorState { name = $"State_{value}" };
                stateMachine.AddState(state, new Vector3(200, value * 60));

                var transition = idleState.AddTransition(state);
                transition.hasExitTime = false;
                transition.hasFixedDuration = true;
                transition.duration = 0;
                transition.AddCondition(AnimatorConditionMode.Equals, value, intParamName);
            }

            controller.AddLayer(new AnimatorControllerLayer
            {
                name = "Base Layer",
                defaultWeight = 1f,
                stateMachine = stateMachine,
            });

            return controller;
        }

        private AnimatorController CreateFxControllerWithParameterDriver(string intParamName, int drivenValue)
        {
            var controller = new AnimatorController();
            controller.AddParameter(intParamName, AnimatorControllerParameterType.Int);

            var stateMachine = new AnimatorStateMachine { name = "Base Layer" };
            var state = new AnimatorState { name = "StateWithDriver" };
            stateMachine.AddState(state, new Vector3(200, 0));
            stateMachine.defaultState = state;

            var driver = state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            driver.parameters = new List<VRCAvatarParameterDriver.Parameter>
            {
                new VRCAvatarParameterDriver.Parameter
                {
                    type = VRCAvatarParameterDriver.ChangeType.Set,
                    name = intParamName,
                    value = drivenValue,
                }
            };

            controller.AddLayer(new AnimatorControllerLayer
            {
                name = "Base Layer",
                defaultWeight = 1f,
                stateMachine = stateMachine,
            });

            return controller;
        }

        private void SetupFxLayer(VRCAvatarDescriptor descriptor, AnimatorController fxController)
        {
            descriptor.customizeAnimationLayers = true;
            descriptor.baseAnimationLayers = new[]
            {
                new VRCAvatarDescriptor.CustomAnimLayer
                {
                    type = VRCAvatarDescriptor.AnimLayerType.FX,
                    isDefault = false,
                    isEnabled = true,
                    animatorController = fxController,
                }
            };
        }
    }
}
