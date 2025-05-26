using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
namespace Meshia.MeshSimplification
{
    [BurstCompile]
    unsafe struct CollectVertexMergeOpponmentsJob : IJob
    {
        [ReadOnly]
        public NativeArray<VertexMerge> UnorderedVertexMerges;
        public NativeParallelMultiHashMap<int, int> VertexMergeOpponentVertices;
        public void Execute()
        {
            VertexMergeOpponentVertices.Clear();
            VertexMergeOpponentVertices.Capacity = UnorderedVertexMerges.Length * 2;
            for (int index = 0; index < UnorderedVertexMerges.Length; index++)
            {
                var merge = UnorderedVertexMerges[index];
                var vertexA = merge.VertexAIndex;
                var vertexB = merge.VertexBIndex;
                VertexMergeOpponentVertices.Add(vertexA, vertexB);
                VertexMergeOpponentVertices.Add(vertexB, vertexA);
            }
        }
    }
}


