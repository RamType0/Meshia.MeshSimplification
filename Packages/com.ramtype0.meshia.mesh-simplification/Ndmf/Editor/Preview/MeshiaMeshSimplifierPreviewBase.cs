#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using nadena.dev.ndmf.preview;

namespace Meshia.MeshSimplification.Ndmf.Editor.Preview
{
    internal abstract class MeshiaMeshSimplifierPreviewBase<TDerived> : IRenderFilter
        where TDerived : MeshiaMeshSimplifierPreviewBase<TDerived>
    {
        public static readonly Dictionary<Renderer, (int proxy, int simplified)> TriangleCountCache = new();

        public static TogglablePreviewNode? ToggleNode { get; protected set; }

        protected MeshiaMeshSimplifierPreviewBase()
        {
        }

        public IEnumerable<TogglablePreviewNode> GetPreviewControlNodes()
        {
            if (ToggleNode != null)
            {
                yield return ToggleNode;
            }
        }

        public bool IsEnabled(ComputeContext context)
        {
            if (ToggleNode == null)
            {
                return true;
            }
            return context.Observe(ToggleNode.IsEnabled);
        }

        public abstract ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context);

        async Task<IRenderFilterNode> IRenderFilter.Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
        {
            var original = proxyPairs.First().Item1;
            var proxy = proxyPairs.First().Item2;
            var proxyMesh = RendererUtility.GetMesh(proxy);

            var (target, options) = QueryTarget(context, group, original, proxy);

            Mesh simplifiedMesh = new();
            try
            {
                await MeshSimplifier.SimplifyAsync(proxyMesh, target, options, simplifiedMesh);
            }
            catch (Exception)
            {
                UnityEngine.Object.DestroyImmediate(simplifiedMesh);
                throw;
            }

            TriangleCountCache[original] = (proxyMesh.GetTriangleCount(), simplifiedMesh.GetTriangleCount());

            return new NdmfMeshSimplifierPreviewNode(simplifiedMesh);
        }

        protected abstract (MeshSimplificationTarget, MeshSimplifierOptions) QueryTarget(ComputeContext context, RenderGroup group, Renderer original, Renderer proxy);
    }

    internal class NdmfMeshSimplifierPreviewNode : IRenderFilterNode
    {
        public RenderAspects WhatChanged => RenderAspects.Mesh;
        private readonly Mesh _simplifiedMesh;

        public NdmfMeshSimplifierPreviewNode(Mesh mesh)
        {
            _simplifiedMesh = mesh;
        }

        public void OnFrame(Renderer original, Renderer proxy)
        {
            RendererUtility.SetMesh(proxy, _simplifiedMesh);
        }

        void IDisposable.Dispose() => UnityEngine.Object.DestroyImmediate(_simplifiedMesh);
    }
}
