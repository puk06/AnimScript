namespace net.puk06.AnimScript
{
    /// <summary>
    /// ソースコード上の位置（1始まりの行番号・桁番号）。
    /// エラーメッセージに「何行目か」を出すために使う。
    /// </summary>
    internal readonly struct SourceLocation
    {
        public int Line { get; }
        public int Column { get; }

        public SourceLocation(int line, int column)
        {
            Line = line;
            Column = column;
        }

        public override string ToString() => $"{Line}行目";
    }
}
