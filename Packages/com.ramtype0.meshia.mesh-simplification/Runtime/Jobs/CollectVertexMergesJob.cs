using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
namespace Meshia.MeshSimplification
{
    [BurstCompile]
    unsafe struct CollectVertexMergesJob : IJob
    {
        [ReadOnly]
        public NativeArray<VertexMerge> UnorderedVertexMerges;
        public NativeKeyedMinPriorityQueue<int2, VertexMerge> VertexMerges;
        public void Execute()
        {
            ref var vertexMerges = ref *VertexMerges.GetUnsafeKeyedMinPriorityQueue();
            vertexMerges.Clear();
            if(vertexMerges.nodes.Capacity < UnorderedVertexMerges.Length)
            {
                vertexMerges.nodes.Capacity = UnorderedVertexMerges.Length;
            }
            if(vertexMerges.keyToIndex.Capacity < UnorderedVertexMerges.Length)
            {
                vertexMerges.keyToIndex.Capacity = UnorderedVertexMerges.Length;
            }


            for (int i = 0; i < UnorderedVertexMerges.Length; i++)
            {
                var merge = UnorderedVertexMerges[i];
                vertexMerges.nodes.AddNoResize(KeyValuePair.Create(new int2(math.min(merge.VertexAIndex, merge.VertexBIndex), math.max(merge.VertexAIndex, merge.VertexBIndex)), merge));    
            }
            vertexMerges.Heapify();
        }
    }
}


