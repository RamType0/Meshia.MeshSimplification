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
                    var overallMeshiaMeshSimplifiers = ctx.AvatarRootObject.GetComponentsInChildren<OverallMeshiaMeshSimplifier>(true);
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
                        foreach (var overallMeshiaMeshSimplifier in overallMeshiaMeshSimplifiers)
                        {
                            foreach (var overallTarget in overallMeshiaMeshSimplifier.Targets)
                            {
                                if (!overallTarget.IsValid() || !overallTarget.Enabled()) continue;
                                var mesh = RendererUtility.GetMesh(overallTarget.Renderer)!;
                                var target = new MeshSimplificationTarget() { Kind = MeshSimplificationTargetKind.AbsoluteTriangleCount, Value = overallTarget.TargetTriangleCount };
                                parameters.Add((mesh, target, overallTarget.Options, mesh));
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
                            foreach (var overallMeshiaMeshSimplifier in overallMeshiaMeshSimplifiers)
                            {
                                foreach (var overallTarget in overallMeshiaMeshSimplifier.Targets)
                                {
                                    if (!overallTarget.IsValid() || !overallTarget.Enabled()) continue;
                                    var renderer = overallTarget.Renderer!;
                                    var (mesh, target, options, simplifiedMesh) = parameters[i++];
                                    AssetDatabase.AddObjectToAsset(simplifiedMesh, ctx.AssetContainer);
                                    RendererUtility.AssignMesh(renderer, simplifiedMesh);

                                    UnityEngine.Object.DestroyImmediate(overallMeshiaMeshSimplifier);
                                }
                            }
                        }
                    }
                }).PreviewingWith(new MeshiaMeshSimplifierPreview(), new OverallMeshiaMeshSimplifierPreview())
            ;
        }
    }
}

#endif