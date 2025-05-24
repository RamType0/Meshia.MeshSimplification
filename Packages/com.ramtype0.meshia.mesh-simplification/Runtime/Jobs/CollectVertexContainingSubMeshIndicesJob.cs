using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Meshia.MeshSimplification
{
    [BurstCompile]
    struct CollectVertexContainingSubMeshIndicesJob : IJob
    {
        [ReadOnly] public Mesh.MeshData Mesh;

        public NativeList<uint> VertexContainingSubMeshIndices;

        public void Execute()
        {
            VertexContainingSubMeshIndices.Resize(Mesh.vertexCount, NativeArrayOptions.ClearMemory);
            for (int subMeshIndex = 0; subMeshIndex < Mesh.subMeshCount; subMeshIndex++)
            {
                ProcessSubMesh(subMeshIndex);
            }
        }

        private void ProcessSubMesh(int subMeshIndex)
        {
            var subMesh = Mesh.GetSubMesh(subMeshIndex);
            uint bit = 1u << subMeshIndex;
            for (int vertexIndex = subMesh.firstVertex, endIndex = subMesh.firstVertex + subMesh.vertexCount; vertexIndex < endIndex; vertexIndex++)
            {
#if UNITY_BURST_EXPERIMENTAL_LOOP_INTRINSICS
                Loop.ExpectVectorized();
#endif
                ref var vertexSubMeshIndex = ref VertexContainingSubMeshIndices.ElementAt(vertexIndex);
                vertexSubMeshIndex |= bit;
            }
        }
    }
}