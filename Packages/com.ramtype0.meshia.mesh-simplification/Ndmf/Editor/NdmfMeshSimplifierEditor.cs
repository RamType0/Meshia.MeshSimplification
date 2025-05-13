#nullable enable
using System.Diagnostics.CodeAnalysis;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Meshia.MeshSimplification.Ndmf.Editor
{
    [CustomEditor(typeof(NdmfMeshSimplifier))]
    [CanEditMultipleObjects]
    public class NdmfMeshSimplifierEditor : UnityEditor.Editor
    {
        [SerializeField]
        VisualTreeAsset visualTreeAsset;
        
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new();
            visualTreeAsset.CloneTree(root);
            root.Bind(serializedObject);

            var ndmfNotImportedWarning = root.Q<HelpBox>("NdmfNotImportedWarning");
            DisplayStyle warningDisplayStyle;
#if ENABLE_NDMF
            warningDisplayStyle = DisplayStyle.None;
#else
            warningDisplayStyle = DisplayStyle.Flex;
#endif
            ndmfNotImportedWarning.style.display = warningDisplayStyle;

            var bakeMeshButtonContainer = root.Q<IMGUIContainer>("BakeMeshButtonContainer");
            bakeMeshButtonContainer.onGUIHandler = () =>
            {
                // TODO: Replace this with non-IMGUI implementation
                // But how could we register callback for whether target mesh is currently available?
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
            };
            
            return root;
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
