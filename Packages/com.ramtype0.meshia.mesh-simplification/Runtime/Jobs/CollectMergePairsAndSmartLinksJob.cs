using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
namespace Meshia.MeshSimplification
{
    [BurstCompile]
    struct CollectMergePairsAndSmartLinksJob : IJob
    {
        [ReadOnly]
        public NativeHashMap<int2, int> EdgeCounts;
        [ReadOnly]
        public NativeHashSet<int2> SmartLinks;
        public NativeList<int2> MergePairs;

        public void Execute()
        {

            var maxPairCount = EdgeCounts.Count + SmartLinks.Count;


            MergePairs.Clear();
            if (MergePairs.Capacity < maxPairCount) 
            {
                MergePairs.Capacity = maxPairCount;
            }

            foreach (var link in SmartLinks)
            {
                if (!EdgeCounts.ContainsKey(link))
                {
                    MergePairs.AddNoResize(link);
                }
            }

            foreach (var pair in EdgeCounts)
            {
                MergePairs.AddNoResize(pair.Key);
            }
        }
    }
}


