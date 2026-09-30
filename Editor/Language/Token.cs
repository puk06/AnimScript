namespace net.puk06.AnimScript
{
    /// <summary>
    /// 字句解析が出力するトークン1個分。
    /// 数値の場合は解析済みの値を <see cref="NumberValue"/> に持つ。
    /// </summary>
    internal readonly struct Token
    {
        public TokenKind Kind { get; }
        public string Text { get; }
        public double NumberValue { get; }
        public SourceLocation Location { get; }

        public Token(TokenKind kind, string text, SourceLocation location, double numberValue = 0)
        {
            Kind = kind;
            Text = text;
            Location = location;
            NumberValue = numberValue;
        }

        /// <summary>エラーメッセージ用の読みやすい表現。</summary>
        public string Describe()
        {
            switch (Kind)
            {
                case TokenKind.EndOfFile: return "ファイルの終わり";
                case TokenKind.Newline: return "行の終わり";
                case TokenKind.String: return $"\"{Text}\"";
                default: return Text;
            }
        }
    }
}
