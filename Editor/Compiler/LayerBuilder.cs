using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// LayerAst → AnimatorController の1レイヤー分の生成。
    /// ステート作成 → 配置 → デフォルトステート → 遷移作成 の順で組み立てる。
    /// </summary>
    internal static class LayerBuilder
    {
        public static void Build(AnimatorController controller, int layerIndex, LayerAst layerAst,
            IReadOnlyDictionary<StateAst, AnimationClip> clipMap, DiagnosticBag diagnostics)
        {
            var layer = controller.layers[layerIndex];
            var stateMachine = layer.stateMachine;

            // --- ステート作成 ---
            var states = new Dictionary<string, AnimatorState>();
            foreach (var stateAst in layerAst.States)
            {
                var state = StateBuilder.Build(stateMachine, stateAst, layerAst, clipMap, diagnostics);
                states[stateAst.Name] = state;
            }

            StateLayout.Apply(stateMachine);

            // --- デフォルトステート（entry -> X）---
            var entry = layerAst.Transitions.FirstOrDefault(t => t.Chain[0] == "entry");
            if (entry != null && states.ContainsKey(entry.Chain[1]))
                stateMachine.defaultState = states[entry.Chain[1]];

            // --- 遷移作成 ---
            // チェーン「A -> B -> C」は A→B と B→C の2区間になる。
            // 先頭が entry の場合は「デフォルトステート指定」なので、2番目以降の区間だけ作る。
            foreach (var transitionAst in layerAst.Transitions)
            {
                var chain = transitionAst.Chain;
                var startIndex = chain[0] == "entry" ? 1 : 0;

                for (var i = startIndex; i < chain.Count - 1; i++)
                {
                    var from = chain[i];
                    var to = chain[i + 1];

                    // Validator で弾かれているはずの組み合わせは、念のためここでもスキップ
                    if (from == "exit" || to == "entry") continue;
                    if (from == "any" && to == "exit") continue;

                    TransitionBuilder.Build(stateMachine, states, transitionAst, from, to);
                }
            }
        }
    }
}
