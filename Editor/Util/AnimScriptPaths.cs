using System.IO;
using UnityEditor;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// animscript のパス回りのユーティリティ。
    /// 出力先は「スクリプトと同じフォルダの build/スクリプト名.controller」。
    /// </summary>
    internal static class AnimScriptPaths
    {
        /// <summary>"Assets/Foo/Bar/x.animscript" → "Assets/Foo/Bar"</summary>
        public static string GetDirectoryPath(string assetPath)
        {
            var index = assetPath.LastIndexOf('/');
            return index >= 0 ? assetPath.Substring(0, index) : "Assets";
        }

        /// <summary>出力先フォルダ（"Assets/Foo/Bar/build"）。</summary>
        public static string GetOutputDirectoryPath(string assetPath)
            => GetDirectoryPath(assetPath) + "/build";

        /// <summary>出力先の .controller アセットパス。</summary>
        public static string GetOutputAssetPath(string assetPath)
        {
            var fileName = Path.GetFileNameWithoutExtension(assetPath);
            return $"{GetOutputDirectoryPath(assetPath)}/{fileName}.controller";
        }

        /// <summary>出力先フォルダを（無ければ）作る。</summary>
        public static void EnsureFolderExists(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath)) return;

            // Unity のカレントディレクトリはプロジェクトルートなので、そのまま絶対パス化できる
            Directory.CreateDirectory(Path.GetFullPath(assetFolderPath));
            AssetDatabase.Refresh();
        }
    }
}
