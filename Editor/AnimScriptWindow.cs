using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// AnimScript の補助ウィンドウ。
    /// NDMF ビルド時に自動的にコントローラが生成されるため、
    /// このウィンドウではコントローラ → animscript の逆変換と構文チートシートを提供する。
    /// メニュー「Tools > ぷこのつーる > AnimScript」から開く。
    /// </summary>
    internal sealed class AnimScriptWindow : EditorWindow
    {
        [MenuItem("Tools/ぷこのつーる/AnimScript")]
        static void Open()
        {
            var window = GetWindow<AnimScriptWindow>("AnimScript");
            window.minSize = new Vector2(380, 300);
            window.Show();
        }

        Vector2 _scroll;
        bool _showCheatSheet;
        bool _showImport;

        AnimatorController _importSource;
        string _importResultMessage;
        string _lastImportScriptPath;

        void OnGUI()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;

                EditorGUILayout.LabelField("AnimScript", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "NDMF ビルド時に .animscript から AnimatorController が自動生成されます。\n" +
                    "このウィンドウではコントローラからの逆変換（インポート）と構文リファレンスが使えます。",
                    EditorStyles.wordWrappedMiniLabel);

                EditorGUILayout.Space();
                DrawImportSection();

                EditorGUILayout.Space();
                DrawCheatSheet();
            }
        }

        // ================================================================
        // コントローラから animscript へ逆変換（インポート）
        // ================================================================

        void DrawImportSection()
        {
            _showImport = EditorGUILayout.Foldout(_showImport, "コントローラからインポート", true);
            if (!_showImport) return;

            EditorGUILayout.LabelField(
                "既存の AnimatorController を .animscript に変換します。\n" +
                "特殊な構造（サブステートマシン等）はコメントや警告で明示されます。",
                EditorStyles.wordWrappedMiniLabel);

            _importSource = EditorGUILayout.ObjectField(
                "コントローラ", _importSource, typeof(AnimatorController), false) as AnimatorController;

            using (new EditorGUI.DisabledScope(_importSource == null))
            {
                if (GUILayout.Button("animscript に変換", GUILayout.Height(32)))
                    ImportController();
            }

            if (!string.IsNullOrEmpty(_importResultMessage))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(_importResultMessage, MessageType.Info);

                if (GUILayout.Button("生成されたスクリプトを選択"))
                {
                    var script = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(_lastImportScriptPath);
                    if (script != null)
                    {
                        Selection.activeObject = script;
                        EditorGUIUtility.PingObject(script);
                    }
                }
            }
        }

        void ImportController()
        {
            var warnings = new List<string>();
            _lastImportScriptPath = ControllerImporter.ConvertAndSave(_importSource, warnings);

            if (warnings.Count > 0)
            {
                Debug.LogWarning($"[AnimScript] 「{_importSource.name}」の変換で {warnings.Count} 件の警告がありました:\n" +
                                 string.Join("\n", warnings));
            }

            _importResultMessage = $"{_lastImportScriptPath} に変換しました。\n" +
                                   $"警告 {warnings.Count} 件（詳細は Console を確認）";

            ShowNotification(new GUIContent("変換しました"));
        }

        // ================================================================
        // 構文チートシート
        // ================================================================

        void DrawCheatSheet()
        {
            _showCheatSheet = EditorGUILayout.Foldout(_showCheatSheet, "構文チートシート", true);
            if (!_showCheatSheet) return;

            var style = EditorStyles.textArea;
            var height = style.CalcHeight(new GUIContent(SyntaxCheatSheet.Text), position.width - 24);
            EditorGUILayout.SelectableLabel(SyntaxCheatSheet.Text, style, GUILayout.Height(height));
        }
    }
}
