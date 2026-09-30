using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// animscript の字句解析器。
    /// ソース文字列をトークンの並びに分解する。
    ///
    /// 仕様メモ:
    /// ・改行は文の区切りなので Newline トークンとして出力する
    /// ・「//」以降は行末までコメント
    /// ・「-数字」は負の数値、「->」は矢印トークン
    /// ・文字列はエスケープ無しの単純な "..."
    /// </summary>
    internal static class Lexer
    {
        public static List<Token> Tokenize(string source, DiagnosticBag diagnostics)
        {
            var tokens = new List<Token>();

            var index = 0;
            var line = 1;
            var column = 1;

            // 現在位置を取得する小さなヘルパー
            SourceLocation Here() => new SourceLocation(line, column);

            // n 文字進める（改行をまたぐことはない前提。文字列内の改行は別途エラーにする）
            void Advance(int count = 1)
            {
                index += count;
                column += count;
            }

            while (index < source.Length)
            {
                var c = source[index];

                // --- 空白（改行以外）は読み飛ばす ---
                if (c == ' ' || c == '\t' || c == '\r')
                {
                    Advance();
                    continue;
                }

                // --- 改行 ---
                if (c == '\n')
                {
                    tokens.Add(new Token(TokenKind.Newline, "\n", Here()));
                    Advance();
                    line++;
                    column = 1;
                    continue;
                }

                // --- コメント（// 以降は行末まで）。1個の / は割り算トークン ---
                if (c == '/')
                {
                    if (Peek(source, index + 1) == '/')
                    {
                        while (index < source.Length && source[index] != '\n') Advance();
                        continue;
                    }
                    tokens.Add(new Token(TokenKind.Slash, "/", Here()));
                    Advance();
                    continue;
                }

                // --- 1文字トークン ---
                if (c == '{') { tokens.Add(new Token(TokenKind.OpenBrace, "{", Here())); Advance(); continue; }
                if (c == '}') { tokens.Add(new Token(TokenKind.CloseBrace, "}", Here())); Advance(); continue; }
                if (c == ':') { tokens.Add(new Token(TokenKind.Colon, ":", Here())); Advance(); continue; }
                if (c == '(') { tokens.Add(new Token(TokenKind.OpenParen, "(", Here())); Advance(); continue; }
                if (c == ')') { tokens.Add(new Token(TokenKind.CloseParen, ")", Here())); Advance(); continue; }
                if (c == '+') { tokens.Add(new Token(TokenKind.Plus, "+", Here())); Advance(); continue; }
                if (c == '*') { tokens.Add(new Token(TokenKind.Star, "*", Here())); Advance(); continue; }
                if (c == '%') { tokens.Add(new Token(TokenKind.Percent, "%", Here())); Advance(); continue; }

                // --- 2文字になりうる記号 ---
                if (c == '=')
                {
                    var location = Here();
                    if (Peek(source, index + 1) == '=') { tokens.Add(new Token(TokenKind.EqualsEquals, "==", location)); Advance(2); }
                    else { tokens.Add(new Token(TokenKind.Equals, "=", location)); Advance(); }
                    continue;
                }
                if (c == '!')
                {
                    var location = Here();
                    if (Peek(source, index + 1) == '=') { tokens.Add(new Token(TokenKind.BangEquals, "!=", location)); Advance(2); }
                    else { tokens.Add(new Token(TokenKind.Bang, "!", location)); Advance(); }
                    continue;
                }
                if (c == '>')
                {
                    var location = Here();
                    if (Peek(source, index + 1) == '=') { tokens.Add(new Token(TokenKind.GreaterEqual, ">=", location)); Advance(2); }
                    else { tokens.Add(new Token(TokenKind.Greater, ">", location)); Advance(); }
                    continue;
                }
                if (c == '<')
                {
                    var location = Here();
                    if (Peek(source, index + 1) == '=') { tokens.Add(new Token(TokenKind.LessEqual, "<=", location)); Advance(2); }
                    else { tokens.Add(new Token(TokenKind.Less, "<", location)); Advance(); }
                    continue;
                }

                // --- 「->」（矢印）か引き算か ---
                if (c == '-')
                {
                    var location = Here();
                    if (Peek(source, index + 1) == '>')
                    {
                        tokens.Add(new Token(TokenKind.Arrow, "->", location));
                        Advance(2);
                        continue;
                    }
                    tokens.Add(new Token(TokenKind.Minus, "-", location));
                    Advance();
                    continue;
                }

                // --- 「..」（for の範囲指定） ---
                if (c == '.')
                {
                    var location = Here();
                    if (Peek(source, index + 1) == '.')
                    {
                        tokens.Add(new Token(TokenKind.DotDot, "..", location));
                        Advance(2);
                        continue;
                    }
                    diagnostics.Error(location, "予期しない文字「.」です。範囲指定は「..」と書いてください（例: for i in 0..7）");
                    Advance();
                    continue;
                }

                // --- 数値 ---
                if (char.IsDigit(c))
                {
                    tokens.Add(ReadNumber(source, ref index, ref column, Here()));
                    continue;
                }

                // --- 文字列 ---
                if (c == '"')
                {
                    tokens.Add(ReadString(source, ref index, ref line, ref column, Here(), diagnostics));
                    continue;
                }

                // --- 識別子 / キーワード / $変数（日本語の名前もOK） ---
                if (char.IsLetter(c) || c == '_' || c == '$')
                {
                    tokens.Add(ReadIdentifier(source, ref index, ref column, Here(), diagnostics));
                    continue;
                }

                // --- どれにも当てはまらない文字 ---
                diagnostics.Error(Here(), $"読めない文字「{c}」です");
                Advance();
            }

            tokens.Add(new Token(TokenKind.EndOfFile, "", Here()));
            return tokens;
        }

        static char Peek(string source, int index)
            => index < source.Length ? source[index] : '\0';

        /// <summary>数値リテラル（整数・小数）。小数点は「.」固定。符号は持たない（式の単項マイナスで表現する）。</summary>
        static Token ReadNumber(string source, ref int index, ref int column, SourceLocation location)
        {
            var start = index;

            while (index < source.Length && char.IsDigit(source[index])) { index++; column++; }

            // 小数部（「1.」のように小数点の後に数字が無い場合は読まない）
            if (index < source.Length && source[index] == '.'
                && index + 1 < source.Length && char.IsDigit(source[index + 1]))
            {
                index++; column++;
                while (index < source.Length && char.IsDigit(source[index])) { index++; column++; }
            }

            var text = source.Substring(start, index - start);
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value);
            return new Token(TokenKind.Number, text, location, value);
        }

        /// <summary>"..." 文字列。エスケープは無し。改行をまたぐとエラー。</summary>
        static Token ReadString(string source, ref int index, ref int line, ref int column, SourceLocation location, DiagnosticBag diagnostics)
        {
            // 開き「"」を消費
            index++; column++;

            var builder = new StringBuilder();
            var terminated = false;
            while (index < source.Length)
            {
                var c = source[index];
                if (c == '"') { terminated = true; index++; column++; break; }
                if (c == '\n') break; // 改行は Lexer 本体側で処理させる
                builder.Append(c);
                index++; column++;
            }

            if (!terminated)
                diagnostics.Error(location, "文字列が「\"」で閉じられていません");

            return new Token(TokenKind.String, builder.ToString(), location);
        }

        /// <summary>
        /// 識別子。文字・数字・「_」に加えて、変数参照の `$name` を埋め込める。
        /// 例: `Pose$i`、`$i`、`Item$i$j`（先頭が $ の場合は変数参照1個分）
        /// 実際の置き換えは AstExpander が行う。
        /// </summary>
        static Token ReadIdentifier(string source, ref int index, ref int column, SourceLocation location, DiagnosticBag diagnostics)
        {
            var start = index;
            while (index < source.Length)
            {
                var c = source[index];

                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    index++; column++;
                    continue;
                }

                // $変数の埋め込み（$ の後には変数名が必要）
                if (c == '$')
                {
                    var next = index + 1 < source.Length ? source[index + 1] : '\0';
                    if (char.IsLetter(next) || next == '_')
                    {
                        index++; column++;
                        continue;
                    }
                    if (next == '$')
                    {
                        // $$ は「$ そのもの」のエスケープ。展開側で1個に戻すので、ここでは両方読む
                        index += 2; column += 2;
                        continue;
                    }
                    diagnostics.Error(new SourceLocation(location.Line, column),
                        "「$」の後には変数名が必要です。$ をそのまま書きたいときは「$$」と書いてください");
                    index++; column++;
                    continue;
                }

                break;
            }
            return new Token(TokenKind.Identifier, source.Substring(start, index - start), location);
        }
    }
}
