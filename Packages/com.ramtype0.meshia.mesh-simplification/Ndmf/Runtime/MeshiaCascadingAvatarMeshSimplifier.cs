#nullable enable

#if ENABLE_MODULAR_AVATAR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using nadena.dev.ndmf.runtime;
using nadena.dev.modular_avatar.core;

namespace Meshia.MeshSimplification.Ndmf
{
    public class MeshiaCascadingAvatarMeshSimplifier : MonoBehaviour
#if ENABLE_VRCHAT_BASE
    , VRC.SDKBase.IEditorOnly
#endif
    {
        public List<MeshiaCascadingAvatarMeshSimplifierTarget> Targets = new();
        public int TargetTriangleCount = 70000;
        public bool AutoAdjustEnabled = false;

        internal void AddTargets()
        {
            var collectedRenderers = CollectOwnedRenderers();

            var existingTargets = Targets.Select(t => t.GetTargetRenderer(this)).Where(r => r != null).ToHashSet();
            foreach (var target in collectedRenderers)
            {
                if (existingTargets.Contains(target)) continue;
                if (!MeshiaCascadingAvatarMeshSimplifierTarget.IsValidTarget(target)) continue;
                Targets.Add(new MeshiaCascadingAvatarMeshSimplifierTarget(target));
            }
        }

        internal Dictionary<int, MeshiaCascadingAvatarMeshSimplifierTarget> GetValidTargets()
        {
            var validTargets = new Dictionary<int, MeshiaCascadingAvatarMeshSimplifierTarget>();
            for (int i = 0; i < Targets.Count; i++)
            {
                var target = Targets[i];
                if (target.IsValid(this))
                {
                    var renderer = target.GetTargetRenderer(this);
                    if (renderer == null) continue;
                    validTargets.Add(i, target);
                }
            }
            return validTargets;
        }

        internal void RemoveInvalidTargets()
        {
            Targets.RemoveAll(t => !t.IsValid(this));
        }

        internal List<Renderer> CollectOwnedRenderers()
        {
            var avatarRoot = RuntimeUtil.FindAvatarInParents(transform);
            if (avatarRoot == null) return new List<Renderer>();

            var collectedRenderers = new List<Renderer>();
            
            // このコンポーネントがある階層の親から開始し、初期所有者をthisに設定
            var startTransform = transform.parent ?? avatarRoot;
            CollectOwnedRenderersRecursively(startTransform, this, collectedRenderers);
            
            return collectedRenderers;
        }

        private void CollectOwnedRenderersRecursively(Transform currentTransform, MeshiaCascadingAvatarMeshSimplifier? currentOwner, List<Renderer> collectedRenderers)
        {
            var effectiveOwner = currentTransform.TryGetComponent<MeshiaCascadingAvatarMeshSimplifier>(out var ownerOnSelf) ? ownerOnSelf : currentOwner;

            if (effectiveOwner == this)
            {
                if (currentTransform.TryGetComponent<Renderer>(out var renderer))
                {
                    collectedRenderers.Add(renderer);
                }
            }

            foreach (Transform child in currentTransform)
            {
                CollectOwnedRenderersRecursively(child, effectiveOwner, collectedRenderers);
            }
        }

        internal void ResolveReferences()
        {
            foreach (var target in Targets)
            {
                target.ResolveReference(this);
            }
        }
    }

    [Serializable]
    public record MeshiaCascadingAvatarMeshSimplifierTarget
    {
        public AvatarObjectReference RendererObjectReference;
        public int TargetTriangleCount;
        public MeshSimplifierOptions Options;
        public bool Enabled;
        public bool Fixed;

        public MeshiaCascadingAvatarMeshSimplifierTarget(Renderer renderer)
        {
            RendererObjectReference = new AvatarObjectReference();
            RendererObjectReference.Set(renderer.gameObject);
            TargetTriangleCount = RendererUtility.GetMesh(renderer)?.GetTriangleCount() ?? 0;
            Options = MeshSimplifierOptions.Default;
            Enabled = true;
            Fixed = false;
        }

        internal static bool IsValidTarget(Renderer? renderer)
        {
            if (renderer == null) return false;
            if (IsEditorOnlyInHierarchy(renderer.gameObject)) return false;
            if (renderer is not SkinnedMeshRenderer and not MeshRenderer) return false;
            var mesh = RendererUtility.GetMesh(renderer);
            if (mesh == null || mesh.GetTriangleCount() == 0) return false;
            return true;
        }

        internal Renderer? GetTargetRenderer(Component container)
        {
            var obj = RendererObjectReference.Get(container);
            if (obj == null) return null;
            var renderer = obj.GetComponent<Renderer>();
            if (renderer == null) return null;
            return renderer;
        }

        internal bool IsValid(MeshiaCascadingAvatarMeshSimplifier container) => IsValidTarget(GetTargetRenderer(container));

        private static bool IsEditorOnlyInHierarchy(GameObject gameObject)
        {
            if (gameObject == null) return false;
            Transform current = gameObject.transform;
            while (current != null)
            {
                if (current.CompareTag("EditorOnly"))
                {
                    return true;
                }
                current = current.parent;
            }
            return false;
        }

        internal void ResolveReference(Component container)
        {
            RendererObjectReference.Get(container);
        }
    }
}

#endif