namespace net.puk06.AnimScript
{
    internal enum DiagnosticSeverity
    {
        Info,
        Warning,
        Error,
    }

    /// <summary>
    /// ビルド中に発生したメッセージ1件分（エラー・警告・情報）。
    /// 行番号付きでユーザーに表示する。
    /// </summary>
    internal sealed class Diagnostic
    {
        public DiagnosticSeverity Severity { get; }
        public string Message { get; }
        public SourceLocation Location { get; }

        public Diagnostic(DiagnosticSeverity severity, SourceLocation location, string message)
        {
            Severity = severity;
            Location = location;
            Message = message;
        }

        public override string ToString() => $"[{Severity}] {Location}: {Message}";
    }
}
