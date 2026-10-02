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
        public sealed class ExportedDriver
        {
            public bool LocalOnly;
            public List<string> Lines;
        }

        public static List<ExportedDriver> ExportAll(AnimatorState state,
            IReadOnlyDictionary<string, string> parameterNameMap, List<string> warnings)
        {
            var result = new List<ExportedDriver>();

#if VRC_SDK_VRCSDK3
            foreach (var driver in state.behaviours.OfType<VRCAvatarParameterDriver>())
            {
                var lines = new List<string>();
                foreach (var parameter in driver.parameters)
                {
                    var name = GetParameterName(parameter.name, parameterNameMap);
                    switch (parameter.type)
                    {
                        case VRC_AvatarParameterDriver.ChangeType.Set: lines.Add($"set {name} {ScriptTextWriter.Format(parameter.value)}"); break;
                        case VRC_AvatarParameterDriver.ChangeType.Add: lines.Add($"add {name} {ScriptTextWriter.Format(parameter.value)}"); break;
                        case VRC_AvatarParameterDriver.ChangeType.Random: lines.Add($"random {name} {ScriptTextWriter.Format(parameter.valueMin)} {ScriptTextWriter.Format(parameter.valueMax)}"); break;
                        case VRC_AvatarParameterDriver.ChangeType.Copy: lines.Add($"copy {GetParameterName(parameter.source, parameterNameMap)} -> {name}"); break;
                        default: warnings.Add($"ステート「{state.name}」の driver に未対応の種類（{parameter.type}）があり、スキップしました"); break;
                    }
                }
                result.Add(new ExportedDriver { LocalOnly = driver.localOnly, Lines = lines });
            }
            return result;
#else
            return result;
#endif
        }

        public static List<string> Export(AnimatorState state, out bool localOnly, List<string> warnings)
        {
            var all = ExportAll(state, new Dictionary<string, string>(), warnings);
            localOnly = all.Count > 0 && all[0].LocalOnly;
            return all.Count > 0 ? all[0].Lines : null;
        }

        static string GetParameterName(string name, IReadOnlyDictionary<string, string> parameterNameMap)
        {
            return parameterNameMap.TryGetValue(name, out var mappedName)
                ? mappedName
                : NameSanitizer.SanitizeQuiet(name);
        }
    }
}
