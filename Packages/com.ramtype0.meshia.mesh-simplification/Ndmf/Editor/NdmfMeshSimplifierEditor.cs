#nullable enable
using System.Diagnostics.CodeAnalysis;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Meshia.MeshSimplification.Ndmf.Editor
{
    [CustomEditor(typeof(NdmfMeshSimplifier))]
    public class NdmfMeshSimplifierEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            


#if !ENABLE_NDMF
            EditorGUILayout.HelpBox("This component has no effect without NDMF imported to the project.", MessageType.Warning);
#endif
            base.OnInspectorGUI();

            if (targets.Length == 1)
            {
                var ndmfMeshSimplifier = (NdmfMeshSimplifier)target;

                if (TryGetTargetMesh(ndmfMeshSimplifier, out var targetMesh))
                {
                    if (GUILayout.Button("Bake mesh"))
                    {
                        var absolutePath = EditorUtility.SaveFilePanel(
                                    title: "Save baked mesh",
                                    directory: "",
                                    defaultName: $"{targetMesh.name}-Simplified.asset",
                                    extension: "asset");

                        if (!string.IsNullOrEmpty(absolutePath))
                        {
                            Mesh simplifiedMesh = new();

                            MeshSimplifier.Simplify(targetMesh, ndmfMeshSimplifier.target, ndmfMeshSimplifier.options, simplifiedMesh);

                            AssetDatabase.CreateAsset(simplifiedMesh, Path.Join("Assets/", Path.GetRelativePath(Application.dataPath, absolutePath)));
                        }
                    }
                }

            }
        }

        private static bool TryGetTargetMesh(NdmfMeshSimplifier ndmfMeshSimplifier, [NotNullWhen(true)] out Mesh? targetMesh)
        {
            targetMesh = null;
            if (ndmfMeshSimplifier.TryGetComponent<MeshFilter>(out var meshFilter))
            {
                targetMesh = meshFilter.sharedMesh;
                if (targetMesh != null) 
                {
                    return true;
                }
            }
            if (ndmfMeshSimplifier.TryGetComponent<SkinnedMeshRenderer>(out var skinnedMeshRenderer))
            {
                targetMesh = skinnedMeshRenderer.sharedMesh; 
                if (targetMesh != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
