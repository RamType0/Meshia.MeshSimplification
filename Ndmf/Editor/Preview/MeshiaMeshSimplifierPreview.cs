#nullable enable

using System.Collections;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace Meshia.MeshSimplification.Ndmf.Editor.Preview
{
    internal class MeshiaMeshSimplifierPreview : MeshiaMeshSimplifierPreviewBase<MeshiaMeshSimplifierPreview>
    {
        public override ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context)
        {
            return context.GetComponentsByType<MeshiaMeshSimplifier>()
            .Select(ndmfMeshSimplifier => context.GetComponent<Renderer>(ndmfMeshSimplifier.gameObject))
            .Where(renderer => renderer is MeshRenderer or SkinnedMeshRenderer)
            .Where(renderer => context.ActiveInHierarchy(renderer.gameObject))
            .Select(renderer => RenderGroup.For(renderer))
            .ToImmutableList();
        }
        protected override (MeshSimplificationTarget, MeshSimplifierOptions, BitArray?) QueryTarget(ComputeContext context, RenderGroup group, Renderer original, Renderer proxy)
        {
            var ndmfMeshSimplifier = original.GetComponent<MeshiaMeshSimplifier>();
            var target = context.Observe(ndmfMeshSimplifier, ndmfMeshSimplifier => ndmfMeshSimplifier.target, (x, y) => x == y);
            var options = context.Observe(ndmfMeshSimplifier, ndmfMeshSimplifier => ndmfMeshSimplifier.options, (x, y) => x == y);
            return (target, options, null);
        }
    }
}