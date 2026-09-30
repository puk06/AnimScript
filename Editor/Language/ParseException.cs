using System;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// 構文エラーを示す例外。
    /// 投げる前に DiagnosticBag へエラーを記録済みなので、
    /// キャッチ側は「次の文まで読み飛ばす」だけでよい（パニックモードリカバリ）。
    /// </summary>
    internal sealed class ParseException : Exception
    {
        public ParseException(string message) : base(message) { }
    }
}
