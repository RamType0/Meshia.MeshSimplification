using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
namespace Meshia.MeshSimplification
{
    [BurstCompile]
    struct ComputeMergesJob : IJobParallelForDefer
    {
        [ReadOnly]
        public NativeArray<float3> VertexPositionBuffer;
        [ReadOnly]
        public NativeArray<ErrorQuadric> VertexErrorQuadrics;
        [ReadOnly]
        public NativeArray<float3> TriangleNormals;
        [ReadOnly]
        public NativeParallelMultiHashMap<int, int> VertexContainingTriangles;
        [ReadOnly]
        public NativeBitArray VertexIsBorderEdgeBits;
        [ReadOnly]
        public NativeArray<int2> Edges;
        [WriteOnly]
        public NativeArray<VertexMerge> UnorderedDirtyVertexMerges;

        public MeshSimplifierOptions Options;
        public void Execute(int index)
        {
            var mergeFactory = new MergeFactory
            {
                VertexPositions = VertexPositionBuffer,
                VertexErrorQuadrics = VertexErrorQuadrics,
                VertexContainingTriangles = VertexContainingTriangles,
                VertexIsBorderEdgeBits = VertexIsBorderEdgeBits,
                TriangleNormals = TriangleNormals,
                Options = Options,
            };
            var edge = Edges[index];
            VertexMerge merge;
            if (mergeFactory.TryComputeMerge(edge, out var position, out var cost))
            {
                merge = new()
                {
                    VertexAIndex = edge.x,
                    VertexBIndex = edge.y,
                    VertexAVersion = 0,
                    VertexBVersion = 0,
                    Position = position,
                    Cost = cost,
                };

            }
            else
            {

                merge = new()
                {
                    VertexAIndex = edge.x,
                    VertexBIndex = edge.y,
                    VertexAVersion = 0,
                    VertexBVersion = 0,
                    Position = float.NaN,
                    Cost = float.PositiveInfinity,
                };
            }
            UnorderedDirtyVertexMerges[index] = merge;

        }
    }
}


