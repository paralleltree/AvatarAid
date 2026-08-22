using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Paltee.AvatarAid.Runtime;

namespace Paltee.AvatarAid.Editor
{
    [CustomEditor(typeof(FaceEmoteInstaller))]
    public class FaceEmoteInstallerEditor : UnityEditor.Editor
    {
        private SerializedProperty _transitionSeconds;
        private SerializedProperty _idleMotion;
        private SerializedProperty _writeDefaultsValues;
        private SerializedProperty _isExpressionSetIndexSaved;
        private SerializedProperty _primaryHandSide;
        private SerializedProperty _defaultDefinitionIndex;
        private SerializedProperty _definitions;

        private void OnEnable()
        {
            _transitionSeconds = serializedObject.FindProperty(nameof(FaceEmoteInstaller.TransitionSeconds));
            _idleMotion = serializedObject.FindProperty(nameof(FaceEmoteInstaller.IdleMotion));
            _writeDefaultsValues = serializedObject.FindProperty(nameof(FaceEmoteInstaller.WriteDefaultsValues));
            _isExpressionSetIndexSaved = serializedObject.FindProperty(nameof(FaceEmoteInstaller.IsExpressionSetIndexSaved));
            _primaryHandSide = serializedObject.FindProperty(nameof(FaceEmoteInstaller.PrimaryHandSide));
            _defaultDefinitionIndex = serializedObject.FindProperty(nameof(FaceEmoteInstaller.DefaultDefinitionIndex));
            _definitions = serializedObject.FindProperty(nameof(FaceEmoteInstaller.Definitions));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_transitionSeconds);
            EditorGUILayout.PropertyField(_idleMotion);
            EditorGUILayout.PropertyField(_writeDefaultsValues);
            EditorGUILayout.PropertyField(_isExpressionSetIndexSaved);
            EditorGUILayout.PropertyField(_primaryHandSide);

            EditorGUILayout.Space();
            DrawDefaultDefinitionPopup();

            if (_isExpressionSetIndexSaved.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "Is Expression Set Index Saved が有効な場合、Default Expression Set はパラメータが未保存の初回ロード時のみ適用されます。",
                    MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_definitions, true);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawDefaultDefinitionPopup()
        {
            var options = new List<string> { "(None)" };
            for (int i = 0; i < _definitions.arraySize; i++)
            {
                var nameProp = _definitions.GetArrayElementAtIndex(i).FindPropertyRelative(nameof(ExpressionSetDefinition.Name));
                var label = string.IsNullOrWhiteSpace(nameProp.stringValue) ? $"Set {i}" : nameProp.stringValue;
                options.Add(label);
            }

            var currentSelection = _defaultDefinitionIndex.intValue >= 0 && _defaultDefinitionIndex.intValue < _definitions.arraySize
                ? _defaultDefinitionIndex.intValue + 1
                : 0;

            EditorGUI.BeginChangeCheck();
            var newSelection = EditorGUILayout.Popup(
                new GUIContent("Default Expression Set", "アバターロード時・パラメータ未同期時に選択される Expression Set です。"),
                currentSelection,
                options.ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                _defaultDefinitionIndex.intValue = newSelection - 1;
            }
        }
    }
}
