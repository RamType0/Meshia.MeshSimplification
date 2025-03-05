#if ENABLE_NDMF

using Meshia.MeshSimplification.Ndmf.Editor;
using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;


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
                    var nfmfMeshSimplifiers = ctx.AvatarRootObject.GetComponentsInChildren<NdmfMeshSimplifier>(true);
                    foreach (var ndmfMeshSimplifier in nfmfMeshSimplifiers)
                    {
                        if (ndmfMeshSimplifier.TryGetComponent<SkinnedMeshRenderer>(out var skinnedMeshRenderer))
                        {

                            var sourceMesh = skinnedMeshRenderer.sharedMesh;
                            Mesh simplifiedMesh = new();
                            MeshSimplifier.Simplify(sourceMesh, ndmfMeshSimplifier.target, ndmfMeshSimplifier.options, simplifiedMesh);
                            AssetDatabase.AddObjectToAsset(simplifiedMesh, ctx.AssetContainer);
                            skinnedMeshRenderer.sharedMesh = simplifiedMesh;
                        }
                        if (ndmfMeshSimplifier.TryGetComponent<MeshFilter>(out var meshFilter))
                        {
                            var sourceMesh = meshFilter.sharedMesh;
                            Mesh simplifiedMesh = new();
                            MeshSimplifier.Simplify(sourceMesh, ndmfMeshSimplifier.target, ndmfMeshSimplifier.options, simplifiedMesh);
                            AssetDatabase.AddObjectToAsset(simplifiedMesh, ctx.AssetContainer);
                            meshFilter.sharedMesh = simplifiedMesh;
                        }
                        Object.DestroyImmediate(ndmfMeshSimplifier);
                    }
                }).PreviewingWith(new NdmfMeshSimplifierPreviewer())
            ;
        }
    }
    class NdmfMeshSimplifierPreviewer : IRenderFilter
    {
        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context)
        {
            return context.GetComponentsByType<NdmfMeshSimplifier>()
            .Select(ndmfMeshSimplifier => context.GetComponent<Renderer>(ndmfMeshSimplifier.gameObject))
            .Where(renderer => renderer is MeshRenderer or SkinnedMeshRenderer)
            .Select(renderer => RenderGroup.For(renderer))
            .ToImmutableList();
        }

        public async Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
        {
            var ndmfMeshSimplifier = group.Renderers.First().GetComponent<NdmfMeshSimplifier>();
            var targetRenderer = proxyPairs.First().Item2;
            var mesh = targetRenderer switch
            {
                SkinnedMeshRenderer skinnedMeshRenderer => skinnedMeshRenderer.sharedMesh,
                MeshRenderer meshRenderer => meshRenderer.TryGetComponent<MeshFilter>(out var meshFilter) ? meshFilter.sharedMesh : null,
                _ => null,
            };

            if (mesh == null)
            {
                return null;
            }
            context.Observe(ndmfMeshSimplifier);
            context.Observe(mesh);
            CancellationTokenSource cts = new();
            context.InvokeOnInvalidate(cts, cts => cts.Cancel());

            Mesh simplifiedMesh = new();
            try
            {
                await MeshSimplifier.SimplifyAsync(mesh, ndmfMeshSimplifier.target, ndmfMeshSimplifier.options, simplifiedMesh, cts.Token);
            }
            catch (System.Exception)
            {
                Object.DestroyImmediate(simplifiedMesh);
                throw;
            }
            return new NdmfMeshSimplifierPreviewNode(simplifiedMesh);
        }


    }
    class NdmfMeshSimplifierPreviewNode : IRenderFilterNode
    {
        public RenderAspects WhatChanged => RenderAspects.Mesh;

        Mesh simplifiedMesh;

        public NdmfMeshSimplifierPreviewNode(Mesh mesh)
        {
            simplifiedMesh = mesh;
        }

        public void OnFrame(Renderer original, Renderer proxy)
        {
            switch (proxy)
            {
                case SkinnedMeshRenderer skinnedMeshRenderer:
                    {
                        skinnedMeshRenderer.sharedMesh = simplifiedMesh;
                    }
                    break;
                case MeshRenderer meshRenderer:
                    {
                        if (meshRenderer.TryGetComponent<MeshFilter>(out var meshFilter))
                        {
                            meshFilter.sharedMesh = simplifiedMesh;
                        }
                    }
                    break;
            }
        }

        void System.IDisposable.Dispose() => Object.DestroyImmediate(simplifiedMesh);

    }
}

#endif