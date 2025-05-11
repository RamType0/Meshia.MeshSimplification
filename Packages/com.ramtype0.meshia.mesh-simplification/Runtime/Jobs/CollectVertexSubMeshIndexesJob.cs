using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Meshia.MeshSimplification
{
    [BurstCompile]
    struct CollectVertexSubMeshIndexesJob : IJobParallelFor
    {
        [ReadOnly] public Mesh.MeshData Mesh;

        [WriteOnly] public NativeArray<uint> VertexSubMeshIndexes;

        public void Execute(int vertexIndex)
        {
            ref var vertexSubMeshIndex = ref VertexSubMeshIndexes.ElementAt(vertexIndex);
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