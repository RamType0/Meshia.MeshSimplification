#nullable enable
using Meshia.MeshSimplification.Ndmf.Editor;
using Meshia.MeshSimplification.Ndmf.Editor.Preview;
using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;
using System;
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
#if ENABLE_MODULAR_AVATAR

            InPhase(BuildPhase.Resolving)
                .Run("Resolve References", ctx =>
                {
                    var cascadingMeshSimplifiers = ctx.AvatarRootObject.GetComponentsInChildren<MeshiaCascadingAvatarMeshSimplifier>(true);
                    foreach (var cascadingMeshSimplifier in cascadingMeshSimplifiers)
                    {
                        cascadingMeshSimplifier.ResolveReferences();
                    }
                });

#endif

            InPhase(BuildPhase.Optimizing)
                .BeforePlugin("com.anatawa12.avatar-optimizer")
                .Run("Simplify meshes", ctx =>
                {
                    var nfmfMeshSimplifiers = ctx.AvatarRootObject.GetComponentsInChildren<MeshiaMeshSimplifier>(true);
#if ENABLE_MODULAR_AVATAR

                    var cascadingMeshSimplifiers = ctx.AvatarRootObject.GetComponentsInChildren<MeshiaCascadingAvatarMeshSimplifier>(true);
#endif

                    using (ListPool<(Mesh Mesh, MeshSimplificationTarget Target, MeshSimplifierOptions Options, Mesh Destination)>.Get(out var parameters))
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
#if ENABLE_MODULAR_AVATAR

                        foreach (var cascadingMeshSimplifier in cascadingMeshSimplifiers)
                        {
                            foreach (var cascadingTarget in cascadingMeshSimplifier.Entries)
                            {
                                if (!cascadingTarget.IsValid(cascadingMeshSimplifier) || !cascadingTarget.Enabled) continue;
                                var mesh = RendererUtility.GetRequiredMesh(cascadingTarget.GetTargetRenderer(cascadingMeshSimplifier)!);
                                var target = new MeshSimplificationTarget() { Kind = MeshSimplificationTargetKind.AbsoluteTriangleCount, Value = cascadingTarget.TargetTriangleCount };
                                parameters.Add((mesh, target, cascadingTarget.Options, mesh));
                            }
                        }

#endif

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

#if ENABLE_MODULAR_AVATAR

                            foreach (var cascadingMeshSimplifier in cascadingMeshSimplifiers)
                            {
                                foreach (var cascadingTarget in cascadingMeshSimplifier.Entries)
                                {
                                    if (!cascadingTarget.IsValid(cascadingMeshSimplifier) || !cascadingTarget.Enabled) continue;
                                    var renderer = cascadingTarget.GetTargetRenderer(cascadingMeshSimplifier)!;
                                    var (mesh, target, options, simplifiedMesh) = parameters[i++];
                                    AssetDatabase.AddObjectToAsset(simplifiedMesh, ctx.AssetContainer);
                                    RendererUtility.SetMesh(renderer, simplifiedMesh);

                                    UnityEngine.Object.DestroyImmediate(cascadingMeshSimplifier);
                                }
                            }

#endif

                        }
                    }
                }).PreviewingWith(new IRenderFilter[]
                {
                    new MeshiaMeshSimplifierPreview(),
#if ENABLE_MODULAR_AVATAR
                    new MeshiaCascadingAvatarMeshSimplifierPreview(),
#endif
                })
            ;
        }
    }
}
