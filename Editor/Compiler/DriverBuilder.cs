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

        public static void Build(VirtualState state, PlayableLayerControlAst block, DiagnosticBag diagnostics)
        {
#if VRC_SDK_VRCSDK3
            if (!System.Enum.TryParse<VRCPlayableLayerControl.BlendableLayer>(block.Layer, out var layer))
            {
                diagnostics.Error(block.Location, $"不明なPlayable Layer「{block.Layer}」です（Action / FX / Gesture / Additive）");
                return;
            }
            var control = ScriptableObject.CreateInstance<VRCPlayableLayerControl>();
            control.name = "VRCPlayableLayerControl";
            control.layer = layer;
            control.goalWeight = block.GoalWeight == null ? 1f : (float)block.GoalWeight.Const();
            control.blendDuration = block.BlendDuration == null ? 0f : (float)block.BlendDuration.Const();
            control.debugString = "AnimScriptBuilder: playableLayerControl";
            state.Behaviours = state.Behaviours.Add(control);
#else
            diagnostics.Warning(block.Location, "VRC SDK (Avatar 3.0) が見つからないため、playableLayerControl ブロックはスキップされました");
#endif
        }

        public static void Build(VirtualState state, AnimatorTrackingControlAst block, DiagnosticBag diagnostics)
        {
#if VRC_SDK_VRCSDK3
            var control = ScriptableObject.CreateInstance<VRCAnimatorTrackingControl>();
            control.name = "VRCAnimatorTrackingControl";
            foreach (var setting in block.Settings)
            {
                if (!System.Enum.TryParse<VRCAnimatorTrackingControl.TrackingType>(setting.Value, out var value))
                {
                    diagnostics.Error(block.Location, $"不明なTrackingType「{setting.Value}」です（NoChange / Tracking / Animation）");
                    continue;
                }
                switch (setting.Key)
                {
                    case "head": control.trackingHead = value; break;
                    case "leftHand": control.trackingLeftHand = value; break;
                    case "rightHand": control.trackingRightHand = value; break;
                    case "hip": control.trackingHip = value; break;
                    case "leftFoot": control.trackingLeftFoot = value; break;
                    case "rightFoot": control.trackingRightFoot = value; break;
                    case "leftFingers": control.trackingLeftFingers = value; break;
                    case "rightFingers": control.trackingRightFingers = value; break;
                    case "eyes": control.trackingEyes = value; break;
                    case "mouth": control.trackingMouth = value; break;
                    default: diagnostics.Error(block.Location, $"不明なtrackingControl項目「{setting.Key}」です"); break;
                }
            }
            control.debugString = "AnimScriptBuilder: trackingControl";
            state.Behaviours = state.Behaviours.Add(control);
#else
            diagnostics.Warning(block.Location, "VRC SDK (Avatar 3.0) が見つからないため、trackingControl ブロックはスキップされました");
#endif
        }

        public static void Build(VirtualState state, AnimatorLayerControlAst block, DiagnosticBag diagnostics)
        {
#if VRC_SDK_VRCSDK3
            if (!System.Enum.TryParse<VRC_AnimatorLayerControl.BlendableLayer>(block.Playable, out var playable))
            {
                diagnostics.Error(block.Location, $"不明なAnimator Layer「{block.Playable}」です（Action / FX / Gesture / Additive）");
                return;
            }
            var control = ScriptableObject.CreateInstance<VRCAnimatorLayerControl>();
            control.name = "VRCAnimatorLayerControl";
            control.playable = playable;
            control.layer = block.Layer == null ? 1 : (int)block.Layer.Const();
            control.goalWeight = block.GoalWeight == null ? 1f : (float)block.GoalWeight.Const();
            control.blendDuration = block.BlendDuration == null ? 0f : (float)block.BlendDuration.Const();
            control.debugString = "AnimScriptBuilder: animatorLayerControl";
            state.Behaviours = state.Behaviours.Add(control);
#else
            diagnostics.Warning(block.Location, "VRC SDK (Avatar 3.0) が見つからないため、animatorLayerControl ブロックはスキップされました");
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
