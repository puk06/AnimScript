using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// ステートに書かれた文字列から AnimationClip を探してくる。
    ///
    /// 解決の優先順位:
    /// 1. スクリプトからの相対パス（拡張子 .anim は省略可）
    /// 2. "Assets/..." で始まるプロジェクト内パス
    /// 3. 「/」を含まない場合は、クリップ名でプロジェクト全体を検索
    ///    （.fbx などのサブアセットも対象）
    /// </summary>
    internal static class ClipResolver
    {
        public static AnimationClip Resolve(string clipText, string scriptAssetPath,
            SourceLocation location, DiagnosticBag diagnostics)
        {
            var text = clipText.Replace('\\', '/');
            var scriptDirectory = AnimScriptPaths.GetDirectoryPath(scriptAssetPath);

            // 1) 2) パスとして解決を試みる
            foreach (var candidate in GetPathCandidates(text, scriptDirectory))
            {
                var clip = TryLoad(candidate);
                if (clip != null) return clip;
            }

            // 3) 名前検索（パスっぽくない書き方のときだけ）
            if (!text.Contains("/"))
            {
                var clip = ResolveByName(text, diagnostics, location, out var found);
                if (found) return clip;
            }

            diagnostics.Error(location,
                $"アニメーションクリップ「{clipText}」が見つかりません。パスを確認してください（スクリプトからの相対パス、または Assets/ からのパスが書けます）");
            return null;
        }

        /// <summary>試すパスの候補を順番に返す。</summary>
        static IEnumerable<string> GetPathCandidates(string text, string scriptDirectory)
        {
            var hasExtension = Path.GetFileName(text).Contains(".");

            if (text.StartsWith("Assets/"))
            {
                yield return text;
                if (!hasExtension) yield return text + ".anim";
            }
            else
            {
                var relative = scriptDirectory + "/" + text;
                yield return relative;
                if (!hasExtension) yield return relative + ".anim";
            }
        }

        /// <summary>1パス試す。.anim 以外（fbx 等）ならサブアセットも探す。</summary>
        static AnimationClip TryLoad(string assetPath)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (clip != null) return clip;

            // ファイル自体はあるのに取れない場合、fbx 等のサブアセットを探す
            if (File.Exists(Path.GetFullPath(assetPath)))
            {
                return AssetDatabase.LoadAllAssetsAtPath(assetPath)
                    .OfType<AnimationClip>()
                    .FirstOrDefault();
            }

            return null;
        }

        /// <summary>クリップ名でプロジェクト全体を検索する。</summary>
        static AnimationClip ResolveByName(string text, DiagnosticBag diagnostics, SourceLocation location, out bool found)
        {
            var name = Path.GetFileNameWithoutExtension(text);

            var matches = new List<(AnimationClip clip, string path)>();
            foreach (var guid in AssetDatabase.FindAssets($"t:AnimationClip {name}"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
                {
                    if (clip.name == name)
                        matches.Add((clip, path));
                }
            }

            if (matches.Count == 1)
            {
                found = true;
                return matches[0].clip;
            }

            if (matches.Count > 1)
            {
                var candidates = string.Join("\n", matches.Select(m => $"・{m.path} ({m.clip.name})"));
                diagnostics.Error(location,
                    $"クリップ名「{name}」が複数見つかりました。パスで指定してください:\n{candidates}");
                found = true; // 二重にエラーを出さない
                return null;
            }

            found = false;
            return null;
        }
    }
}
