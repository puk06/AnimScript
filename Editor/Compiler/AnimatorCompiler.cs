using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// animscript のビルド統括。
    /// NDMF プラグインから呼ばれ、渡された <see cref="VirtualAnimatorController"/> を直接編集する。
    /// （AnimatorController ファイルへの保存は行わず、NDMF のコミット処理に任せる）
    /// </summary>
    internal static class AnimatorCompiler
    {
        /// <summary>
        /// NDMF プラグインから呼ばれるメインエントリ。
        /// 既存の VirtualAnimatorController を animscript の内容で上書きする。
        /// </summary>
        public static void BuildInto(string source, string scriptAssetPath,
            VirtualAnimatorController controller, CloneContext cloneContext, DiagnosticBag diagnostics)
        {
            var ast = ParseAndValidate(source, diagnostics);
            if (ast == null) return;

            BuildInto(ast, scriptAssetPath, controller, cloneContext, diagnostics);
        }

        // ================================================================
        // 解析・検証
        // ================================================================

        /// <summary>
        /// animscript の内容を解析し、検証する。
        /// </summary>
        public static AnimScriptAst? ParseAndValidate(string source, DiagnosticBag diagnostics)
        {
            var ast = Parser.Parse(source, diagnostics);

            if (!diagnostics.HasErrors)
                AstExpander.Expand(ast, diagnostics);

            if (!diagnostics.HasErrors)
                ScriptValidator.Validate(ast, diagnostics);

            if (diagnostics.HasErrors)
                return null;

            return ast;
        }

        // ================================================================
        // VAC 構築
        // ================================================================

        static void BuildInto(AnimScriptAst ast, string scriptAssetPath,
            VirtualAnimatorController controller, CloneContext cloneContext, DiagnosticBag diagnostics)
        {
            // --- モーション解決（AnimationClip / BlendTree → VirtualMotion） ---
            var motionMap = new Dictionary<StateAst, VirtualMotion>();
            foreach (var layer in ast.Layers)
            foreach (var state in layer.States)
            {
                if (!state.HasClip || state.ClipPath == null) continue;

                var motion = ClipResolver.Resolve(state.ClipPath, scriptAssetPath, state.Location, diagnostics);
                if (motion != null)
                    motionMap[state] = cloneContext.Clone(motion)!;
            }

            // --- loop on/off は VirtualClip（クローン）に対して適用 ---
            ApplyLoopSettings(ast, motionMap, diagnostics);

            // --- パラメータ ---
            foreach (var parameterAst in ast.Parameters)
                controller.SetParameter(parameterAst.Name, ParameterBuilder.Build(parameterAst));

            // --- レイヤー（default 指定があれば先頭に） ---
            foreach (var layerAst in OrderLayers(ast.Layers))
            {
                var layer = controller.AddLayer(LayerPriority.Default, layerAst.Name);
                layer.BlendingMode = layerAst.BlendingMode ?? AnimatorLayerBlendingMode.Override;
                layer.DefaultWeight = (float)(layerAst.Weight?.Const() ?? 1.0);
                if (layerAst.AvatarMaskPath != null)
                {
                    var mask = ResolveAvatarMask(layerAst.AvatarMaskPath, scriptAssetPath,
                        layerAst.Location, diagnostics);
                    if (mask != null)
                        layer.AvatarMask = cloneContext.Clone(mask)!;
                }

                LayerBuilder.Build(layer, layerAst, motionMap, diagnostics);
            }
        }

        static AvatarMask ResolveAvatarMask(string maskText, string scriptAssetPath,
            SourceLocation location, DiagnosticBag diagnostics)
        {
            var text = maskText.Replace('\\', '/');
            var directory = AnimScriptPaths.GetDirectoryPath(scriptAssetPath);
            var candidates = text.StartsWith("Assets/") || text.StartsWith("Packages/")
                ? new[] { text }
                : new[] { directory + "/" + text };

            foreach (var candidate in candidates)
            {
                var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(candidate);
                if (mask != null) return mask;
            }

            diagnostics.Error(location,
                $"AvatarMask「{maskText}」が見つかりません。スクリプトからの相対パス、または Assets/ / Packages/ からのパスを指定してください");
            return null;
        }

        // ================================================================
        // loop on/off（VirtualClip のクローンに対して適用）
        // ================================================================

        static void ApplyLoopSettings(AnimScriptAst ast,
            IReadOnlyDictionary<StateAst, VirtualMotion> motionMap, DiagnosticBag diagnostics)
        {
            var loopByClip = new Dictionary<VirtualClip, bool>();

            foreach (var layer in ast.Layers)
            foreach (var state in layer.States)
            {
                var loop = state.Loop ?? layer.Loop;
                if (!loop.HasValue) continue;
                if (!motionMap.TryGetValue(state, out var motion)) continue;
                if (!(motion is VirtualClip clip)) continue;

                if (loopByClip.TryGetValue(clip, out var existing) && existing != loop.Value)
                {
                    diagnostics.Warning(state.Location,
                        $"クリップ「{clip.Name}」に loop on / off の両方が指定されています。後の指定が使われます");
                }
                loopByClip[clip] = loop.Value;
            }

            foreach (var pair in loopByClip)
            {
                var settings = pair.Key.Settings;
                if (settings.loopTime == pair.Value) continue;

                settings.loopTime = pair.Value;
                pair.Key.Settings = settings;

                diagnostics.Info(new SourceLocation(1, 1),
                    $"クリップ「{pair.Key.Name}」の Loop Time を {(pair.Value ? "on" : "off")} に変更しました");
            }
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
    }
}
