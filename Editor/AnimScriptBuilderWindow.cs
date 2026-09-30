using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// animscript をドロップして AnimatorController を生成するウィンドウ。
    /// メニュー「Tools > ぷこのつーる > AnimScript Builder」から開く。
    /// </summary>
    internal sealed class AnimScriptBuilderWindow : EditorWindow
    {
        [MenuItem("Tools/ぷこのつーる/AnimScript Builder")]
        static void Open()
        {
            var window = GetWindow<AnimScriptBuilderWindow>("AnimScript Builder");
            window.minSize = new Vector2(380, 400);
            window.Show();
        }

        UnityEngine.Object _scriptFile;
        BuildResult _lastResult;
        Vector2 _scroll;
        bool _showCheatSheet;
        bool _showImport;

        AnimatorController _importSource;
        string _importResultMessage;

        void OnGUI()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;

                EditorGUILayout.LabelField("AnimScript Builder", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "animscript（テキスト）から AnimatorController を生成します。\n" +
                    "出力先: スクリプトと同じフォルダの build/ 内",
                    EditorStyles.wordWrappedMiniLabel);

                EditorGUILayout.Space();
                DrawFileField();

                EditorGUILayout.Space();
                DrawBuildButton();

                EditorGUILayout.Space();
                DrawResult();

                EditorGUILayout.Space();
                DrawImportSection();

                EditorGUILayout.Space();
                DrawCheatSheet();
            }
        }

        // ================================================================
        // ファイル指定（ObjectField + ドラッグ＆ドロップ）
        // ================================================================

        void DrawFileField()
        {
            var newFile = EditorGUILayout.ObjectField("スクリプト", _scriptFile, typeof(UnityEngine.Object), false);
            if (newFile != _scriptFile)
            {
                _scriptFile = newFile;
                _lastResult = null;
            }

            var label = _scriptFile == null
                ? "ここに .animscript をドラッグ＆ドロップ"
                : AssetDatabase.GetAssetPath(_scriptFile);

            var rect = GUILayoutUtility.GetRect(0, 36, GUILayout.ExpandWidth(true));
            GUI.Box(rect, label);
            HandleDragAndDrop(rect);
        }

        void HandleDragAndDrop(Rect rect)
        {
            var evt = Event.current;
            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform) return;
            if (!rect.Contains(evt.mousePosition)) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (var dropped in DragAndDrop.objectReferences)
                {
                    var path = AssetDatabase.GetAssetPath(dropped);
                    if (string.IsNullOrEmpty(path)) continue;
                    _scriptFile = dropped;
                    _lastResult = null;
                    break;
                }
            }

            evt.Use();
        }

        // ================================================================
        // ビルド
        // ================================================================

        void DrawBuildButton()
        {
            using (new EditorGUI.DisabledScope(_scriptFile == null))
            {
                if (GUILayout.Button("ビルド", GUILayout.Height(32)))
                    Build();
            }
        }

        void Build()
        {
            var assetPath = AssetDatabase.GetAssetPath(_scriptFile);

            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/"))
            {
                ShowNotification(new GUIContent("Assets フォルダ内のスクリプトを指定してください"));
                return;
            }

            string source;
            try
            {
                source = File.ReadAllText(assetPath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[AnimScriptBuilder] 読み込みに失敗しました: {exception.Message}", _scriptFile);
                return;
            }

            _lastResult = AnimatorCompiler.Build(source, assetPath);

            // Unity コンソールにも流す（行番号付き・ダブルクリックでファイルが開ける）
            foreach (var diagnostic in _lastResult.Diagnostics)
            {
                var message = $"[AnimScriptBuilder] {assetPath}({diagnostic.Location.Line}): {diagnostic.Message}";
                switch (diagnostic.Severity)
                {
                    case DiagnosticSeverity.Error:
                        Debug.LogError(message, _scriptFile);
                        break;
                    case DiagnosticSeverity.Warning:
                        Debug.LogWarning(message, _scriptFile);
                        break;
                    default:
                        Debug.Log(message, _scriptFile);
                        break;
                }
            }

            ShowNotification(_lastResult.Success
                ? new GUIContent("ビルド成功！")
                : new GUIContent("ビルド失敗…詳細はウィンドウ下部へ"));
        }

        // ================================================================
        // 結果表示
        // ================================================================

        void DrawResult()
        {
            if (_lastResult == null) return;

            if (_lastResult.Success)
            {
                EditorGUILayout.HelpBox(
                    $"生成しました:\n{_lastResult.OutputPath}\n\n" +
                    $"レイヤー {_lastResult.LayerCount} / ステート {_lastResult.StateCount} / 遷移 {_lastResult.TransitionCount}",
                    MessageType.Info);

                if (GUILayout.Button("生成されたコントローラを選択"))
                {
                    var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(_lastResult.OutputPath);
                    if (controller != null)
                    {
                        Selection.activeObject = controller;
                        EditorGUIUtility.PingObject(controller);
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("ビルドに失敗しました。以下のエラーを修正してください。", MessageType.Error);
            }

            foreach (var diagnostic in _lastResult.Diagnostics)
            {
                var type = MessageType.Info;
                if (diagnostic.Severity == DiagnosticSeverity.Warning) type = MessageType.Warning;
                if (diagnostic.Severity == DiagnosticSeverity.Error) type = MessageType.Error;

                var where = diagnostic.Location.Line > 0 ? $"{diagnostic.Location.Line}行目: " : "";
                EditorGUILayout.HelpBox(where + diagnostic.Message, type);
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
                "特殊な構造（BlendTree 等）はコメントや警告で明示されます。",
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

        string _lastImportScriptPath;

        void ImportController()
        {
            var warnings = new System.Collections.Generic.List<string>();
            _lastImportScriptPath = ControllerImporter.ConvertAndSave(_importSource, warnings);

            if (warnings.Count > 0)
            {
                Debug.LogWarning($"[AnimScriptBuilder] 「{_importSource.name}」の変換で {warnings.Count} 件の警告がありました:\n" +
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
