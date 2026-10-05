using UnityEditor;
using UnityEditor.Animations;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// Project ウィンドウで AnimatorController のチェック画面を開くメニュー。
    /// </summary>
    internal static class ControllerCheckMenuItem
    {
        const string MenuPath = "Assets/AnimScript/Animator Controllerをチェック";

        [MenuItem(MenuPath, true)]
        static bool Validate()
        {
            return Selection.objects.Length == 1 && Selection.objects[0] is AnimatorController;
        }

        [MenuItem(MenuPath, false)]
        static void Check()
        {
            if (Selection.objects.Length == 1 && Selection.objects[0] is AnimatorController controller)
                AnimScriptWindow.OpenCheck(controller);
        }
    }
}
