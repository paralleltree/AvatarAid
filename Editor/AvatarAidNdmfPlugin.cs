using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using nadena.dev.ndmf;

[assembly: ExportsPlugin(typeof(Paltee.AvatarAid.AvatarAidNdmfPlugin))]

namespace Paltee.AvatarAid
{
    public class AvatarAidNdmfPlugin : Plugin<AvatarAidNdmfPlugin>
    {
        public override string QualifiedName => "dev.paltee.avatar-aid";
        public override string DisplayName => "AvatarAid";

        protected override void Configure()
        {
            InPhase(BuildPhase.Generating).Run("Apply FaceEmote", ctx => new FaceEmoteProcessor().Process(ctx));
            InPhase(BuildPhase.Transforming).Run("Apply DistanceFade", ctx => new LiltoonDistanceFadeProcessor().Process(ctx));
        }
    }
}
