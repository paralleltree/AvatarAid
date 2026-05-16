using System;
using UnityEngine;
using VRC.SDKBase;

namespace Paltee.AvatarAid.Runtime
{
    [DisallowMultipleComponent]
    [AddComponentMenu("TriggerGestureEmoteInstaller")]
    public sealed class TriggerGestureEmoteInstaller : MonoBehaviour, IEditorOnly
    {
        [SerializeField]
        public AnimationClip Animation;
        [SerializeField]
        public string TargetName;
        [SerializeField]
        public float TransitionSeconds = 0.1f;
        [SerializeField]
        public bool IsAdditive;
    }
}
