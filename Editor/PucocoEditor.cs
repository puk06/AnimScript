using System;
using UnityEditor;
using UnityEngine;

namespace net.puk06.AnimScript
{
    [CustomEditor(typeof(Pucoco))]
    [CanEditMultipleObjects]
    internal sealed class PucocoEditor : UnityEditor.Editor
    {
        SerializedProperty _scriptFile;
        SerializedProperty _layerType;
        SerializedProperty _pathMode;
        SerializedProperty _relativePathRoot;
        SerializedProperty _layerPriority;
        SerializedProperty _mergeAnimatorMode;
        SerializedProperty _matchAvatarWriteDefaults;

        void OnEnable()
        {
            _scriptFile = serializedObject.FindProperty(nameof(Pucoco.ScriptFile));
            _layerType = serializedObject.FindProperty(nameof(Pucoco.LayerType));
            _pathMode = serializedObject.FindProperty(nameof(Pucoco.pathMode));
            _relativePathRoot = serializedObject.FindProperty(nameof(Pucoco.relativePathRoot));
            _layerPriority = serializedObject.FindProperty(nameof(Pucoco.LayerPriority));
            _mergeAnimatorMode = serializedObject.FindProperty(nameof(Pucoco.MergeAnimatorMode));
            _matchAvatarWriteDefaults = serializedObject.FindProperty(nameof(Pucoco.MatchAvatarWriteDefaults));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("AnimScript Builder", EditorStyles.boldLabel);

            DrawScriptFile();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("ビルド設定 (MA Merge Animator)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_layerType, new GUIContent("レイヤー種別"));
            EditorGUILayout.PropertyField(_pathMode, new GUIContent("パスモード"));
            EditorGUILayout.PropertyField(_relativePathRoot, new GUIContent("相対パスのルート"));
            EditorGUILayout.PropertyField(_layerPriority, new GUIContent("レイヤーの優先度"));
            EditorGUILayout.PropertyField(_mergeAnimatorMode, new GUIContent("統合モード"));
            EditorGUILayout.PropertyField(_matchAvatarWriteDefaults,
                new GUIContent("アバターの Write Defaults に合わせる"));

            serializedObject.ApplyModifiedProperties();
        }

        void DrawScriptFile()
        {
            EditorGUI.BeginChangeCheck();
            var script = EditorGUILayout.ObjectField(
                new GUIContent("スクリプト"),
                _scriptFile.objectReferenceValue,
                typeof(UnityEngine.Object),
                false);
            if (EditorGUI.EndChangeCheck())
                _scriptFile.objectReferenceValue = script;

            var path = _scriptFile.objectReferenceValue == null
                ? null
                : AssetDatabase.GetAssetPath(_scriptFile.objectReferenceValue);

            if (_scriptFile.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("ビルドする .animscript ファイルを指定してください。", MessageType.Warning);
            }
            else if (string.IsNullOrEmpty(path))
            {
                EditorGUILayout.HelpBox("指定されたスクリプトがAssets内にありません。", MessageType.Error);
            }
            else if (!path.EndsWith(".animscript", StringComparison.OrdinalIgnoreCase))
            {
                EditorGUILayout.HelpBox(
                    $"ScriptFile は .animscript ファイルである必要があります。\n現在: {path}",
                    MessageType.Error);
            }
        }
    }
}
