using System.Collections.Generic;

namespace net.puk06.AnimScript
{
    internal enum DriverActionKind
    {
        Set,
        Add,
        Random,
        Copy,
    }

    /// <summary>
    /// ステート内の `driver { ... }` ブロック。
    /// VRC Avatar Parameter Driver (StateMachineBehaviour) として出力される。
    ///
    /// Items … Parser が書き込む並び（ループ等を含む）
    /// Actions … AstExpander が展開後に書き込む平坦な結果。
    /// </summary>
    internal sealed class DriverBlockAst
    {
        /// <summary>localOnly 指定。Add/Random をローカルのみで動かす時に使う。</summary>
        public bool LocalOnly { get; set; }

        public List<IBlockItem> Items { get; } = new List<IBlockItem>();
        public List<DriverActionAst> Actions { get; } = new List<DriverActionAst>();

        public SourceLocation Location { get; set; }
    }

    /// <summary>
    /// driver ブロック内の1行。
    /// <code>
    /// set    Target 1        → Kind=Set,    Target, Value
    /// add    Target 0.5      → Kind=Add,    Target, Value
    /// random Target 0 10     → Kind=Random, Target, ValueMin, ValueMax
    /// copy   Source -> Target → Kind=Copy,  Source, Target
    /// </code>
    /// </summary>
    internal sealed class DriverActionAst : IBlockItem
    {
        public DriverActionKind Kind { get; set; }

        /// <summary>書き込み先パラメータ名。</summary>
        public string Target { get; set; }

        /// <summary>copy のコピー元パラメータ名。</summary>
        public string Source { get; set; }

        public ExprAst Value { get; set; }
        public ExprAst ValueMin { get; set; }
        public ExprAst ValueMax { get; set; }

        public SourceLocation Location { get; set; }
    }
}
