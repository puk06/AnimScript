using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
#if VRC_SDK_VRCSDK3
using VRC.SDK3.Avatars.Components;
#endif

namespace net.puk06.AnimScript
{
    internal static class ControlExporter
    {
        public static List<string> Export(AnimatorState state, List<string> warnings)
        {
            var lines = new List<string>();
#if VRC_SDK_VRCSDK3
            foreach (var control in state.behaviours.OfType<VRCPlayableLayerControl>())
            {
                lines.Add("playableLayerControl {");
                lines.Add($"layer {control.layer}");
                lines.Add($"goalWeight {ScriptTextWriter.Format(control.goalWeight)}");
                lines.Add($"blendDuration {ScriptTextWriter.Format(control.blendDuration)}");
                lines.Add("}");
            }
            foreach (var control in state.behaviours.OfType<VRCAnimatorTrackingControl>())
            {
                lines.Add("trackingControl {");
                lines.Add($"head {control.trackingHead}");
                lines.Add($"leftHand {control.trackingLeftHand}");
                lines.Add($"rightHand {control.trackingRightHand}");
                lines.Add($"hip {control.trackingHip}");
                lines.Add($"leftFoot {control.trackingLeftFoot}");
                lines.Add($"rightFoot {control.trackingRightFoot}");
                lines.Add($"leftFingers {control.trackingLeftFingers}");
                lines.Add($"rightFingers {control.trackingRightFingers}");
                lines.Add($"eyes {control.trackingEyes}");
                lines.Add($"mouth {control.trackingMouth}");
                lines.Add("}");
            }
#endif
            return lines;
        }
    }
}
