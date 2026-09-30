namespace net.puk06.AnimScript
{
    internal enum ParamKind
    {
        Float,
        Int,
        Bool,
    }

    /// <summary>
    /// `param Speed : float = 0` のようなパラメータ宣言。
    /// AnimatorController のパラメータになる。
    /// </summary>
    internal sealed class ParameterAst : IBlockItem
    {
        public string Name { get; set; }
        public ParamKind Kind { get; set; }

        /// <summary>初期値。null なら型の既定値（0 / false）。</summary>
        public ExprAst DefaultValue { get; set; }

        public SourceLocation Location { get; set; }
    }
}
