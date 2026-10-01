using System.Collections.Generic;
using System.Text;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// Unity 側の名前を animscript の識別子として使える形に変換する。
    ///
    /// animscript の識別子は「文字・数字・_（数字始まり不可）」なので、
    /// スペースや記号を含むステート名などはそのまま書き出すと読み戻せない。
    /// 使えない文字は _ に置き換え、キーワードとの衝突も _ 付きで回避する。
    /// 変換は純粋関数（同じ入力には同じ出力）なので、宣言と参照で結果がズレない。
    /// </summary>
    internal static class NameSanitizer
    {
        // animscript のキーワード。これらと同名の名前は _ 付きで回避する
        static readonly HashSet<string> Keywords = new HashSet<string>
        {
            "param", "layer", "state", "entry", "exit", "any",
            "when", "and", "or", "dur", "offset", "exitTime", "self",
            "speed", "speedParam", "sp", "cycleOffset", "co", "cycleOffsetParam", "cop",
            "time", "mirror", "mirrorParam", "mp", "wd", "loop", "clip", "none",
            "driver", "localOnly", "set", "add", "random", "copy",
            "default", "weight", "float", "int", "bool",
            "on", "off", "var", "for", "while", "in", "step",
            "true", "false",
        };

        /// <summary>変換した場合に警告を積む版（宣言箇所で使う）。</summary>
        public static string Sanitize(string name, List<string> warnings)
        {
            var sanitized = SanitizeCore(name);
            if (sanitized != name)
                warnings.Add($"名前「{name}」は animscript で使えない文字を含むため「{sanitized}」に変換しました");
            return sanitized;
        }

        /// <summary>警告を出さない版（参照箇所で使う。宣言時の警告で十分なため）。</summary>
        public static string SanitizeQuiet(string name) => SanitizeCore(name);

        /// <summary>
        /// 重複しない名前にする（ステート名用）。
        /// 「A B」と「A_B」が両方「A_B」になるようなケースを _2 付きで回避する。
        /// </summary>
        public static string SanitizeUnique(string name, HashSet<string> usedNames, List<string> warnings)
        {
            var sanitized = Sanitize(name, warnings);
            if (usedNames.Add(sanitized)) return sanitized;

            var index = 2;
            while (!usedNames.Add(sanitized + "_" + index)) index++;

            var unique = sanitized + "_" + index;
            warnings.Add($"名前「{name}」は変換後に重複したため「{unique}」に変換しました");
            return unique;
        }

        static string SanitizeCore(string name)
        {
            var builder = new StringBuilder(name.Length);
            foreach (var c in name)
                builder.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');

            if (builder.Length == 0)
                builder.Append('_');

            // 先頭が数字なら _ を付ける（animscript の識別子は数字始まり不可）
            if (char.IsDigit(builder[0]))
                builder.Insert(0, '_');

            // キーワードと衝突する場合も _ を付ける
            if (Keywords.Contains(builder.ToString()))
                builder.Insert(0, '_');

            return builder.ToString();
        }
    }
}
