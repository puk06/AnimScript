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
        [Header("ビルドするスクリプト")]
        public UnityEngine.Object? ScriptFile;

        [Header("レイヤー種別")]
        public VRCAvatarDescriptor.AnimLayerType LayerType = VRCAvatarDescriptor.AnimLayerType.FX;

        [Header("レイヤーの優先度")]
        public int LayerPriority = 0;

        [Header("統合モード")]
        public MergeAnimatorMode MergeAnimatorMode = MergeAnimatorMode.Append;

        [Header("Write Defaults の設定をアバターに合わせる")]
        public bool MatchAvatarWriteDefaults = true;
    }
}
