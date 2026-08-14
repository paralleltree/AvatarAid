using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace Paltee.AvatarAid.Runtime
{
    [DisallowMultipleComponent]
    [AddComponentMenu("AvatarAid/Compress Shared Int Parameters")]
    public sealed class CompressSharedIntParameters : MonoBehaviour, IEditorOnly
    {
        [Tooltip("処理対象から除外するパラメーター名のプレフィックスのリスト（前方一致）")]
        public List<string> ExcludePrefixes = new List<string>();
    }
}
