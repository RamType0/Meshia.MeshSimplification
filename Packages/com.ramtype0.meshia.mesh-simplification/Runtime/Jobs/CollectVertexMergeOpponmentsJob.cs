using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
namespace Meshia.MeshSimplification
{
    [BurstCompile]
    struct CollectVertexMergeOpponmentsJob : IJob
    {
        [ReadOnly]
        public NativeArray<VertexMerge> VertexMerges;
        public NativeParallelMultiHashMap<int, int> VertexMergeOpponentVertices;
        public void Execute()
        {
            VertexMergeOpponentVertices.Clear();
            VertexMergeOpponentVertices.Capacity = VertexMerges.Length * 2;
            for (int index = 0; index < VertexMerges.Length; index++)
            {
                var merge = VertexMerges[index];
                var vertexA = merge.VertexAIndex;
                var vertexB = merge.VertexBIndex;
                VertexMergeOpponentVertices.Add(vertexA, vertexB);
                VertexMergeOpponentVertices.Add(vertexB, vertexA);
            }
        }
    }
}


