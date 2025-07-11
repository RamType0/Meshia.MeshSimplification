#nullable enable

#if ENABLE_MODULAR_AVATAR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using nadena.dev.ndmf.runtime;
using nadena.dev.modular_avatar.core;
using UnityEngine.Pool;
using System.Diagnostics.CodeAnalysis;

namespace Meshia.MeshSimplification.Ndmf
{
    [AddComponentMenu("Meshia Mesh Simplification/Meshia Cascading Avatar Mesh Simplifier")]
    public class MeshiaCascadingAvatarMeshSimplifier : MonoBehaviour
#if ENABLE_VRCHAT_BASE
    , VRC.SDKBase.IEditorOnly
#endif
    {
        public List<MeshiaCascadingAvatarMeshSimplifierRendererEntry> Entries = new();
        public int TargetTriangleCount = 70000;
        public bool AutoAdjustEnabled = true;

        public void RefreshEntries()
        {
            using (ListPool<Renderer>.Get(out var ownedRenderers))
            {
                GetOwnedRenderers(ownedRenderers);
                var currentEntries = Entries.Select(t => t.GetTargetRenderer(this));
                var addedEntries = ownedRenderers.Except(currentEntries).Where(MeshiaCascadingAvatarMeshSimplifierRendererEntry.IsValidTarget).Select(renderer => new MeshiaCascadingAvatarMeshSimplifierRendererEntry(renderer!)).ToArray();

                Entries.AddRange(addedEntries);
            }

            
        }

        private void GetOwnedRenderers(List<Renderer> ownedRenderers)
        {
            var myScopeOrigin = transform.parent;

            if(myScopeOrigin == null)
            {
                throw new InvalidOperationException($"{nameof(MeshiaCascadingAvatarMeshSimplifier)} should not be attached to root GameObject.");
            }
            using (ListPool<MeshiaCascadingAvatarMeshSimplifier>.Get(out var childSimplifiers))
            using (HashSetPool<Transform>.Get(out var otherScopeOrigins))
            {
                myScopeOrigin.gameObject.GetComponentsInChildren(childSimplifiers);
                foreach (var childSimplifier in childSimplifiers)
                {
                    if (childSimplifier != this)
                    {
                        var otherScopeOrigin = childSimplifier.transform.parent;
                        if(otherScopeOrigin == myScopeOrigin)
                        {
                            throw new InvalidOperationException($"Multiple {nameof(MeshiaCascadingAvatarMeshSimplifier)} is attached to direct children of GameObject. This is not allowed.");
                        }
                        otherScopeOrigins.Add(otherScopeOrigin);

                    }
                }

                using (ListPool<Renderer>.Get(out var childRenderers))
                {
                    myScopeOrigin.gameObject.GetComponentsInChildren(childRenderers);
                    for (int i = 0; i < childRenderers.Count;)
                    {
                        Renderer? childRenderer = childRenderers[i];

                        if (childRenderer is MeshRenderer or SkinnedMeshRenderer)
                        {
                            i++;
                        }
                        else
                        {
                            childRenderers[i] = childRenderers[^1];
                            childRenderers.RemoveAt(childRenderers.Count - 1);
                        }
                    }
                    if (otherScopeOrigins.Count != 0)
                    {
                        for (int i = 0; i < childRenderers.Count;)
                        {
                            Renderer? childRenderer = childRenderers[i];
                            if (IsOwnedByThis(childRenderer))
                            {
                                i++;
                            }
                            else
                            {
                                childRenderers[i] = childRenderers[^1];
                                childRenderers.RemoveAt(childRenderers.Count - 1);
                            }
                            bool IsOwnedByThis(Renderer childRenderer)
                            {
                                var currentTransform = childRenderer.transform;
                                while (currentTransform != myScopeOrigin)
                                {
                                    if (otherScopeOrigins.Contains(currentTransform))
                                    {
                                        return false;
                                    }
                                    else
                                    {
                                        currentTransform = currentTransform.parent;
                                    }

                                }
                                return true;
                            }
                        }
                    }
                    ownedRenderers.AddRange(childRenderers);
                }
            }

            
        }

        public void ResolveReferences()
        {
            foreach (var target in Entries)
            {
                target.ResolveReference(this);
            }
        }
    }

    [Serializable]
    public record MeshiaCascadingAvatarMeshSimplifierRendererEntry
    {
        public AvatarObjectReference RendererObjectReference;
        public int TargetTriangleCount;
        public MeshSimplifierOptions Options;
        public bool Enabled;
        public bool Fixed;

        public MeshiaCascadingAvatarMeshSimplifierRendererEntry(Renderer renderer)
        {
            RendererObjectReference = new AvatarObjectReference();
            RendererObjectReference.Set(renderer.gameObject);
            TargetTriangleCount = RendererUtility.GetMesh(renderer)?.GetTriangleCount() ?? 0;
            Options = MeshSimplifierOptions.Default;
            Enabled = true;
            Fixed = false;
        }

        internal static bool IsValidTarget([NotNullWhen(true)] Renderer? renderer)
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
            return obj.TryGetComponent<Renderer>(out var renderer) && renderer is (MeshRenderer or SkinnedMeshRenderer) ? renderer : null;
        }

        internal bool IsValid(MeshiaCascadingAvatarMeshSimplifier container) => IsValidTarget(GetTargetRenderer(container));

        internal static bool IsEditorOnlyInHierarchy(GameObject gameObject)
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