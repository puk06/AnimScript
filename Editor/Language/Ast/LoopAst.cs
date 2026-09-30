using System.Collections.Generic;

namespace net.puk06.AnimScript
{
    internal enum LoopKind
    {
        For,
        While,
    }

    /// <summary>while の比較演算子。ビルド時に自分たちで評価するので >= / <= も使える。</summary>
    internal enum CmpOp
    {
        Equals,
        NotEquals,
        Less,
        Greater,
        LessEqual,
        GreaterEqual,
    }

    /// <summary>
    /// while の条件。`$i < 8` のような比較、または `$flag` のような式単体（0以外で真）。
    /// </summary>
    internal sealed class WhileCondAst
    {
        public ExprAst Left { get; set; }

        /// <summary>比較演算子。null の場合は「Left が 0 以外なら真」。</summary>
        public CmpOp? Op { get; set; }

        public ExprAst Right { get; set; }
        public SourceLocation Location { get; set; }
    }

    /// <summary>
    /// ビルド時ループ。
    /// <code>
    /// for i in 0..7 { ... }        … 0〜7（両端含む）で Body を展開
    /// for i in 0..10 step 2 { ... } … 刻み幅つき
    /// while $i &lt; 8 { ... }        … 条件が成り立つ間 Body を展開
    /// </code>
    /// Body にはそのブロックで使える要素（state / 遷移 / param / layer / driver アクション / ネストしたループ等）が入る。
    /// </summary>
    internal sealed class LoopAst : IBlockItem
    {
        public LoopKind Kind { get; set; }

        // --- for 用 ---
        public string VarName { get; set; }
        public ExprAst From { get; set; }
        public ExprAst To { get; set; }
        public ExprAst Step { get; set; } // null なら 1

        // --- while 用 ---
        public WhileCondAst Condition { get; set; }

        public List<IBlockItem> Body { get; } = new List<IBlockItem>();

        public SourceLocation Location { get; set; }
    }
}
