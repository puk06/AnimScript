using System.Collections.Generic;
using System.Linq;
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
        bool _showCheck;

        AnimatorController _importSource;
        AnimatorController _checkSource;
        string _importResultMessage;
        string _lastImportScriptPath;
        List<Diagnostic> _checkDiagnostics;
        List<string> _checkWarnings;

        internal static void OpenCheck(AnimatorController controller)
        {
            var window = GetWindow<AnimScriptWindow>("AnimScript");
            window.minSize = new Vector2(420, 360);
            window._checkSource = controller;
            window._showCheck = true;
            window.RunCheck();
            window.Show();
            window.Focus();
        }

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
                DrawCheckSection();

                EditorGUILayout.Space();
                DrawCheatSheet();
            }
        }

        void DrawCheckSection()
        {
            _showCheck = EditorGUILayout.Foldout(_showCheck, "Animator Controllerをチェック", true);
            if (!_showCheck) return;

            EditorGUILayout.LabelField(
                "コントローラを animscript に変換した場合と同じ内容で解析し、\n" +
                "遷移先・パラメータ・到達不能ステートなどの問題を検出します。",
                EditorStyles.wordWrappedMiniLabel);

            _checkSource = EditorGUILayout.ObjectField(
                "コントローラ", _checkSource, typeof(AnimatorController), false) as AnimatorController;

            using (new EditorGUI.DisabledScope(_checkSource == null))
            {
                if (GUILayout.Button("チェックを実行", GUILayout.Height(28)))
                    RunCheck();
            }

            if (_checkDiagnostics == null && _checkWarnings == null) return;

            var diagnostics = _checkDiagnostics ?? new List<Diagnostic>();
            var errors = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
            var warnings = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning) + (_checkWarnings?.Count ?? 0);
            var infos = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Info);
            var type = errors > 0 ? MessageType.Error : warnings > 0 ? MessageType.Warning : MessageType.Info;
            EditorGUILayout.HelpBox($"エラー {errors} 件 / 警告 {warnings} 件 / 情報 {infos} 件", type);

            foreach (var diagnostic in diagnostics)
            {
                var prefix = diagnostic.Severity == DiagnosticSeverity.Error ? "エラー" :
                    diagnostic.Severity == DiagnosticSeverity.Warning ? "警告" : "情報";
                EditorGUILayout.LabelField(
                    $"[{prefix}] {diagnostic.Location.Line}行目: {diagnostic.Message}",
                    EditorStyles.wordWrappedMiniLabel);
            }

            if (_checkWarnings != null)
            {
                // 遷移時間の警告は変換時の注意であり、構造チェックの結果には含めない。
                foreach (var warning in _checkWarnings.Where(w => !w.Contains("遷移時間")))
                    EditorGUILayout.LabelField($"[変換警告] {warning}", EditorStyles.wordWrappedMiniLabel);
            }
        }

        void RunCheck()
        {
            if (_checkSource == null) return;

            _checkDiagnostics = new List<Diagnostic>();
            var conversionWarnings = new List<string>();
            var scriptText = ControllerImporter.Convert(_checkSource, conversionWarnings);
            // 名前の変換は保存時の出力上の注意であり、構造チェックの結果には含めない。
            _checkWarnings = conversionWarnings
                .Where(warning => !warning.StartsWith("名前「"))
                .ToList();
            var diagnostics = new DiagnosticBag();
            AnimatorCompiler.ParseAndValidate(scriptText, diagnostics);
            _checkDiagnostics = diagnostics.ToList();

            foreach (var warning in _checkWarnings)
                Debug.LogWarning($"[AnimScript] {_checkSource.name}: {warning}", _checkSource);
            foreach (var diagnostic in _checkDiagnostics)
            {
                var message = $"[AnimScript] {_checkSource.name} ({diagnostic.Location.Line}行目): {diagnostic.Message}";
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                    Debug.LogError(message, _checkSource);
                else if (diagnostic.Severity == DiagnosticSeverity.Warning)
                    Debug.LogWarning(message, _checkSource);
                else
                    Debug.Log(message, _checkSource);
            }

            var errorCount = _checkDiagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
            var warningCount = _checkDiagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning) + _checkWarnings.Count;
            ShowNotification(new GUIContent(errorCount > 0
                ? $"チェック完了: エラー {errorCount} 件"
                : warningCount > 0
                    ? $"チェック完了: 警告 {warningCount} 件"
                    : "チェック完了: 問題ありません"));
            Repaint();
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
