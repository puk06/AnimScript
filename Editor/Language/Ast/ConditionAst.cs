namespace net.puk06.AnimScript
{
    internal enum CondOp
    {
        If,         // Param        … true のとき
        IfNot,      // !Param       … false のとき
        Greater,    // Param > x
        Less,       // Param < x
        Equals,     // Param == x
        NotEquals,  // Param != x
    }

    /// <summary>
    /// `when` 以降の条件1つ分。`and` で連結された場合はリストになる。
    /// Unity の AnimatorCondition にそのまま対応する。
    /// （>= / <= は AnimatorConditionMode に無いので意図的に非対応。
    ///   while ループの条件では使える）
    /// </summary>
    internal sealed class ConditionAst
    {
        public string Param { get; set; }
        public CondOp Op { get; set; }

        /// <summary>比較する値（If / IfNot のときは未使用）。</summary>
        public ExprAst Value { get; set; }

        public SourceLocation Location { get; set; }
    }
}
