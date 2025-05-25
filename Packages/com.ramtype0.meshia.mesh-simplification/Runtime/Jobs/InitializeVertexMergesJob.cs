using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
namespace Meshia.MeshSimplification
{
    [BurstCompile]
    struct InitializeVertexMergesJob : IJob
    {
        [ReadOnly]
        public NativeArray<VertexMerge> UnorderedDirtyVertexMerges;
        public NativeMinPairingHeap<VertexMerge> VertexMerges;
        public void Execute()
        {
            VertexMerges.Clear();
            for (int i = 0; i < UnorderedDirtyVertexMerges.Length; i++)
            {
                var merge = UnorderedDirtyVertexMerges[i];
                if (float.IsPositiveInfinity(merge.Cost))
                {
                    continue;
                }
                VertexMerges.Enqueue(merge);

            }

        }
    }
}


