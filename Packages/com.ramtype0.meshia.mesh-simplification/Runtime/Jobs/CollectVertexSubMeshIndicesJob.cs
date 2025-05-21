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

        public NativeList<uint> VertexSubMeshIndices;

        public void Execute()
        {
            VertexSubMeshIndices.Resize(Mesh.vertexCount, NativeArrayOptions.ClearMemory);
            for (int i = 0; i < Mesh.subMeshCount; i++)
            {
                var subMesh = Mesh.GetSubMesh(i);
                uint bit = 1u << i;
                for (int vertexIndex = subMesh.firstVertex; vertexIndex < subMesh.vertexCount; vertexIndex++)
                {
                    ref var vertexSubMeshIndex = ref VertexSubMeshIndices.ElementAt(vertexIndex);
                    vertexSubMeshIndex |= bit;
                }
            }
        }
    }
}