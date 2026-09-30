using UnityEditor;
using UnityEngine;

namespace net.puk06.AnimScript
{
    /// <summary>
    /// AnimationClip アセット自体の設定を変更するユーティリティ。
    /// （`loop on/off` 指定で使う。クリップのアセットに書き込む点に注意）
    /// </summary>
    internal static class ClipSettingsUtility
    {
        /// <summary>clip の loopTime を設定する。変更があった場合のみ true を返す。</summary>
        public static bool ApplyLoop(AnimationClip clip, bool loop)
        {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (settings.loopTime == loop) return false;

            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return true;
        }
    }
}
