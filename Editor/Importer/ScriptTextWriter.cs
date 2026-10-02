using System.Globalization;
using System.Text;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// animscript のテキストを組み立てる小さなヘルパー。
    /// インデント管理と行出力だけを担当する（インポーター用）。
    /// </summary>
    internal sealed class ScriptTextWriter
    {
        readonly StringBuilder _builder = new StringBuilder();
        int _indent;

        public void Indent() => _indent++;

        public void Unindent() => _indent = _indent > 0 ? _indent - 1 : 0;

        /// <summary>1行書く。空文字なら空行になる。</summary>
        public void Line(string text = "")
        {
            if (text.Length > 0)
                _builder.Append(' ', _indent * 4).Append(text);
            _builder.Append('\n');
        }

        /// <summary>コメント行を書く。</summary>
        public void Comment(string text) => Line("// " + text);

        public override string ToString() => _builder.ToString();

        /// <summary>
        /// 数値を animscript 用に整形する。整数なら小数点を付けない。
        /// （元の値が float の誤差を含んでいても、最短表記で出る）
        /// </summary>
        public static string Format(float value)
        {
            // Unity の計算誤差で出る極小値は、意図した 0 として扱う。
            if (System.Math.Abs(value) < 0.000001f)
                return "0";

            if (value == System.Math.Floor(value) && !float.IsInfinity(value))
                return ((long)value).ToString(CultureInfo.InvariantCulture);
            // G 指定は指数表記になることがあるため、AnimScript の構文に合わせて固定小数点で出力する。
            return value.ToString("0.#########", CultureInfo.InvariantCulture);
        }
    }
}
