namespace net.puk06.AnimScript
{
    /// <summary>
    /// 条件式（`when` の右側）のAST。
    /// 項（<see cref="ConditionAst"/>）か、and/or で結合したもの（<see cref="ConditionBinaryAst"/>）。
    ///
    /// Unity の遷移条件は AND のリストしか持てないため、
    /// or を含む式は生成時に DNF（AND のかたまりの OR）へ展開され、
    /// かたまり1つにつき1本の遷移が作られる（<see cref="ConditionLogic"/> 参照）。
    /// </summary>
    internal abstract class ConditionExprAst
    {
        public SourceLocation Location { get; set; }
    }

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
    /// 条件の「項」1つ分。`Param`、`!Param`、`Param > 1` など。
    /// Unity の AnimatorCondition にそのまま対応する。
    /// （>= / <= は AnimatorConditionMode に無いので意図的に非対応。
    ///   while ループの条件では使える）
    /// </summary>
    internal sealed class ConditionAst : ConditionExprAst
    {
        public string Param { get; set; }
        public CondOp Op { get; set; }

        /// <summary>比較する値（If / IfNot のときは未使用）。</summary>
        public ExprAst Value { get; set; }
    }

    /// <summary>
    /// and / or での条件の結合。`A and B`、`A or (B and C)` など。
    /// 優先順位は and > or（括弧で明示もできる）。
    /// </summary>
    internal sealed class ConditionBinaryAst : ConditionExprAst
    {
        public bool IsOr { get; set; }
        public ConditionExprAst Left { get; set; }
        public ConditionExprAst Right { get; set; }
    }
}
