using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace Paltee.AvatarAid.Runtime
{
    [DisallowMultipleComponent]
    [AddComponentMenu("FaceEmoteInstaller")]
    public sealed class FaceEmoteInstaller : MonoBehaviour, IEditorOnly
    {
        [SerializeField]
        public float TransitionSeconds = 0.1f;
        [SerializeField]
        public AnimationClip IdleMotion;

        [SerializeField]
        public List<ExpressionSetDefinition> Definitions;
    }

    [Serializable]
    public class ExpressionSetDefinition
    {
        [SerializeField]
        public AnimationClip Fist;
        [SerializeField]
        public AnimationClip Open;
        [SerializeField]
        public AnimationClip Point;
        [SerializeField]
        public AnimationClip Peace;
        [SerializeField]
        public AnimationClip RockNRoll;
        [SerializeField]
        public AnimationClip Gun;
        [SerializeField]
        public AnimationClip ThumbsUp;
    }
}
