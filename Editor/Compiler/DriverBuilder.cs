using nadena.dev.ndmf.animator;
using UnityEngine;
#if VRC_SDK_VRCSDK3
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
#endif

namespace net.puk06.AnimScript
{
    /// <summary>
    /// DriverBlockAst → VRC Avatar Parameter Driver (StateMachineBehaviour) の生成。
    ///
    /// VRC SDK3 (Avatar 3.0) がある環境でのみ有効。
    /// 無い環境では警告を出してスキップする。
    /// </summary>
    internal static class DriverBuilder
    {
        public static void Build(VirtualState state, DriverBlockAst block, DiagnosticBag diagnostics)
        {
#if VRC_SDK_VRCSDK3
            var driver = ScriptableObject.CreateInstance<VRCAvatarParameterDriver>();
            driver.name = "VRCAvatarParameterDriver";
            driver.localOnly = block.LocalOnly;
            driver.debugString = $"AnimScriptBuilder: {block.Actions.Count} action(s)";

            foreach (var action in block.Actions)
                driver.parameters.Add(BuildParameter(action));

            state.Behaviours = state.Behaviours.Add(driver);
#else
            diagnostics.Warning(block.Location,
                "VRC SDK (Avatar 3.0) が見つからないため、driver ブロックはスキップされました");
#endif
        }

#if VRC_SDK_VRCSDK3
        static VRC_AvatarParameterDriver.Parameter BuildParameter(DriverActionAst action)
        {
            var parameter = new VRC_AvatarParameterDriver.Parameter();

            switch (action.Kind)
            {
                case DriverActionKind.Set:
                    parameter.type = VRC_AvatarParameterDriver.ChangeType.Set;
                    parameter.name = action.Target;
                    parameter.value = (float)action.Value.Const();
                    break;
                case DriverActionKind.Add:
                    parameter.type = VRC_AvatarParameterDriver.ChangeType.Add;
                    parameter.name = action.Target;
                    parameter.value = (float)action.Value.Const();
                    break;
                case DriverActionKind.Random:
                    parameter.type = VRC_AvatarParameterDriver.ChangeType.Random;
                    parameter.name = action.Target;
                    parameter.valueMin = (float)action.ValueMin.Const();
                    parameter.valueMax = (float)action.ValueMax.Const();
                    break;
                case DriverActionKind.Copy:
                    parameter.type = VRC_AvatarParameterDriver.ChangeType.Copy;
                    parameter.source = action.Source;
                    parameter.name = action.Target;
                    break;
            }

            return parameter;
        }
#endif
    }
}
