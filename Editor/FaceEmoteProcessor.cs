using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using nadena.dev.ndmf;
using nadena.dev.modular_avatar.core;

namespace Paltee.AvatarAid
{
    public class FaceEmoteProcessor
    {
        public void Process(BuildContext context)
        {
            var installerComponent = context.AvatarRootObject.GetComponent<Runtime.FaceEmoteInstaller>();
            if (installerComponent == null) return;

            Apply(context, installerComponent);

            UnityEngine.Object.DestroyImmediate(installerComponent);
        }

        protected void Apply(BuildContext buildContext, Runtime.FaceEmoteInstaller installer)
        {
            var targetGameObject = new GameObject("BuiltFaceEmote");
            targetGameObject.transform.parent = buildContext.AvatarRootObject.transform;

            ApplyMergeAnimator(installer, targetGameObject);
        }

        protected void ApplyMergeAnimator(Runtime.FaceEmoteInstaller installer, GameObject target)
        {
            var mergeAnimator = target.AddComponent<ModularAvatarMergeAnimator>();
            mergeAnimator.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
        }
    }
}
