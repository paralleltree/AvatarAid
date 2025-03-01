using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using nadena.dev.ndmf;

namespace Paltee.AvatarAid
{
    public class LiltoonDistanceFadeProcessor
    {
        protected readonly string DistanceFadeParamName = "_DistanceFade";
        public void Process(BuildContext context)
        {
            var installerComponent = context.AvatarRootObject.GetComponent<Runtime.ApplyLiltoonDistanceFade>();
            if (installerComponent == null) return;

            Apply(context, installerComponent);

            UnityEngine.Object.DestroyImmediate(installerComponent);
        }

        protected void Apply(BuildContext context, Runtime.ApplyLiltoonDistanceFade installer)
        {
            ProcessRecursively(context.AvatarRootObject.transform, installer);
        }

        protected void ProcessRecursively(Transform parent, Runtime.ApplyLiltoonDistanceFade installer)
        {
            var children = parent.GetComponentsInChildren<Transform>(true);
            foreach (var child in children.Skip(1))
            {
                ProcessRecursively(child, installer);
            }

            ApplyDistanceFade(parent.gameObject, installer);
        }

        protected void ApplyDistanceFade(GameObject gameObject, Runtime.ApplyLiltoonDistanceFade installer)
        {
            var meshRenderer = gameObject.GetComponent<Renderer>();
            if (meshRenderer == null) return;

            var newMats = new Material[meshRenderer.sharedMaterials.Length];
            for (int i = 0; i < meshRenderer.sharedMaterials.Length; i++)
            {
                if (meshRenderer.sharedMaterials[i] == null)
                {
                    continue;
                }
                var newMat = new Material(meshRenderer.sharedMaterials[i]);
                ApplyDistanceFadeToMaterial(newMat, installer);
                newMats[i] = newMat;
            }

            meshRenderer.sharedMaterials = newMats;
        }

        protected void ApplyDistanceFadeToMaterial(Material mat, Runtime.ApplyLiltoonDistanceFade installer)
        {
            var names = mat.GetPropertyNames(MaterialPropertyType.Vector);
            if (!names.Contains(DistanceFadeParamName))
            {
                Debug.LogWarning($"_DistanceFade parameter not found: {mat.name}");
            }
            var color = mat.GetColor(DistanceFadeParamName);
            // R: 開始距離 [0-1]
            // G: 終了距離 [0-1]
            // B: 強度 [0-1]
            color.b = 1;
            mat.SetColor(DistanceFadeParamName, color);
        }
    }
}
