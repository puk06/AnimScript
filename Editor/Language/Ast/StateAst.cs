using System.Collections.Generic;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// ステート定義。1行書きとブロック書きの両方がこの形にまとまる。
    ///
    /// <code>
    /// state Idle = "Idle.anim" speed 1.2
    /// state Empty              // motion 無しの空ステート
    /// state Pose {
    ///     clip "Pose.anim"
    ///     time MotionTime
    ///     wd off
    ///     driver { ... }
    /// }
    /// </code>
    /// </summary>
    internal sealed class StateAst : IBlockItem
    {
        public string Name { get; set; }

        /// <summary>clip 指定が書かれたかどうか。</summary>
        public bool HasClip { get; set; }

        /// <summary>
        /// モーション参照文字列（AnimationClip のパスまたは名前、BlendTree のパス。$変数を含むことがある）。
        /// HasClip == true でも null の場合は `clip none`（明示的な空）。
        /// </summary>
        public string ClipPath { get; set; }

        public ExprAst Speed { get; set; }
        /// <summary>再生速度を制御するパラメータ名。未指定なら無効。</summary>
        public string SpeedParameter { get; set; }
        /// <summary>クリップの再生開始位置。</summary>
        public ExprAst CycleOffset { get; set; }
        /// <summary>クリップの再生開始位置を制御するパラメータ名。未指定なら無効。</summary>
        public string CycleOffsetParameter { get; set; }
        /// <summary>Motion Time を制御するパラメータ名。未指定なら無効。</summary>
        public string TimeParameter { get; set; }
        public bool? WriteDefaults { get; set; }
        public bool? Loop { get; set; }
        public bool? Mirror { get; set; }
        /// <summary>ミラー再生を制御するパラメータ名。未指定なら無効。</summary>
        public string MirrorParameter { get; set; }
        public bool? FootIK { get; set; }

        public List<DriverBlockAst> Drivers { get; } = new List<DriverBlockAst>();
        public List<PlayableLayerControlAst> PlayableLayerControls { get; } = new List<PlayableLayerControlAst>();
        public List<AnimatorTrackingControlAst> AnimatorTrackingControls { get; } = new List<AnimatorTrackingControlAst>();

        public SourceLocation Location { get; set; }
    }
}
