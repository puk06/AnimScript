using System;
using UnityEditor;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// Project ウィンドウで AnimScript ファイルを単体チェックするメニュー。
    /// </summary>
    internal static class ScriptCheckMenuItem
    {
        const string MenuPath = "Assets/AnimScript/AnimScriptをビルド確認";

        [MenuItem(MenuPath, true)]
        static bool Validate()
        {
            if (Selection.objects.Length != 1) return false;

            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            return !string.IsNullOrEmpty(path)
                && path.EndsWith(".animscript", StringComparison.OrdinalIgnoreCase);
        }

        [MenuItem(MenuPath, false)]
        static void Check()
        {
            if (Selection.objects.Length != 1) return;

            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!string.IsNullOrEmpty(path)
                && path.EndsWith(".animscript", StringComparison.OrdinalIgnoreCase))
                AnimScriptWindow.OpenScriptCheck(path);
        }
    }
}
