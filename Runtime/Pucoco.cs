#nullable enable
using System;
using nadena.dev.modular_avatar.core;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace net.puk06.AnimScript
{
    [Serializable]
    [AddComponentMenu("Pucoco! - Non-Destructive AnimScript Builder")]
    public class Pucoco : MonoBehaviour
    {
        public UnityEngine.Object? ScriptFile;
        public VRCAvatarDescriptor.AnimLayerType LayerType = VRCAvatarDescriptor.AnimLayerType.FX;
        public MergeAnimatorPathMode pathMode = MergeAnimatorPathMode.Relative;
        public AvatarObjectReference relativePathRoot = new AvatarObjectReference();
        public int LayerPriority = 0;
        public MergeAnimatorMode MergeAnimatorMode = MergeAnimatorMode.Append;
        public bool MatchAvatarWriteDefaults = true;
    }
}
