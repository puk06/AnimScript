using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// StateAst → AnimatorState の生成。
    /// クリップ・速度・WriteDefaults・VRC Parameter Driver をまとめて設定する。
    /// </summary>
    internal static class StateBuilder
    {
        public static AnimatorState Build(AnimatorStateMachine stateMachine, StateAst ast, LayerAst layer,
            IReadOnlyDictionary<StateAst, AnimationClip> clipMap, DiagnosticBag diagnostics)
        {
            var state = stateMachine.AddState(ast.Name);

            // motion（clip none / 未指定なら null のまま = 空ステート）
            if (ast.HasClip && ast.ClipPath != null && clipMap.TryGetValue(ast, out var clip))
                state.motion = clip;

            // WriteDefaults: ステート指定 → レイヤー指定 → Unity標準(true)
            state.writeDefaultValues = ast.WriteDefaults ?? layer.WriteDefaults ?? true;

            if (ast.Speed != null)
                state.speed = (float)ast.Speed.Const();

            if (ast.Driver != null)
                DriverBuilder.Build(state, ast.Driver, diagnostics);

            return state;
        }
    }
}
