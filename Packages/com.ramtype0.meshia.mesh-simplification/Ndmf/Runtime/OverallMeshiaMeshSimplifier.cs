#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Meshia.MeshSimplification.Ndmf
{
    public class OverallMeshiaMeshSimplifier : MonoBehaviour
#if ENABLE_VRCHAT_BASE
    , VRC.SDKBase.IEditorOnly
#endif
    {
        public List<OverallMeshiaMeshSimplifierTarget> Targets = new();
        public int TargetTriangleCount = 70000;
        public bool IsAutoAdjust = false;
    }

    [Serializable]
    public record OverallMeshiaMeshSimplifierTarget
    {
        public Renderer Renderer;
        public int TargetTriangleCount;
        public MeshSimplifierOptions Options;
        public OverallMeshiaMeshSimplifierTargetState State;
        public bool Fixed;

        public OverallMeshiaMeshSimplifierTarget(Renderer renderer)
        {
            Renderer = renderer;
            TargetTriangleCount = RendererUtility.GetMesh(renderer)!.triangles.Length / 3;
            Options = MeshSimplifierOptions.Default;
            State = OverallMeshiaMeshSimplifierTargetState.Enabled;
            Fixed = false;
        }

        public static bool IsValidForTarget(Renderer renderer)
        {
            if (renderer == null) return false;
            if (renderer is not SkinnedMeshRenderer and not MeshRenderer) return false;
            var mesh = RendererUtility.GetMesh(renderer);
            if (mesh == null) return false;
            if (mesh.triangles.Length == 0) return false;
            return true;
        }

        public bool IsValid() => IsValidForTarget(Renderer);
        public bool Enabled() => State == OverallMeshiaMeshSimplifierTargetState.Enabled;
    }

    public enum OverallMeshiaMeshSimplifierTargetState
    {
        Enabled,
        Disabled,
        EditorOnly
    }
}
