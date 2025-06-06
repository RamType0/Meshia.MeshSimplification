#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Meshia.MeshSimplification.Ndmf
{
    public class MeshiaCascadingMeshSimplifier : MonoBehaviour
#if ENABLE_VRCHAT_BASE
    , VRC.SDKBase.IEditorOnly
#endif
    {
        public List<MeshiaCascadingMeshSimplifierTarget> Targets = new();
        public int TargetTriangleCount = 70000;
        public bool IsAutoAdjust = false;
    }

    [Serializable]
    public record MeshiaCascadingMeshSimplifierTarget
    {
        public Renderer Renderer;
        public int TargetTriangleCount;
        public MeshSimplifierOptions Options;
        public MeshiaCascadingMeshSimplifierTargetKind State;
        public bool Fixed;

        public MeshiaCascadingMeshSimplifierTarget(Renderer renderer)
        {
            Renderer = renderer;
            TargetTriangleCount = RendererUtility.GetMesh(renderer).GetTriangleCount();
            Options = MeshSimplifierOptions.Default;
            State = MeshiaCascadingMeshSimplifierTargetKind.Enabled;
            Fixed = false;
        }

        public static bool IsValidTarget(Renderer renderer)
        {
            if (renderer == null) return false;
            if (renderer is not SkinnedMeshRenderer and not MeshRenderer) return false;
            var mesh = RendererUtility.GetMesh(renderer);
            if (mesh.GetTriangleCount() == 0) return false;
            return true;
        }

        public bool IsValid() => IsValidTarget(Renderer);
        public bool Enabled() => State == MeshiaCascadingMeshSimplifierTargetKind.Enabled;
    }

    public enum MeshiaCascadingMeshSimplifierTargetKind
    {
        Enabled,
        Disabled,
        EditorOnly
    }
}
