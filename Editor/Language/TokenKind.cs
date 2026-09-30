namespace net.puk06.AnimScript
{
    /// <summary>
    /// animscript のトークン種別。
    /// キーワード（param / layer / state など）は文脈依存なので、
    /// 字句解析段階ではすべて <see cref="Identifier"/> として扱い、Parser 側で解釈する。
    /// </summary>
    internal enum TokenKind
    {
        Identifier,     // 名前・キーワード・$変数参照（Pose$i のように埋め込み可）
        Number,         // 数値リテラル（符号は持たない。負数は式で表現）
        String,         // "..." （エスケープ無しのシンプル仕様。$変数の埋め込み可）

        Colon,          // :
        Equals,         // =
        EqualsEquals,   // ==
        Bang,           // !
        BangEquals,     // !=
        Greater,        // >
        GreaterEqual,   // >= （比較には使えないが、良いエラーを出すために字句解析では認識する）
        Less,           // <
        LessEqual,      // <= （同上）
        Arrow,          // ->

        OpenBrace,      // {
        CloseBrace,     // }
        OpenParen,      // (
        CloseParen,     // )

        Plus,           // +
        Minus,          // - （負数は「0 - x」として式で表現する）
        Star,           // *
        Slash,          // / （// はコメント）
        Percent,        // %
        DotDot,         // .. （for の範囲指定）

        Newline,        // 改行（文の区切りとして意味を持つ）
        EndOfFile,
    }
}
