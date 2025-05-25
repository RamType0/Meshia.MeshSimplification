#if ENABLE_NDMF

using Meshia.MeshSimplification.Ndmf.Editor;
using Meshia.MeshSimplification.Ndmf.Editor.Preview;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;


[assembly: ExportsPlugin(typeof(NdmfPlugin))]

namespace Meshia.MeshSimplification.Ndmf.Editor
{
    class NdmfPlugin : Plugin<NdmfPlugin>
    {
        public override string DisplayName => "Meshia NDMF Mesh Simplifier";

        protected override void Configure()
        {
            InPhase(BuildPhase.Optimizing)
                .BeforePlugin("com.anatawa12.avatar-optimizer")
                .Run("Simplify meshes", ctx =>
                {
                    var nfmfMeshSimplifiers = ctx.AvatarRootObject.GetComponentsInChildren<MeshiaMeshSimplifier>(true);
                    using(ListPool<(Mesh Mesh, MeshSimplificationTarget Target, MeshSimplifierOptions Options, Mesh Destination)>.Get(out var parameters))
                    {
                        foreach (var ndmfMeshSimplifier in nfmfMeshSimplifiers)
                        {
                            if (ndmfMeshSimplifier.TryGetComponent<SkinnedMeshRenderer>(out var skinnedMeshRenderer))
                            {
                                var sourceMesh = skinnedMeshRenderer.sharedMesh;
                                Mesh simplifiedMesh = new();
                                parameters.Add((sourceMesh, ndmfMeshSimplifier.target, ndmfMeshSimplifier.options, simplifiedMesh));
                            }
                            if (ndmfMeshSimplifier.TryGetComponent<MeshFilter>(out var meshFilter))
                            {
                                var sourceMesh = meshFilter.sharedMesh;
                                Mesh simplifiedMesh = new();
                                parameters.Add((sourceMesh, ndmfMeshSimplifier.target, ndmfMeshSimplifier.options, simplifiedMesh));
                            }
                            
                        }
                        MeshSimplifier.SimplifyBatch(parameters);
                        {
                            var i = 0;

                            foreach (var ndmfMeshSimplifier in nfmfMeshSimplifiers)
                            {
                                if (ndmfMeshSimplifier.TryGetComponent<SkinnedMeshRenderer>(out var skinnedMeshRenderer))
                                {
                                    var (mesh, target, options, simplifiedMesh) = parameters[i++];
                                    AssetDatabase.AddObjectToAsset(simplifiedMesh, ctx.AssetContainer);
                                    skinnedMeshRenderer.sharedMesh = simplifiedMesh;
                                }
                                if (ndmfMeshSimplifier.TryGetComponent<MeshFilter>(out var meshFilter))
                                {
                                    var (mesh, target, options, simplifiedMesh) = parameters[i++];
                                    AssetDatabase.AddObjectToAsset(simplifiedMesh, ctx.AssetContainer);
                                    meshFilter.sharedMesh = simplifiedMesh;
                                }

                                UnityEngine.Object.DestroyImmediate(ndmfMeshSimplifier);
                            }
                        }
                    }
                }).PreviewingWith(new MeshiaMeshSimplifierPreview())
            ;
        }
    }
}

#endif