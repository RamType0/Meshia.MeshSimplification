#nullable enable

using UnityEngine;

namespace Meshia.MeshSimplification.Ndmf
{
    public class RendererUtility
    {
        public static Mesh? GetMesh(Renderer renderer)
        {
            switch (renderer)
            {
                case MeshRenderer meshrenderer:
                    var meshfilter = meshrenderer.GetComponent<MeshFilter>();
                    return meshfilter == null ? null : meshfilter.sharedMesh;
                case SkinnedMeshRenderer skinnedMeshRenderer:
                    return skinnedMeshRenderer.sharedMesh;
                default:
                    return null;
            }
        } 
        
        public static void SetMesh(Renderer renderer, Mesh mesh)
        {
            switch (renderer)
            {
                case MeshRenderer meshrenderer:
                    var meshfilter = meshrenderer.GetComponent<MeshFilter>();
                    if (meshfilter == null) return;
                    meshfilter.sharedMesh = mesh;
                    break;
                case SkinnedMeshRenderer skinnedMeshRenderer:
                    skinnedMeshRenderer.sharedMesh = mesh;
                    break;
                default:
                    break;
            }
        }
    }
}