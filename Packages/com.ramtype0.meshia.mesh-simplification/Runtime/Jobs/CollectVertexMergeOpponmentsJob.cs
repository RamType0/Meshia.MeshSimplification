using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
namespace Meshia.MeshSimplification
{
    [BurstCompile]
    struct CollectVertexMergeOpponmentsJob : IJob
    {
        [ReadOnly]
        public NativeArray<VertexMerge> Merges;
        public NativeParallelMultiHashMap<int, int> VertexMergeOpponentVertices;
        public void Execute()
        {
            VertexMergeOpponentVertices.Capacity = Merges.Length * 2;
            for (int index = 0; index < Merges.Length; index++)
            {
                var merge = Merges[index];
                var vertexA = merge.VertexAIndex;
                var vertexB = merge.VertexBIndex;
                VertexMergeOpponentVertices.Add(vertexA, vertexB);
                VertexMergeOpponentVertices.Add(vertexB, vertexA);
            }
        }
    }
}


