#nullable enable

using System;
using UnityEngine;

namespace Meshia.MeshSimplification.Ndmf
{
    public class RendererUtility
    {
        public static Mesh? GetMesh(Renderer renderer)
        {
            switch (renderer)
            {
                case MeshRenderer meshRenderer:
                    var meshFilter = meshRenderer.GetComponent<MeshFilter>();
                    if (meshFilter == null) throw new Exception("MeshFilter is null");
                    var mesh = meshFilter.sharedMesh;
                    if (mesh == null) throw new Exception("Mesh is null");
                    return mesh;
                case SkinnedMeshRenderer skinnedMeshRenderer:
                    var mesh2 = skinnedMeshRenderer.sharedMesh;
                    if (mesh2 == null) throw new Exception("Mesh is null");
                    return mesh2;
                default:
                    throw new ArgumentException("Could not find target property to get mesh.");
            }
        }
        
        public static void SetMesh(Renderer renderer, Mesh mesh)
        {
            switch (renderer)
            {
                case MeshRenderer meshrenderer:
                    var meshfilter = meshrenderer.GetComponent<MeshFilter>();
                    if (meshfilter == null) throw new Exception("MeshFilter is null");
                    meshfilter.sharedMesh = mesh;
                    break;
                case SkinnedMeshRenderer skinnedMeshRenderer:
                    skinnedMeshRenderer.sharedMesh = mesh;
                    break;
                default:
                    throw new ArgumentException("Could not find target property to set mesh.");
            }
        }
    }
}