using System.Collections.Generic;
using System.Linq;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// AST の意味チェック。
    /// 構文として正しいが意味的におかしいもの
    /// （未定義ステートへの遷移、宣言されていないパラメータ、entry/exit/any の置き方 etc.）
    /// をここで検出する。
    /// </summary>
    internal static class ScriptValidator
    {
        public static void Validate(AnimScriptAst ast, DiagnosticBag diagnostics)
        {
            ValidateParameters(ast, diagnostics);
            ValidateLayers(ast, diagnostics);
        }

        // ================================================================
        // パラメータ
        // ================================================================

        static void ValidateParameters(AnimScriptAst ast, DiagnosticBag diagnostics)
        {
            var names = new HashSet<string>();
            foreach (var parameter in ast.Parameters)
            {
                if (!names.Add(parameter.Name))
                    diagnostics.Error(parameter.Location, $"パラメータ「{parameter.Name}」が重複して宣言されています");
            }
        }

        // ================================================================
        // レイヤー
        // ================================================================

        static void ValidateLayers(AnimScriptAst ast, DiagnosticBag diagnostics)
        {
            if (ast.Layers.Count == 0)
                diagnostics.Error(new SourceLocation(1, 1), "layer が1つも定義されていません");

            // レイヤー名の重複
            var layerNames = new HashSet<string>();
            foreach (var layer in ast.Layers)
            {
                if (!layerNames.Add(layer.Name))
                    diagnostics.Error(layer.Location, $"レイヤー「{layer.Name}」が重複しています");
            }

            // default は1つまで
            var defaultLayers = ast.Layers.Where(l => l.IsDefault).ToList();
            if (defaultLayers.Count > 1)
            {
                foreach (var layer in defaultLayers.Skip(1))
                    diagnostics.Error(layer.Location, "default レイヤーは1つだけ指定できます");
            }

            var declaredParams = new HashSet<string>(ast.Parameters.Select(p => p.Name));

            foreach (var layer in ast.Layers)
                ValidateLayerBody(layer, declaredParams, diagnostics);
        }

        static void ValidateLayerBody(LayerAst layer, HashSet<string> declaredParams, DiagnosticBag diagnostics)
        {
            // ステート名の重複
            var stateNames = new HashSet<string>();
            foreach (var state in layer.States)
            {
                if (!stateNames.Add(state.Name))
                    diagnostics.Error(state.Location, $"レイヤー「{layer.Name}」内でステート「{state.Name}」が重複しています");
            }

            // driver のパラメータ存在チェック（警告どまり。VRCExpressionParameters 側で定義されている運用もあるため）
            foreach (var state in layer.States)
            {
                if (state.TimeParameter != null && !declaredParams.Contains(state.TimeParameter))
                    diagnostics.Warning(state.Location, $"time の対象パラメータ「{state.TimeParameter}」が param 宣言されていません");

                if (state.Driver == null) continue;
                foreach (var action in state.Driver.Actions)
                {
                    if (!declaredParams.Contains(action.Target))
                        diagnostics.Warning(action.Location, $"driver の対象パラメータ「{action.Target}」が param 宣言されていません");
                    if (action.Kind == DriverActionKind.Copy && !declaredParams.Contains(action.Source))
                        diagnostics.Warning(action.Location, $"copy 元のパラメータ「{action.Source}」が param 宣言されていません");
                }
            }

            // entry -> の数
            var entryTransitions = layer.Transitions.Where(t => t.Chain[0] == "entry").ToList();
            foreach (var extra in entryTransitions.Skip(1))
                diagnostics.Error(extra.Location, "entry -> は1レイヤーにつき1つだけ書けます");
            if (entryTransitions.Count == 0 && layer.States.Count > 0)
                diagnostics.Warning(layer.Location, $"レイヤー「{layer.Name}」に entry -> が無いため、最初のステート「{layer.States[0].Name}」がデフォルトになります");

            // 遷移チェーンのチェック
            foreach (var transition in layer.Transitions)
                ValidateTransition(layer, transition, stateNames, declaredParams, diagnostics);

            // どこからも遷移されないステートの警告
            WarnUnreachableStates(layer, entryTransitions, diagnostics);
        }

        // ================================================================
        // 遷移
        // ================================================================

        static void ValidateTransition(LayerAst layer, TransitionAst transition,
            HashSet<string> stateNames, HashSet<string> declaredParams, DiagnosticBag diagnostics)
        {
            var chain = transition.Chain;

            // 特殊ノード（entry / exit / any）の位置チェック＆ステート存在チェック
            for (var i = 0; i < chain.Count; i++)
            {
                var node = chain[i];
                switch (node)
                {
                    case "entry":
                        if (i != 0)
                            diagnostics.Error(transition.Location, "entry はチェーンの先頭にだけ書けます");
                        break;
                    case "any":
                        if (i != 0)
                            diagnostics.Error(transition.Location, "any はチェーンの先頭にだけ書けます");
                        break;
                    case "exit":
                        if (i != chain.Count - 1)
                            diagnostics.Error(transition.Location, "exit はチェーンの末尾にだけ書けます");
                        break;
                    default:
                        if (!stateNames.Contains(node))
                            diagnostics.Error(transition.Location,
                                $"ステート「{node}」はレイヤー「{layer.Name}」に定義されていません（スペルミスか、state の宣言忘れです）");
                        break;
                }
            }

            // entry 始まり
            if (chain[0] == "entry")
            {
                if (chain[1] == "exit" || chain[1] == "any")
                    diagnostics.Error(transition.Location, "entry の次はステート名を書いてください（entry -> ステート名）");

                var hasOptions = transition.Condition != null
                                  || transition.ExitTime != null
                                  || transition.Duration != null
                                  || transition.Offset != null
                                  || transition.AllowSelf;
                if (chain.Count == 2 && hasOptions)
                    diagnostics.Error(transition.Location,
                        "entry -> ステート名 はデフォルトステートの指定なので、when / exitTime / dur / offset / self は付けられません");
            }

            // any 始まり
            if (chain[0] == "any" && chain[chain.Count - 1] == "exit")
                diagnostics.Error(transition.Location, "any から exit への遷移は作れません（Unity の仕様上、AnyState は Exit へ遷移できません）");

            // self は any からの遷移専用
            if (transition.AllowSelf && chain[0] != "any")
                diagnostics.Error(transition.Location, "self は any -> からの遷移でのみ使えます");

            // 条件で使うパラメータは宣言必須（宣言が無いと Animator 側で条件が機能しない）
            foreach (var term in ConditionLogic.EnumerateTerms(transition.Condition))
            {
                if (!declaredParams.Contains(term.Param))
                    diagnostics.Error(term.Location,
                        $"パラメータ「{term.Param}」は宣言されていません。スクリプト上部に「param {term.Param} : float」のように宣言してください");
            }

            // or 展開でできる遷移の本数チェック（暴走防止）
            var termCount = ConditionLogic.CountTerms(transition.Condition);
            if (termCount > ConditionLogic.MaxDnfTerms)
            {
                diagnostics.Error(transition.Location,
                    $"or の組み合わせが多すぎます。この行から {termCount} 本の遷移が作られます（上限は {ConditionLogic.MaxDnfTerms} 本）。条件を整理してください");
            }
        }

        static void WarnUnreachableStates(LayerAst layer, List<TransitionAst> entryTransitions, DiagnosticBag diagnostics)
        {
            // 到達可能 = どこかの遷移の行き先になっている or デフォルトステート
            var reachable = new HashSet<string>();
            foreach (var transition in layer.Transitions)
            {
                // チェーンの2番目以降はすべて「行き先」
                for (var i = 1; i < transition.Chain.Count; i++)
                    reachable.Add(transition.Chain[i]);
            }
            if (entryTransitions.Count == 1)
                reachable.Add(entryTransitions[0].Chain[1]);
            else if (entryTransitions.Count == 0 && layer.States.Count > 0)
                reachable.Add(layer.States[0].Name); // entry 無指定時は先頭ステートがデフォルトになる

            foreach (var state in layer.States)
            {
                if (!reachable.Contains(state.Name))
                    diagnostics.Warning(state.Location, $"ステート「{state.Name}」にはどこからも遷移できません");
            }
        }
    }
}
