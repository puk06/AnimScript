using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
#if VRC_SDK_VRCSDK3
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
#endif

namespace net.puk06.AnimScript
{
    /// <summary>
    /// ステートに付いている VRC Avatar Parameter Driver を
    /// driver ブロックの行（set / add / random / copy）に変換する。
    ///
    /// VRC SDK3 が無い環境では常に「無し」として扱う。
    /// </summary>
    internal static class DriverExporter
    {
        /// <summary>
        /// driver ブロックの中身の行を返す。ドライバーが付いていなければ null。
        /// </summary>
        public static List<string> Export(AnimatorState state, out bool localOnly, List<string> warnings)
        {
            localOnly = false;

#if VRC_SDK_VRCSDK3
            var driver = state.behaviours.OfType<VRCAvatarParameterDriver>().FirstOrDefault();
            if (driver == null) return null;

            localOnly = driver.localOnly;

            var lines = new List<string>();
            foreach (var parameter in driver.parameters)
            {
                var name = NameSanitizer.SanitizeQuiet(parameter.name);
                switch (parameter.type)
                {
                    case VRC_AvatarParameterDriver.ChangeType.Set:
                        lines.Add($"set {name} {ScriptTextWriter.Format(parameter.value)}");
                        break;
                    case VRC_AvatarParameterDriver.ChangeType.Add:
                        lines.Add($"add {name} {ScriptTextWriter.Format(parameter.value)}");
                        break;
                    case VRC_AvatarParameterDriver.ChangeType.Random:
                        lines.Add($"random {name} {ScriptTextWriter.Format(parameter.valueMin)} {ScriptTextWriter.Format(parameter.valueMax)}");
                        break;
                    case VRC_AvatarParameterDriver.ChangeType.Copy:
                        lines.Add($"copy {NameSanitizer.SanitizeQuiet(parameter.source)} -> {name}");
                        break;
                    default:
                        warnings.Add($"ステート「{state.name}」の driver に未対応の種類（{parameter.type}）があり、スキップしました");
                        break;
                }
            }
            return lines;
#else
            return null;
#endif
        }
    }
}
