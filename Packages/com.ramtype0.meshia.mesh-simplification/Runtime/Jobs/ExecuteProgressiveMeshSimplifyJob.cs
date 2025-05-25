using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
namespace Meshia.MeshSimplification
{
    [BurstCompile]
    struct ExecuteProgressiveMeshSimplifyJob : IJob
    {
        public Mesh.MeshData Mesh;
        public MeshSimplificationTarget Target;
        public Mesh.MeshData DestinationMesh;
        public NativeList<BlendShapeData> DestinationBlendShapes;

        public NativeArray<float3> VertexPositionBuffer;
        public NativeArray<float4> VertexNormalBuffer;
        public NativeArray<float4> VertexTangentBuffer;
        public NativeArray<float4> VertexColorBuffer;
        public NativeArray<float4> VertexTexCoord0Buffer;
        public NativeArray<float4> VertexTexCoord1Buffer;
        public NativeArray<float4> VertexTexCoord2Buffer;
        public NativeArray<float4> VertexTexCoord3Buffer;
        public NativeArray<float4> VertexTexCoord4Buffer;
        public NativeArray<float4> VertexTexCoord5Buffer;
        public NativeArray<float4> VertexTexCoord6Buffer;
        public NativeArray<float4> VertexTexCoord7Buffer;
        public NativeArray<float> VertexBlendWeightBuffer;
        public NativeArray<uint> VertexBlendIndicesBuffer;
        public NativeArray<uint> VertexContainingSubMeshIndices;
        public NativeList<BlendShapeData> BlendShapes;
        public NativeArray<int3> Triangles;
        public NativeArray<ErrorQuadric> VertexErrorQuadrics;
        public NativeParallelMultiHashMap<int, int> VertexContainingTriangles;
        public NativeParallelMultiHashMap<int, int> VertexMergeOpponentVertices;
        public NativeBitArray PreserveVertex;
        public NativeArray<float3> TriangleNormals;
        public NativeBitArray DiscardedTriangle;
        public NativeBitArray DiscardedVertex;
        public NativeMinPairingHeap<VertexMerge> Merges;
        public NativeHashSet<int2> SmartLinks;
        public AllocatorManager.AllocatorHandle BlendShapeDataAllocator;
        public MeshSimplifierOptions Options;
        public unsafe void Execute()
        {
            using var vertexVersions = new NativeArray<int>(VertexPositionBuffer.Length, Allocator.Temp);
            var progressiveMeshSimplifyData = new ProgressiveMeshSimplifyData
            {
                VertexPositionBuffer = VertexPositionBuffer,
                VertexNormalBuffer = VertexNormalBuffer,
                VertexTangentBuffer = VertexTangentBuffer,
                VertexColorBuffer = VertexColorBuffer,
                VertexTexCoord0Buffer = VertexTexCoord0Buffer,
                VertexTexCoord1Buffer = VertexTexCoord1Buffer,
                VertexTexCoord2Buffer = VertexTexCoord2Buffer,
                VertexTexCoord3Buffer = VertexTexCoord3Buffer,
                VertexTexCoord4Buffer = VertexTexCoord4Buffer,
                VertexTexCoord5Buffer = VertexTexCoord5Buffer,
                VertexTexCoord6Buffer = VertexTexCoord6Buffer,
                VertexTexCoord7Buffer = VertexTexCoord7Buffer,
                VertexBlendWeightBuffer = VertexBlendWeightBuffer,
                VertexBlendIndicesBuffer = VertexBlendIndicesBuffer,
                BlendShapes = BlendShapes,
                Triangles = Triangles,
                VertexContainingSubMeshIndices = VertexContainingSubMeshIndices,
                VertexVersions = vertexVersions,
                VertexErrorQuadrics = VertexErrorQuadrics,
                VertexContainingTriangles = VertexContainingTriangles,
                VertexMergeOpponentVertices = VertexMergeOpponentVertices,
                VertexIsBorderEdgeBits = PreserveVertex,
                VertexMerges = Merges,
                DiscardedVertex = DiscardedVertex,
                DiscardedTriangle = DiscardedTriangle,
                VertexCount = VertexPositionBuffer.Length - DiscardedVertex.CountBits(0, DiscardedVertex.Length),
                TriangleCount = Triangles.Length - DiscardedTriangle.CountBits(0, DiscardedTriangle.Length),
                Options = Options,
                TriangleNormals = TriangleNormals,
                SmartLinks = SmartLinks,

            };

            progressiveMeshSimplifyData.Simplify(Mesh, Target);
            progressiveMeshSimplifyData.ToMeshData(Mesh, DestinationMesh, DestinationBlendShapes, BlendShapeDataAllocator);
        }
    }
}


