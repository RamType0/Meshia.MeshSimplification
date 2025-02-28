using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
namespace Meshia.MeshSimplification
{
    [BurstCompile]
    struct MarkBorderEdgeVerticesJob : IJob
    {
        [ReadOnly]
        public Mesh.MeshData Mesh;
        [ReadOnly]
        public NativeHashMap<int2, int> EdgeCounts;
        public NativeBitArray VertexIsBorderEdgeBits;
        public void Execute()
        {
            VertexIsBorderEdgeBits.Resize(Mesh.vertexCount);
            VertexIsBorderEdgeBits.Clear();
            foreach (var pair in EdgeCounts)
            {
                var edge = pair.Key;
                var count = pair.Value;
                if (count == 1)
                {
                    VertexIsBorderEdgeBits.Set(edge.x, true);
                    VertexIsBorderEdgeBits.Set(edge.y, true);
                }
            }
        }
    }
}


