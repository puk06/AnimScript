namespace net.puk06.AnimScript
{
    /// <summary>
    /// ブロック（ファイル直下・layer 内・driver 内・ループ内）に並べられる要素の印。
    /// param / layer / state / 遷移 / driver アクション / var / 代入 / for / while が該当する。
    /// </summary>
    internal interface IBlockItem
    {
        SourceLocation Location { get; }
    }
}
