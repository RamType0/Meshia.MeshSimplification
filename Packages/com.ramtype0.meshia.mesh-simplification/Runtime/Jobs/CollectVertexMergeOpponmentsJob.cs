using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
namespace Meshia.MeshSimplification
{
    [BurstCompile]
    unsafe struct CollectVertexMergeOpponmentsJob : IJob
    {
        [ReadOnly]
        public NativeMinPriorityQueue<VertexMerge> VertexMerges;
        public NativeParallelMultiHashMap<int, int> VertexMergeOpponentVertices;
        public void Execute()
        {
            ref readonly var vertexMerges = ref VertexMerges.GetUnsafePriorityQueue()->nodes;
            VertexMergeOpponentVertices.Clear();
            VertexMergeOpponentVertices.Capacity = vertexMerges.Length * 2;
            for (int index = 0; index < vertexMerges.Length; index++)
            {
                var merge = vertexMerges[index];
                var vertexA = merge.VertexAIndex;
                var vertexB = merge.VertexBIndex;
                VertexMergeOpponentVertices.Add(vertexA, vertexB);
                VertexMergeOpponentVertices.Add(vertexB, vertexA);
            }
        }
    }
}


