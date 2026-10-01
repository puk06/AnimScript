using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// Project ウィンドウで AnimatorController を右クリックして
    /// animscript に変換するメニュー。
    /// </summary>
    internal static class ImportMenuItem
    {
        const string MenuPath = "Assets/AnimScript/AnimScriptに変換";

        [MenuItem(MenuPath, true)]
        static bool Validate()
        {
            foreach (var obj in Selection.objects)
                if (obj is AnimatorController) return true;
            return false;
        }

        [MenuItem(MenuPath, false)]
        static void Convert()
        {
            var controllers = new List<AnimatorController>();
            foreach (var obj in Selection.objects)
            {
                if (obj is AnimatorController controller)
                    controllers.Add(controller);
            }
            if (controllers.Count == 0) return;

            var totalWarnings = new List<string>();
            foreach (var controller in controllers)
            {
                var warnings = new List<string>();
                var path = ControllerImporter.ConvertAndSave(controller, warnings);
                totalWarnings.AddRange(warnings);

                if (warnings.Count > 0)
                {
                    Debug.LogWarning($"[AnimScriptBuilder] 「{controller.name}」の変換で {warnings.Count} 件の警告がありました:\n" +
                                     string.Join("\n", warnings));
                }
                Debug.Log($"[AnimScriptBuilder] {path} に変換しました");
            }

            if (totalWarnings.Count > 0)
            {
                EditorUtility.DisplayDialog("AnimScript 変換完了",
                    $"{controllers.Count} 件のコントローラを変換しました。\n" +
                    $"{totalWarnings.Count} 件の警告があります。詳細は Console を確認してください。",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("AnimScript 変換完了",
                    $"{controllers.Count} 件のコントローラを変換しました。",
                    "OK");
            }
        }
    }
}
