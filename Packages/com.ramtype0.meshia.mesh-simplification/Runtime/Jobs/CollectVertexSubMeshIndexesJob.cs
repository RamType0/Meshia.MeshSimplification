using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Meshia.MeshSimplification
{
    [BurstCompile]
    struct CollectVertexSubMeshIndicesJob : IJob
    {
        [ReadOnly] public Mesh.MeshData Mesh;

        public NativeArray<uint> VertexSubMeshIndices;

        public void Execute()
        {
            for (int vertexIndex = 0; vertexIndex < Mesh.vertexCount; vertexIndex++)
            {
                ref var vertexSubMeshIndex = ref VertexSubMeshIndices.ElementAt(vertexIndex);
                vertexSubMeshIndex = 0u;
                for (int i = 0; i < Mesh.subMeshCount; i++)
                {
                    var subMesh = Mesh.GetSubMesh(i);
                    if (subMesh.firstVertex <= vertexIndex && vertexIndex < subMesh.firstVertex + subMesh.vertexCount)
                    {
                        vertexSubMeshIndex |= 1u << i;
                    }
                }
            }
        }
    }
}