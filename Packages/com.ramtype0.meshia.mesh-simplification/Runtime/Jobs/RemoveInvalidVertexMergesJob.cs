using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
namespace Meshia.MeshSimplification
{
    [BurstCompile]
    struct RemoveInvalidVertexMergesJob : IJob
    {
        public NativeList<VertexMerge> UnorderedDirtyVertexMerges;
        public void Execute()
        {
            var index = 0;
            while (index < UnorderedDirtyVertexMerges.Length)
            {
                var merge = UnorderedDirtyVertexMerges[index];
                if (float.IsPositiveInfinity(merge.Cost))
                {
                    UnorderedDirtyVertexMerges.RemoveAtSwapBack(index);
                }
                else
                {
                    index++;
                }
            }
        }
    }
}


