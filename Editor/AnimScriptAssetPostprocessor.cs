using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// .animscript の再インポート時に、生成物を作らず解析・検証だけを実行する。
    /// </summary>
    internal sealed class AnimScriptAssetPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (var assetPath in importedAssets)
            {
                if (!assetPath.EndsWith(".animscript", StringComparison.OrdinalIgnoreCase))
                    continue;

                Check(assetPath);
            }
        }

        static void Check(string assetPath)
        {
            var diagnostics = new DiagnosticBag();
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var filePath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));

            try
            {
                AnimatorCompiler.ParseAndValidate(File.ReadAllText(filePath), diagnostics);
            }
            catch (Exception exception)
            {
                diagnostics.Error(new SourceLocation(1, 1),
                    $"スクリプトファイルの読み込みに失敗しました: {exception.Message}");
            }

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            var results = diagnostics.ToList();
            foreach (var diagnostic in results)
            {
                var message = $"[AnimScript] {assetPath} ({diagnostic.Location.Line}行目): {diagnostic.Message}";
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                    Debug.LogError(message, asset);
                else if (diagnostic.Severity == DiagnosticSeverity.Warning)
                    Debug.LogWarning(message, asset);
                else
                    Debug.Log(message, asset);
            }

            if (!results.Exists(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                Debug.Log($"[AnimScript] {assetPath}: ビルドチェックに成功しました。", asset);
        }
    }
}
