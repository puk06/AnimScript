namespace net.puk06.AnimScript
{
    /// <summary>
    /// ビルド時変数の宣言。`var i = 0`
    /// 参照するときは `$i` と書く。生成されるコントローラには残らない。
    /// </summary>
    internal sealed class VarDeclAst : IBlockItem
    {
        public string Name { get; set; }
        public ExprAst Value { get; set; }
        public SourceLocation Location { get; set; }
    }

    /// <summary>
    /// ビルド時変数への代入。`i = $i + 1`
    /// while ループのカウンタ更新などに使う。
    /// </summary>
    internal sealed class AssignAst : IBlockItem
    {
        public string Name { get; set; }
        public ExprAst Value { get; set; }
        public SourceLocation Location { get; set; }
    }
}
