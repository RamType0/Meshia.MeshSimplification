using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
namespace Meshia.MeshSimplification
{
    [BurstCompile]
    struct CountEdgesJob : IJob
    {
        [ReadOnly]
        public NativeArray<int3> Triangles;

        public NativeHashMap<int2, int> EdgeCounts;

        public void Execute()
        {
            EdgeCounts.Clear();
            var maxEdgeCount = Triangles.Length * 3;
            if (EdgeCounts.Capacity < maxEdgeCount)
            {
                EdgeCounts.Capacity = maxEdgeCount;
            }
            foreach (var triangle in Triangles)
            {
                var x = triangle.x;
                var y = triangle.y;
                var z = triangle.z;
                var edgeA = new int2(math.min(x, y), math.max(x, y));
                var edgeB = new int2(math.min(y, z), math.max(y, z));
                var edgeC = new int2(math.min(z, x), math.max(z, x));

                if (EdgeCounts.TryGetValue(edgeA, out var countA))
                {
                    EdgeCounts[edgeA] = countA + 1;
                }
                else
                {
                    EdgeCounts.Add(edgeA, 1);
                }

                if (EdgeCounts.TryGetValue(edgeB, out var countB))
                {
                    EdgeCounts[edgeB] = countB + 1;
                }
                else
                {
                    EdgeCounts.Add(edgeB, 1);
                }

                if (EdgeCounts.TryGetValue(edgeC, out var countC))
                {
                    EdgeCounts[edgeC] = countC + 1;
                }
                else
                {
                    EdgeCounts.Add(edgeC, 1);
                }
            }
        }
    }
}


