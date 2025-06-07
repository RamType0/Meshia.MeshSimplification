#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace Meshia.MeshSimplification.Ndmf.Editor.Preview
{
    internal class MeshiaCascadingMeshSimplifierPreview : MeshiaMeshSimplifierPreviewBase<MeshiaCascadingMeshSimplifierPreview>
    {
        static MeshiaCascadingMeshSimplifierPreview()
        {
            ToggleNode = TogglablePreviewNode.Create(
                () => "MeshiaCascadingMeshSimplifier",
                qualifiedName: "Meshia.MeshSimplification.MeshiaCascadingMeshSimplifier"
            );
        }

        public override ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context)
        {
            var groups = new List<RenderGroup>();
            foreach (var root in context.GetAvatarRoots())
            {
                if (context.ActiveInHierarchy(root) is false) continue;
                foreach (var component in context.GetComponentsInChildren<MeshiaCascadingMeshSimplifier>(root, true))
                {
                    var componentEnabled = context.Observe(component.gameObject, g => g.activeInHierarchy);
                    if (!componentEnabled) continue;

                    var targetCount = context.Observe(component, c => c.Targets.Count());
                    for (int i = 0; i < targetCount; i++)
                    {
                        var index = i;
                        var targetEnabled = context.Observe(component, c => c.Targets[index].IsValid() && c.Targets[index].Enabled());
                        if (!targetEnabled) continue;

                        var renderer = component.Targets[index].Renderer;
                        groups.Add(RenderGroup.For(renderer).WithData<(MeshiaCascadingMeshSimplifier, int)>((component, index)));
                    }
                }
            }
            return groups.ToImmutableList();
        }
        
        protected override (MeshSimplificationTarget, MeshSimplifierOptions) QueryTarget(ComputeContext context, RenderGroup group, Renderer original, Renderer proxy)
        {
            var data = group.GetData<(MeshiaCascadingMeshSimplifier, int)>();
            var component = data.Item1;
            var index = data.Item2;

            var cascadingTarget = context.Observe(component, c => c.Targets[index] with { }, (a, b) => a.Equals(b));
            var target = new MeshSimplificationTarget() { Kind = MeshSimplificationTargetKind.AbsoluteTriangleCount, Value = cascadingTarget.TargetTriangleCount };
            return (target, cascadingTarget.Options);
        }
    }
}