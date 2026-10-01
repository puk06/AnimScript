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
        /// クリップ参照文字列（パスまたは名前。$変数を含むことがある）。
        /// HasClip == true でも null の場合は `clip none`（明示的な空）。
        /// </summary>
        public string ClipPath { get; set; }

        public ExprAst Speed { get; set; }
        /// <summary>Motion Time を制御するパラメータ名。未指定なら無効。</summary>
        public string TimeParameter { get; set; }
        public bool? WriteDefaults { get; set; }
        public bool? Loop { get; set; }
        public bool? Mirror { get; set; }
        public bool? FootIK { get; set; }

        /// <summary>VRC Parameter Driver ブロック。無ければ null。</summary>
        public DriverBlockAst Driver { get; set; }

        public SourceLocation Location { get; set; }
    }
}
