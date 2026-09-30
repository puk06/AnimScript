namespace net.puk06.AnimScript
{
    /// <summary>
    /// 文を解析している場所。ループの中身などは「どこに書かれたか」で
    /// 使える文が変わるため、Parser がコンテキストを持ち回る。
    /// </summary>
    internal enum ParseContext
    {
        /// <summary>ファイル直下。param / layer が書ける。</summary>
        Script,

        /// <summary>layer ブロック内。state / 遷移 が書ける。</summary>
        Layer,

        /// <summary>driver ブロック内。set / add / random / copy が書ける。</summary>
        Driver,
    }
}
