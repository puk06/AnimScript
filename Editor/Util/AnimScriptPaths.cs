namespace net.puk06.AnimScript
{
    /// <summary>
    /// animscript のパス回りのユーティリティ。
    /// NDMF ビルド時はファイル出力を行わないが、インポート時の .animscript 保存や
    /// クリップの相対パス解決に使う。
    /// </summary>
    internal static class AnimScriptPaths
    {
        /// <summary>"Assets/Foo/Bar/x.animscript" → "Assets/Foo/Bar"</summary>
        public static string GetDirectoryPath(string assetPath)
        {
            var index = assetPath.LastIndexOf('/');
            return index >= 0 ? assetPath.Substring(0, index) : "Assets";
        }
    }
}
