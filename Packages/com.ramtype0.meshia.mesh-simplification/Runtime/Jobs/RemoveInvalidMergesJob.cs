using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
namespace Meshia.MeshSimplification
{
    [BurstCompile]
    struct RemoveInvalidMergesJob : IJob
    {
        public NativeList<VertexMerge> VertexMerges;

        public void Execute()
        {
            var index = 0;
            while (index < VertexMerges.Length)
            {
                var merge = VertexMerges[index];
                if (float.IsPositiveInfinity(merge.Cost))
                {
                    VertexMerges.RemoveAtSwapBack(index);
                }
                else
                {
                    index++;
                }
            }
        }
    }
}


