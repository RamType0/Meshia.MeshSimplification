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

        public NativeList<uint> VertexContainingSubMeshIndices;

        public void Execute()
        {
            VertexContainingSubMeshIndices.Resize(Mesh.vertexCount, NativeArrayOptions.ClearMemory);
            for (int i = 0; i < Mesh.subMeshCount; i++)
            {
                var subMesh = Mesh.GetSubMesh(i);
                uint bit = 1u << i;
                for (int vertexIndex = subMesh.firstVertex, endIndex = subMesh.firstVertex + subMesh.vertexCount; vertexIndex < endIndex; vertexIndex++)
                {
                    ref var vertexSubMeshIndex = ref VertexContainingSubMeshIndices.ElementAt(vertexIndex);
                    vertexSubMeshIndex |= bit;
                }
            }
        }
    }
}