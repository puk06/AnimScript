using System.Collections.Generic;
using nadena.dev.ndmf.animator;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// StateAst → VirtualState の生成。
    /// クリップ・速度・WriteDefaults・VRC Parameter Driver をまとめて設定する。
    /// </summary>
    internal static class StateBuilder
    {
        public static VirtualState Build(VirtualStateMachine stateMachine, StateAst ast, LayerAst layer,
            IReadOnlyDictionary<StateAst, VirtualMotion> motionMap, DiagnosticBag diagnostics)
        {
            var state = stateMachine.AddState(ast.Name);

            // motion（clip none / 未指定なら null のまま = 空ステート）
            if (ast.HasClip && ast.ClipPath != null && motionMap.TryGetValue(ast, out var motion))
                state.Motion = motion;

            // WriteDefaults: ステート指定 → レイヤー指定 → Unity標準(true)
            state.WriteDefaultValues = ast.WriteDefaults ?? layer.WriteDefaults ?? true;

            if (ast.Speed != null)
                state.Speed = (float)ast.Speed.Const();

            if (ast.SpeedParameter != null)
                state.SpeedParameter = ast.SpeedParameter;

            if (ast.CycleOffset != null)
                state.CycleOffset = (float)ast.CycleOffset.Const();

            if (ast.CycleOffsetParameter != null)
                state.CycleOffsetParameter = ast.CycleOffsetParameter;

            if (ast.TimeParameter != null)
                state.TimeParameter = ast.TimeParameter;

            if (ast.Mirror.HasValue)
                state.Mirror = ast.Mirror.Value;

            if (ast.MirrorParameter != null)
                state.MirrorParameter = ast.MirrorParameter;

            if (ast.FootIK.HasValue)
                state.IKOnFeet = ast.FootIK.Value;

            foreach (var driver in ast.Drivers)
                DriverBuilder.Build(state, driver, diagnostics);
            foreach (var control in ast.PlayableLayerControls)
                DriverBuilder.Build(state, control, diagnostics);
            foreach (var control in ast.AnimatorTrackingControls)
                DriverBuilder.Build(state, control, diagnostics);

            return state;
        }
    }
}
