using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// animscript のビルド統括。
    /// 解析 → 意味チェック → クリップ解決 → AnimatorController 生成、の流れを仕切る。
    ///
    /// 出力先は「スクリプトと同じフォルダの build/スクリプト名.controller」。
    /// 既に生成物がある場合は中身を消して再利用する
    /// （GUID が変わらないので、アバターの Playable Layer に設定済みでも参照が切れない）。
    /// </summary>
    internal static class AnimatorCompiler
    {
        public static BuildResult Build(string source, string scriptAssetPath)
        {
            var diagnostics = new DiagnosticBag();

            // --- 1. 字句解析 & 構文解析 ---
            var ast = Parser.Parse(source, diagnostics);

            // --- 2. ループ展開（for / while / var / 代入 を実行し、平坦なASTにする） ---
            if (!diagnostics.HasErrors)
                AstExpander.Expand(ast, diagnostics);

            // --- 3. 意味チェック ---
            if (!diagnostics.HasErrors)
                ScriptValidator.Validate(ast, diagnostics);

            // --- 4. クリップ解決 ---
            var clipMap = new Dictionary<StateAst, AnimationClip>();
            if (!diagnostics.HasErrors)
            {
                foreach (var layer in ast.Layers)
                foreach (var state in layer.States)
                {
                    if (!state.HasClip || state.ClipPath == null) continue;

                    var clip = ClipResolver.Resolve(state.ClipPath, scriptAssetPath, state.Location, diagnostics);
                    if (clip != null)
                        clipMap[state] = clip;
                }
            }

            if (diagnostics.HasErrors)
                return BuildResult.Failure(diagnostics);

            // --- 5. AnimatorController 生成 ---
            var outputPath = AnimScriptPaths.GetOutputAssetPath(scriptAssetPath);
            try
            {
                GenerateController(ast, outputPath, clipMap, diagnostics);
            }
            catch (Exception exception)
            {
                diagnostics.Error(new SourceLocation(1, 1), $"生成中に予期しないエラーが発生しました: {exception.Message}");
                return BuildResult.Failure(diagnostics);
            }

            if (diagnostics.HasErrors)
                return BuildResult.Failure(diagnostics);

            return BuildResult.Succeeded(outputPath, diagnostics, ast);
        }

        // ================================================================
        // 生成
        // ================================================================

        static void GenerateController(AnimScriptAst ast, string outputPath,
            IReadOnlyDictionary<StateAst, AnimationClip> clipMap, DiagnosticBag diagnostics)
        {
            // build フォルダを確保（outputPath の1つ上の階層）
            AnimScriptPaths.EnsureFolderExists(AnimScriptPaths.GetDirectoryPath(outputPath));

            var controller = PrepareController(outputPath);

            // --- パラメータ ---
            foreach (var parameterAst in ast.Parameters)
                controller.AddParameter(ParameterBuilder.Build(parameterAst));

            // --- レイヤー（default 指定があれば先頭に） ---
            foreach (var layerAst in OrderLayers(ast.Layers))
            {
                var stateMachine = new AnimatorStateMachine
                {
                    name = layerAst.Name,
                    hideFlags = HideFlags.HideInHierarchy,
                };

                controller.AddLayer(new AnimatorControllerLayer
                {
                    name = layerAst.Name,
                    defaultWeight = (float)(layerAst.Weight?.Const() ?? 1.0),
                    stateMachine = stateMachine,
                });

                LayerBuilder.Build(controller, controller.layers.Length - 1, layerAst, clipMap, diagnostics);
            }

            // --- クリップの loop 設定（アセット自体に書き込むので最後にまとめて） ---
            ApplyLoopSettings(ast, clipMap, diagnostics);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 出力先のコントローラを用意する。
        /// 既存があれば中身を全消去して再利用（GUID 維持）、無ければ新規作成。
        /// </summary>
        static AnimatorController PrepareController(string outputPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(outputPath);
            if (existing == null)
                return AnimatorController.CreateAnimatorControllerAtPath(outputPath);

            // レイヤーとパラメータを全削除
            for (var i = existing.layers.Length - 1; i >= 0; i--)
                existing.RemoveLayer(i);
            foreach (var parameter in existing.parameters)
                existing.RemoveParameter(parameter);

            // 消え残ったサブアセット（ステートやSMBの残骸）があれば掃除
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(outputPath))
            {
                if (asset != existing)
                    UnityEngine.Object.DestroyImmediate(asset, true);
            }
            AssetDatabase.SaveAssets();

            return existing;
        }

        /// <summary>default 指定のレイヤーを先頭に並べ替える。無指定なら定義順。</summary>
        static List<LayerAst> OrderLayers(List<LayerAst> layers)
        {
            var defaultLayer = layers.FirstOrDefault(l => l.IsDefault);
            if (defaultLayer == null) return layers;

            var ordered = new List<LayerAst> { defaultLayer };
            ordered.AddRange(layers.Where(l => l != defaultLayer));
            return ordered;
        }

        /// <summary>loop on/off 指定をクリップに反映する。同じクリップへの競合は警告。</summary>
        static void ApplyLoopSettings(AnimScriptAst ast,
            IReadOnlyDictionary<StateAst, AnimationClip> clipMap, DiagnosticBag diagnostics)
        {
            var loopByClip = new Dictionary<AnimationClip, bool>();

            foreach (var layer in ast.Layers)
            foreach (var state in layer.States)
            {
                // ステート指定 → レイヤー指定 の順にフォールバック
                var loop = state.Loop ?? layer.Loop;
                if (!loop.HasValue) continue;
                if (!clipMap.TryGetValue(state, out var clip)) continue;

                if (loopByClip.TryGetValue(clip, out var existing) && existing != loop.Value)
                {
                    diagnostics.Warning(state.Location,
                        $"クリップ「{clip.name}」に loop on / off の両方が指定されています。後の指定が使われます");
                }
                loopByClip[clip] = loop.Value;
            }

            foreach (var pair in loopByClip)
            {
                if (ClipSettingsUtility.ApplyLoop(pair.Key, pair.Value))
                {
                    diagnostics.Info(new SourceLocation(1, 1),
                        $"クリップ「{pair.Key.name}」の Loop Time を {(pair.Value ? "on" : "off")} に変更しました（クリップのアセット自体が変更されます）");
                }
            }
        }
    }
}
