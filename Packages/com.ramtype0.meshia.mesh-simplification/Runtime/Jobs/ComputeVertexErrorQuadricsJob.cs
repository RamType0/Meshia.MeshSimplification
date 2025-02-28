using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Plane = Unity.Mathematics.Geometry.Plane;
namespace Meshia.MeshSimplification
{
    [BurstCompile]
    struct ComputeVertexErrorQuadricsJob : IJobParallelForDefer
    {
        [ReadOnly]
        public NativeArray<float3> VertexPositionBuffer;
        [ReadOnly]
        public NativeArray<int3> Triangles;
        [ReadOnly]
        public NativeParallelMultiHashMap<int, int> VertexContainingTriangles;
        [ReadOnly]
        public NativeHashMap<int2, int> EdgeCounts;
        [ReadOnly]
        public NativeArray<ErrorQuadric> TriangleErrorQuadrics;
        [WriteOnly]
        public NativeArray<ErrorQuadric> VertexErrorQuadrics;
        public void Execute(int vertexIndex)
        {
            var vertexErrorQuadric = new ErrorQuadric();
            var vertexPosition = VertexPositionBuffer[vertexIndex];
            foreach (var triangleIndex in VertexContainingTriangles.GetValuesForKey(vertexIndex))
            {
                var triangle = Triangles[triangleIndex];
                var x = triangle.x;
                var y = triangle.y;
                var z = triangle.z;

                var edgeA = new int2(math.min(x, y), math.max(x, y));
                var edgeB = new int2(math.min(y, z), math.max(y, z));
                var edgeC = new int2(math.min(z, x), math.max(z, x));

                int2x2 belongingEdges;
                if (vertexIndex == x)
                {
                    belongingEdges = new(edgeA, edgeC);
                }
                else if (vertexIndex == y)
                {

                    belongingEdges = new(edgeA, edgeB);
                }
                else if (vertexIndex == z)
                {
                    belongingEdges = new(edgeB, edgeC);
                }
                else
                {

                    throw new Exception();
                }


                if (EdgeCounts[belongingEdges.c0] == 1 || EdgeCounts[belongingEdges.c1] == 1)
                {
                    vertexErrorQuadric += new ErrorQuadric(new Plane(math.right(), vertexPosition));

                    vertexErrorQuadric += new ErrorQuadric(new Plane(math.up(), vertexPosition));

                    vertexErrorQuadric += new ErrorQuadric(new Plane(math.forward(), vertexPosition));
                }
                else
                {
                    vertexErrorQuadric += TriangleErrorQuadrics[triangleIndex];
                }
            }
            VertexErrorQuadrics[vertexIndex] = vertexErrorQuadric;
        }
    }
}


