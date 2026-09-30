using System.Collections.Generic;
using System.Linq;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// 診断メッセージ（エラー/警告/情報）を集める入れ物。
    /// Lexer → Parser → Validator → Compiler と同じ袋を持ち回して、
    /// 最後にまとめてウィンドウへ表示する。
    /// </summary>
    internal sealed class DiagnosticBag
    {
        readonly List<Diagnostic> _diagnostics = new List<Diagnostic>();

        public bool HasErrors => _diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

        public void Error(SourceLocation location, string message)
            => _diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, location, message));

        public void Warning(SourceLocation location, string message)
            => _diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, location, message));

        public void Info(SourceLocation location, string message)
            => _diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info, location, message));

        /// <summary>行番号順に並んだ一覧を返す。</summary>
        public List<Diagnostic> ToList()
            => _diagnostics
                .OrderBy(d => d.Location.Line)
                .ThenBy(d => d.Location.Column)
                .ToList();
    }
}
