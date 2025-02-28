using System;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
namespace Meshia.MeshSimplification
{
    struct ProgressiveMeshSimplifyData
    {
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

        public int VertexBlendWeightCount => VertexBlendWeightBuffer.Length / VertexPositionBuffer.Length;
        public NativeArray<float> VertexBlendWeightBuffer;
        public NativeArray<uint> VertexBlendIndicesBuffer;


        public NativeList<BlendShapeData> BlendShapes;

        public NativeArray<int3> Triangles;
        public NativeArray<int> VertexVersions;
        public NativeArray<ErrorQuadric> VertexErrorQuadrics;
        public NativeParallelMultiHashMap<int, int> VertexContainingTriangles;
        public NativeParallelMultiHashMap<int, int> VertexMergeOpponentVertices;
        public NativeBitArray VertexIsBorderEdgeBits;

        public unsafe UnsafeMinHeap<VertexMerge>* VertexMergesPtr;
        unsafe ref UnsafeMinHeap<VertexMerge> VertexMerges => ref *VertexMergesPtr;
        public MeshSimplifierOptions Options;

        public NativeBitArray DiscardedVertex;

        public NativeBitArray DiscardedTriangle;

        public NativeArray<float3> TriangleNormals;
        public NativeHashSet<int2> SmartLinks;

        public int VertexCount;

        MergeFactory MergeFactory => new()
        {

            VertexPositions = VertexPositionBuffer,
            VertexErrorQuadrics = VertexErrorQuadrics,
            TriangleNormals = TriangleNormals,
            VertexContainingTriangles = VertexContainingTriangles,

            VertexIsBorderEdgeBits = VertexIsBorderEdgeBits,
            Options = Options,
        };


        readonly bool IsValidMerge(VertexMerge merge)
        {
            var vertexA = merge.VertexAIndex;
            var vertexB = merge.VertexBIndex;
            var versionCheck = merge.VertexAVersion == VertexVersions[vertexA] & merge.VertexBVersion == VertexVersions[vertexB];
            if (!versionCheck)
            {
                return false;
            }
            if (WillMakeContainingTriangleFlipped(merge, vertexA, vertexB))
            {
                return false;
            }
            if (WillMakeContainingTriangleFlipped(merge, vertexB, vertexA))
            {
                return false;
            }
            return true;
        }

        readonly bool WillMakeContainingTriangleFlipped(VertexMerge merge, int vertex, int opponentVertex)
        {
            foreach (var vertexAContainingTriangleIndex in VertexContainingTriangles.GetValuesForKey(vertex))
            {
                var triangle = Triangles[vertexAContainingTriangleIndex];

                if (math.any(triangle == opponentVertex))
                {
                    continue;
                }
                int vertex1, vertex2;

                if (triangle.x == vertex)
                {
                    vertex1 = triangle.y;
                    vertex2 = triangle.z;
                }
                else if (triangle.y == vertex)
                {
                    vertex1 = triangle.z;
                    vertex2 = triangle.x;
                }
                else
                {
                    vertex1 = triangle.x;
                    vertex2 = triangle.y;
                }
                var triangleVertexPositions = new float3x3
                {
                    c0 = merge.Position,
                    c1 = VertexPositionBuffer[vertex1],
                    c2 = VertexPositionBuffer[vertex2],
                };


                var originalTriangleNormal = TriangleNormals[vertexAContainingTriangleIndex];
                var triangleNormalAfterMerge = math.cross(triangleVertexPositions.c1 - triangleVertexPositions.c0, triangleVertexPositions.c2 - triangleVertexPositions.c0);
                triangleNormalAfterMerge = math.normalize(triangleNormalAfterMerge);
                var dot = math.dot(originalTriangleNormal, triangleNormalAfterMerge);

                if (dot < Options.MinNormalDot)
                {
                    return true;
                }
            }
            return false;
        }

        public void Simplify(int targetVertexCount)
        {
            while (targetVertexCount < VertexCount && VertexMerges.TryPop(out var merge))
            {
                if (IsValidMerge(merge))
                {
                    ApplyMerge(merge);
                }
            }
        }
        readonly bool IsDiscardedVertex(int vertex) => DiscardedVertex.IsSet(vertex);
        void DiscardVertex(int vertex)
        {
            if (!IsDiscardedVertex(vertex))
            {
                DiscardedVertex.Set(vertex, true);
                VertexCount--;
                VertexVersions[vertex]++;
            }

        }
        readonly bool IsDiscardedTriangle(int triangleIndex) => DiscardedTriangle.IsSet(triangleIndex);
        void DiscardTriangle(int triangleIndex)
        {
            if (!IsDiscardedTriangle(triangleIndex))
            {
                DiscardedTriangle.Set(triangleIndex, true);
            }

        }
        public void ApplyMerge(VertexMerge merge)
        {
            var vertexA = merge.VertexAIndex;
            var vertexB = merge.VertexBIndex;
            int2 vertexPair = new(math.min(vertexA, vertexB), math.max(vertexA, vertexB));

            if (!VertexIsBorderEdgeBits.IsSet(vertexA) && VertexIsBorderEdgeBits.IsSet(vertexB))
            {
                (vertexA, vertexB) = (vertexB, vertexA);
            }

            if (!Options.PreserveBorderEdges || !VertexIsBorderEdgeBits.IsSet(vertexA) || SmartLinks.Contains(vertexPair))
            {
                MergeVertexAttributeData(vertexA, vertexB, merge.Position);
            }




            VertexErrorQuadrics.ElementAt(vertexA) += VertexErrorQuadrics[vertexB];

            VertexVersions.ElementAt(vertexA)++;

            using var nonReferencedVertices = new UnsafeList<int>(16, Allocator.Temp);

            using var vertexBContainingTriangles = new UnsafeList<int>(16, Allocator.Temp);
            foreach (var triangleIndex in VertexContainingTriangles.GetValuesForKey(vertexB))
            {
                vertexBContainingTriangles.Add(triangleIndex);
            }

            VertexContainingTriangles.Remove(vertexB);
            nonReferencedVertices.Add(vertexB);

            // Replace reference to vertexB in triangles to vertexA.
            {
                using var discardingTriangles = new UnsafeList<int>(vertexBContainingTriangles.Length, Allocator.Temp);
                foreach (var triangleIndex in vertexBContainingTriangles)
                {
                    ref var triangleVertices = ref GetTriangleVertices(triangleIndex);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    if (IsDiscardedTriangle(triangleIndex))
                    {
                        Debug.LogWarning($"Discarded triangle {triangleIndex} found in the vertex{vertexB} containing triangles.");
                    }
                    else if (!math.any(triangleVertices == vertexB))
                    {
                        Debug.LogWarning($"Triangle {triangleIndex} found in vertex{vertexB} containing triangles, but it doesn't contain the vertex{vertexB}.");
                    }
#endif
                    // Replace vertexB in triangle to vertexA.
                    if (math.any(triangleVertices == vertexA))
                    {
                        // Triangle vertices (a, b, ?) => (a, a, ?)
                        // The triangle has only 2 vertices... discarding it
                        discardingTriangles.Add(triangleIndex);
                    }
                    else
                    {
                        // Triangle vertices (b, ?, ?) => (a, ?, ?)
                        triangleVertices = math.select(triangleVertices, vertexA, triangleVertices == vertexB);
                        VertexContainingTriangles.Add(vertexA, triangleIndex);
                    }
                }

                foreach (var triangleIndex in discardingTriangles)
                {
                    // Discard vertex which doesn't belong to any triangle.

                    // We need to collect them to discardingTriangles temporary
                    // because the vertex potentially gets new belonging triangle
                    // while iterating.
                    var triangleVertices = GetTriangleVertices(triangleIndex);
                    for (int i = 0; i < 3; i++)
                    {
                        var vertex = triangleVertices[i];
                        if (vertex == vertexB)
                        {
                            continue;
                        }
                        VertexContainingTriangles.Remove(vertex, triangleIndex);
                        if (!VertexContainingTriangles.ContainsKey(vertex))
                        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                            if (nonReferencedVertices.Contains(vertex))
                            {
                                Debug.LogError($"Non referenced vertex{vertex} duplicated.");
                            }
#endif

                            nonReferencedVertices.Add(vertex);
                        }
                    }
                    DiscardTriangle(triangleIndex);
                }
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!nonReferencedVertices.Contains(vertexB))
            {
                Debug.LogError($"Non referenced vertices doesn't contain vertexB.");
            }
#endif


            // Replace all reference to vertexB in merge opponents lookup.
            // Also, we need to recompute merges.

            if (!nonReferencedVertices.Contains(vertexA))
            {
                foreach (var vertexAOpponentVertex in VertexMergeOpponentVertices.GetValuesForKey(vertexA))
                {
                    if (MergeFactory.TryComputeMerge(new(vertexA, vertexAOpponentVertex), out var position, out var cost))
                    {
                        VertexMerges.Push(new VertexMerge
                        {
                            VertexAIndex = vertexA,
                            VertexBIndex = vertexAOpponentVertex,
                            VertexAVersion = VertexVersions[vertexA],
                            VertexBVersion = VertexVersions[vertexAOpponentVertex],
                            Position = position,
                            Cost = cost,
                        });
                    }

                }

                // Recompute merge with vertexB since it was merged into vertexA
                {
                    using var vertexBOpponentVertices = new UnsafeList<int>(16, Allocator.Temp);
                    foreach (var vertexBOpponentVertex in VertexMergeOpponentVertices.GetValuesForKey(vertexB))
                    {
                        vertexBOpponentVertices.Add(vertexBOpponentVertex);
                    }
                    foreach (var vertexBOpponentVertex in vertexBOpponentVertices)
                    {
                        if (vertexBOpponentVertex == vertexA)
                        {
                            continue;
                        }

                        foreach (var vertexAOpponentVertex in VertexMergeOpponentVertices.GetValuesForKey(vertexA))
                        {
                            if (vertexBOpponentVertex == vertexAOpponentVertex)
                            {
                                goto NextVertexBOpponent;
                            }
                        }

                        if (MergeFactory.TryComputeMerge(new(vertexA, vertexBOpponentVertex), out var position, out var cost))
                        {
                            VertexMerges.Push(new VertexMerge
                            {
                                VertexAIndex = vertexA,
                                VertexBIndex = vertexBOpponentVertex,
                                VertexAVersion = VertexVersions[vertexA],
                                VertexBVersion = VertexVersions[vertexBOpponentVertex],
                                Position = position,
                                Cost = cost,
                            });
                            VertexMergeOpponentVertices.Add(vertexA, vertexBOpponentVertex);
                            VertexMergeOpponentVertices.Add(vertexBOpponentVertex, vertexA);
                        }
                    NextVertexBOpponent:;
                    }
                }
            }

            {

                foreach (var nonReferencedVertex in nonReferencedVertices)
                {
                    if (IsDiscardedVertex(nonReferencedVertex))
                    {
                        continue;
                    }
                    using var opponentVertices = new UnsafeList<int>(16, Allocator.Temp);
                    foreach (var opponentVertex in VertexMergeOpponentVertices.GetValuesForKey(nonReferencedVertex))
                    {
                        opponentVertices.Add(opponentVertex);
                    }
                    VertexMergeOpponentVertices.Remove(nonReferencedVertex);
                    foreach (var opponentVertex in opponentVertices)
                    {

                        VertexMergeOpponentVertices.Remove(opponentVertex, nonReferencedVertex);
                    }
                    DiscardVertex(nonReferencedVertex);
                }
            }

        }
        readonly ref int3 GetTriangleVertices(int triangleIndex)
        {
            return ref Triangles.ElementAt(triangleIndex);
        }
        void MergeVertexAttributeData(int vertexA, int vertexB, float3 mergePosition)
        {
            if (Options.UseBarycentricCoordinateInterpolation)
            {

                foreach (var vertexAContainingTriangleIndex in VertexContainingTriangles.GetValuesForKey(vertexA))
                {
                    var triangle = Triangles[vertexAContainingTriangleIndex];
                    if (math.any(triangle == vertexB))
                    {
                        float3x3 triangleVertexPositions = new()
                        {
                            c0 = VertexPositionBuffer[triangle.x],
                            c1 = VertexPositionBuffer[triangle.y],
                            c2 = VertexPositionBuffer[triangle.z],
                        };


                        float lerpFactor = ComputeLerpFactor(vertexA, vertexB, mergePosition);
                        var barycentricCoordinate = ComputeBarycentricCoordinate(triangleVertexPositions, mergePosition);


                        VertexPositionBuffer[vertexA] = mergePosition;
                        MergeNormalVertexAttribute(VertexNormalBuffer, triangle, vertexA, barycentricCoordinate);
                        MergeNormalVertexAttribute(VertexTangentBuffer, triangle, vertexA, barycentricCoordinate);

                        MergeVectorVertexAttribute(VertexColorBuffer, triangle, vertexA, barycentricCoordinate);
                        MergeVectorVertexAttribute(VertexTexCoord0Buffer, triangle, vertexA, barycentricCoordinate);
                        MergeVectorVertexAttribute(VertexTexCoord1Buffer, triangle, vertexA, barycentricCoordinate);
                        MergeVectorVertexAttribute(VertexTexCoord2Buffer, triangle, vertexA, barycentricCoordinate);
                        MergeVectorVertexAttribute(VertexTexCoord3Buffer, triangle, vertexA, barycentricCoordinate);
                        MergeVectorVertexAttribute(VertexTexCoord4Buffer, triangle, vertexA, barycentricCoordinate);
                        MergeVectorVertexAttribute(VertexTexCoord5Buffer, triangle, vertexA, barycentricCoordinate);
                        MergeVectorVertexAttribute(VertexTexCoord6Buffer, triangle, vertexA, barycentricCoordinate);
                        MergeVectorVertexAttribute(VertexTexCoord7Buffer, triangle, vertexA, barycentricCoordinate);


                        MergeBlendWeightAndIndices(vertexA, vertexB, lerpFactor);

                        MergeBlendShapes(triangle, vertexA, barycentricCoordinate);
                        return;
                    }
                }
            }

            {
                float lerpFactor = ComputeLerpFactor(vertexA, vertexB, mergePosition);

                VertexPositionBuffer[vertexA] = mergePosition;

                MergeNormalVertexAttribute(VertexNormalBuffer, vertexA, vertexB, lerpFactor);
                MergeNormalVertexAttribute(VertexTangentBuffer, vertexA, vertexB, lerpFactor);

                MergeVectorVertexAttribute(VertexColorBuffer, vertexA, vertexB, lerpFactor);
                MergeVectorVertexAttribute(VertexTexCoord0Buffer, vertexA, vertexB, lerpFactor);
                MergeVectorVertexAttribute(VertexTexCoord1Buffer, vertexA, vertexB, lerpFactor);
                MergeVectorVertexAttribute(VertexTexCoord2Buffer, vertexA, vertexB, lerpFactor);
                MergeVectorVertexAttribute(VertexTexCoord3Buffer, vertexA, vertexB, lerpFactor);
                MergeVectorVertexAttribute(VertexTexCoord4Buffer, vertexA, vertexB, lerpFactor);
                MergeVectorVertexAttribute(VertexTexCoord5Buffer, vertexA, vertexB, lerpFactor);
                MergeVectorVertexAttribute(VertexTexCoord6Buffer, vertexA, vertexB, lerpFactor);
                MergeVectorVertexAttribute(VertexTexCoord7Buffer, vertexA, vertexB, lerpFactor);

                MergeBlendWeightAndIndices(vertexA, vertexB, lerpFactor);

                MergeBlendShapes(vertexA, vertexB, lerpFactor);
            }




        }
        readonly float ComputeLerpFactor(int vertexA, int vertexB, float3 mergePosition)
        {
            var a = VertexPositionBuffer[vertexA];
            var b = VertexPositionBuffer[vertexB];
            var c = mergePosition;
            var ab = b - a;
            var ac = c - a;
            return math.saturate(math.dot(ab, ac) / math.lengthsq(ab));
        }

        readonly float3 ComputeBarycentricCoordinate(float3x3 triangleVertexPositions, float3 position)
        {
            var AB = triangleVertexPositions.c1 - triangleVertexPositions.c0;
            var AC = triangleVertexPositions.c2 - triangleVertexPositions.c0;
            var AP = position - triangleVertexPositions.c0;

            var dotABAB = math.dot(AB, AB);
            var dotABAC = math.dot(AB, AC);
            var dotACAC = math.dot(AC, AC);
            var dotAPAB = math.dot(AP, AB);
            var dotAPAC = math.dot(AP, AC);
            var denom = dotABAB * dotACAC - dotABAC * dotABAC;

            // Make sure the denominator is not too small to cause math problems
            const float DenomEpilson = 0.00000001f;
            if (math.abs(denom) < DenomEpilson)
            {
                denom = DenomEpilson;
            }

            var y = (dotACAC * dotAPAB - dotABAC * dotAPAC) / denom;
            var z = (dotABAB * dotAPAC - dotABAC * dotAPAB) / denom;
            var x = 1 - y - z;
            return new(x, y, z);
        }

        static void MergeVectorVertexAttribute(Span<float4> vertexAttributeData, int vertexA, int vertexB, float lerpFactor)
        {
            if (!vertexAttributeData.IsEmpty)
            {
                vertexAttributeData[vertexA] = math.lerp(vertexAttributeData[vertexA], vertexAttributeData[vertexB], lerpFactor);
            }
        }
        static void MergeVectorVertexAttribute(Span<float4> vertexAttributeData, int3 triangle, int destinationVertex, float3 barycentricCoordinate)
        {
            if (!vertexAttributeData.IsEmpty)
            {
                vertexAttributeData[destinationVertex] = vertexAttributeData[triangle.x] * barycentricCoordinate.x + vertexAttributeData[triangle.y] * barycentricCoordinate.y + vertexAttributeData[triangle.z] * barycentricCoordinate.z;
            }
        }
        static void MergeNormalVertexAttribute(Span<float4> vertexAttributeData, int vertexA, int vertexB, float lerpFactor)
        {
            if (!vertexAttributeData.IsEmpty)
            {
                vertexAttributeData[vertexA].xyz = math.normalizesafe(math.lerp(vertexAttributeData[vertexA].xyz, vertexAttributeData[vertexB].xyz, lerpFactor));
            }
        }
        static void MergeNormalVertexAttribute(Span<float4> vertexAttributeData, int3 triangle, int destinationVertex, float3 barycentricCoordinate)
        {
            if (!vertexAttributeData.IsEmpty)
            {
                vertexAttributeData[destinationVertex].xyz = math.normalizesafe(vertexAttributeData[triangle.x].xyz * barycentricCoordinate.x + vertexAttributeData[triangle.y].xyz * barycentricCoordinate.y + vertexAttributeData[triangle.z].xyz * barycentricCoordinate.z);
            }
        }

        void MergeBlendShapes(int vertexA, int vertexB, float lerpFactor)
        {
            for (int shapeIndex = 0; shapeIndex < BlendShapes.Length; shapeIndex++)
            {
                var frames = BlendShapes[shapeIndex].Frames;
                for (int frameIndex = 0; frameIndex < frames.Length; frameIndex++)
                {
                    var frame = frames[frameIndex];
                    var deltaVertices = frame.DeltaVertices;
                    var deltaNormals = frame.DeltaNormals;
                    var deltaTangents = frame.DeltaTangents;
                    deltaVertices[vertexA] = math.lerp(deltaVertices[vertexA], deltaVertices[vertexB], lerpFactor);


                    deltaNormals[vertexA] = math.normalizesafe(math.lerp(deltaNormals[vertexA], deltaNormals[vertexB], lerpFactor));

                    deltaTangents[vertexA] = math.normalizesafe(math.lerp(deltaTangents[vertexA], deltaTangents[vertexB], lerpFactor));
                }
            }
        }


        void MergeBlendShapes(int3 triangle, int destinationVertex, float3 barycentricCoordinate)
        {
            for (int shapeIndex = 0; shapeIndex < BlendShapes.Length; shapeIndex++)
            {
                var frames = BlendShapes[shapeIndex].Frames;
                for (int frameIndex = 0; frameIndex < frames.Length; frameIndex++)
                {
                    var frame = frames[frameIndex];
                    var deltaVertices = frame.DeltaVertices;
                    var deltaNormals = frame.DeltaNormals;
                    var deltaTangents = frame.DeltaTangents;
                    deltaVertices[destinationVertex] = deltaVertices[triangle.x] * barycentricCoordinate.x + deltaVertices[triangle.y] * barycentricCoordinate.y + deltaVertices[triangle.z] * barycentricCoordinate.z;
                    deltaNormals[destinationVertex] = math.normalizesafe(deltaNormals[triangle.x] * barycentricCoordinate.x + deltaNormals[triangle.y] * barycentricCoordinate.y + deltaNormals[triangle.z] * barycentricCoordinate.z);
                    deltaTangents[destinationVertex] = math.normalizesafe(deltaTangents[triangle.x] * barycentricCoordinate.x + deltaTangents[triangle.y] * barycentricCoordinate.y + deltaTangents[triangle.z] * barycentricCoordinate.z);


                }
            }
        }

        [SkipLocalsInit]
        private void MergeBlendWeightAndIndices(int vertexA, int vertexB, float lerpFactor)
        {
            if (VertexBlendWeightBuffer.Length != 0 && VertexBlendIndicesBuffer.Length != 0)
            {
                var vertexBlendWeights = VertexBlendWeightBuffer.AsSpan();
                var vertexBlendIndices = VertexBlendIndicesBuffer.AsSpan();

                var dimension = VertexBlendWeightCount;

                Span<float> blendWeightsAB = stackalloc float[dimension * 2];
                Span<uint> blendIndicesAB = stackalloc uint[dimension * 2];

                var blendWeightsA = blendWeightsAB[..dimension];
                vertexBlendWeights.Slice(vertexA * dimension, dimension).CopyTo(blendWeightsA);
                var blendWeightsB = blendWeightsAB.Slice(dimension, dimension);
                vertexBlendWeights.Slice(vertexB * dimension, dimension).CopyTo(blendWeightsB);

                var blendIndicesA = blendIndicesAB[..dimension];
                vertexBlendIndices.Slice(vertexA * dimension, dimension).CopyTo(blendIndicesA);
                var blendIndicesB = blendIndicesAB.Slice(dimension, dimension);
                vertexBlendIndices.Slice(vertexB * dimension, dimension).CopyTo(blendIndicesB);

                foreach (ref var weightA in blendWeightsA)
                {
                    weightA *= 1 - lerpFactor;
                }
                foreach (ref var weightB in blendWeightsB)
                {
                    weightB *= lerpFactor;
                }

                for (int a = 0; a < blendIndicesA.Length; a++)
                {
                    var indexA = blendIndicesA[a];

                    for (int b = 0; b < blendIndicesB.Length; b++)
                    {
                        var indexB = blendIndicesB[b];

                        if (indexA == indexB)
                        {
                            ref var weightB = ref blendWeightsB[b];

                            blendWeightsA[a] += weightB;
                            weightB = float.NegativeInfinity;

                            break;
                        }
                    }
                }

                var mergedBlendWeights = vertexBlendWeights.Slice(vertexA * dimension, dimension);
                var mergedBlendIndices = vertexBlendIndices.Slice(vertexA * dimension, dimension);

                for (int mergedBlendWeightIndex = 0; mergedBlendWeightIndex < dimension; mergedBlendWeightIndex++)
                {
                    var maxBlendWeightIndex = 0;
                    var maxBlendWeight = blendWeightsAB[maxBlendWeightIndex];
                    for (int i = 1; i < blendWeightsAB.Length; i++)
                    {
                        var blendWeight = blendWeightsAB[i];

                        if (blendWeight > maxBlendWeight)
                        {
                            maxBlendWeight = blendWeight;
                            maxBlendWeightIndex = i;
                        }
                    }
                    mergedBlendWeights[mergedBlendWeightIndex] = maxBlendWeight;
                    mergedBlendIndices[mergedBlendWeightIndex] = blendIndicesAB[maxBlendWeightIndex];

                    blendWeightsAB[maxBlendWeightIndex] = float.NegativeInfinity;
                }

                var mergedBlendWeightSum = 0f;
                foreach (var weight in mergedBlendWeights)
                {
                    mergedBlendWeightSum += weight;
                }

                var mergedBlendWeightNormalizer = math.rcp(mergedBlendWeightSum);

                foreach (ref var weight in mergedBlendWeights)
                {
                    weight *= mergedBlendWeightNormalizer;
                }
            }
        }
        public void ToMeshData(Mesh.MeshData sourceMesh, Mesh.MeshData destinationMesh, out UnsafeList<BlendShapeData> destinationBlendShapes, AllocatorManager.AllocatorHandle blendShapeDataAllocator)
        {

            var sourceVertexCount = sourceMesh.vertexCount;
            var destinationVertexCount = VertexCount;
            var sourceTriangleCount = Triangles.Length;


            var destinationToSourceVertexIndex = new NativeArray<int>(destinationVertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            var sourceToDestinationVertexIndex = new NativeArray<int>(sourceVertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            var destinationToSourceTriangleIndex = new NativeArray<int>(sourceTriangleCount - DiscardedTriangle.CountBits(0, DiscardedTriangle.Length), Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            var destinationIndexFormat = IndexFormat.UInt16;

            var destinationSubMeshes = new NativeArray<SubMeshDescriptor>(sourceMesh.subMeshCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            {
                var destinationVertexIndex = 0;
                var destinationIndexBufferIndex = 0;
                var destinationTriangleIndex = 0;
                var sourceTriangleIndex = 0;
                for (int subMeshIndex = 0; subMeshIndex < sourceMesh.subMeshCount; subMeshIndex++)
                {
                    var sourceSubMeshDescriptor = sourceMesh.GetSubMesh(subMeshIndex);
                    var destinationSubMesh = new SubMeshDescriptor
                    {
                        bounds = sourceSubMeshDescriptor.bounds,
                        topology = sourceSubMeshDescriptor.topology,
                        indexStart = destinationIndexBufferIndex,
                        firstVertex = destinationVertexIndex,
                    };
                    var sourceSubMeshLastVertex = sourceSubMeshDescriptor.firstVertex + sourceSubMeshDescriptor.vertexCount;

                    for (int sourceVertexIndex = sourceSubMeshDescriptor.firstVertex; sourceVertexIndex < sourceSubMeshLastVertex; sourceVertexIndex++)
                    {
                        if (!DiscardedVertex.IsSet(sourceVertexIndex))
                        {
                            destinationToSourceVertexIndex[destinationVertexIndex++] = sourceVertexIndex;
                        }
                    }

                    if (sourceSubMeshDescriptor.topology is MeshTopology.Triangles)
                    {
                        var sourceSubMeshTriangleCount = sourceSubMeshDescriptor.indexCount / 3;

                        var sourceSubMeshTriangleEnd = sourceTriangleIndex + sourceSubMeshTriangleCount;
                        for (; sourceTriangleIndex < sourceSubMeshTriangleEnd; sourceTriangleIndex++)
                        {
                            if (!DiscardedTriangle.IsSet(sourceTriangleIndex))
                            {
                                destinationToSourceTriangleIndex[destinationTriangleIndex++] = sourceTriangleIndex;

                                destinationIndexBufferIndex += 3;
                            }
                        }
                    }
                    else
                    {
                        destinationIndexBufferIndex += sourceSubMeshDescriptor.indexCount;

                    }

                    destinationSubMesh.vertexCount = destinationVertexIndex - destinationSubMesh.firstVertex;
                    destinationSubMesh.indexCount = destinationIndexBufferIndex - destinationSubMesh.indexStart;

                    if (destinationSubMesh.vertexCount >= ushort.MaxValue)
                    {
                        destinationIndexFormat = IndexFormat.UInt32;
                    }

                    // baseVertex is not set yet
                    destinationSubMeshes[subMeshIndex] = destinationSubMesh;
                }
            }


            for (int destinationVertexIndex = 0; destinationVertexIndex < destinationToSourceVertexIndex.Length; destinationVertexIndex++)
            {
                sourceToDestinationVertexIndex[destinationToSourceVertexIndex[destinationVertexIndex]] = destinationVertexIndex;
            }

            var maxIndexBufferValue = destinationIndexFormat switch
            {
                IndexFormat.UInt16 => ushort.MaxValue,
                IndexFormat.UInt32 => uint.MaxValue,
                _ => throw new Exception($"Unexpected index format:{destinationIndexFormat}"),
            };
            var currentDestinationBaseVertex = 0;
            for (int subMeshIndex = 0; subMeshIndex < sourceMesh.subMeshCount; subMeshIndex++)
            {
                ref var destinationSubMesh = ref destinationSubMeshes.ElementAt(subMeshIndex);

                if (destinationSubMesh.firstVertex + destinationSubMesh.vertexCount - currentDestinationBaseVertex >= maxIndexBufferValue)
                {
                    currentDestinationBaseVertex = destinationSubMesh.firstVertex;
                }
                destinationSubMesh.baseVertex = currentDestinationBaseVertex;
            }

            using var vertexAttributeDescriptors = sourceMesh.GetVertexAttributeDescriptors(Allocator.Temp);

            destinationMesh.SetVertexBufferParams(VertexCount, vertexAttributeDescriptors.AsArray());

            var destinationVertexPositions = destinationMesh.GetVertexPositions();

            for (int i = 0; i < destinationVertexPositions.Length; i++)
            {
                destinationVertexPositions[i] = VertexPositionBuffer[destinationToSourceVertexIndex[i]];
            }

            SetVertexAttributeData(destinationMesh, VertexAttribute.Normal, VertexNormalBuffer, destinationToSourceVertexIndex);
            SetVertexAttributeData(destinationMesh, VertexAttribute.Tangent, VertexTangentBuffer, destinationToSourceVertexIndex);
            SetVertexAttributeData(destinationMesh, VertexAttribute.Color, VertexColorBuffer, destinationToSourceVertexIndex);
            SetVertexAttributeData(destinationMesh, VertexAttribute.TexCoord0, VertexTexCoord0Buffer, destinationToSourceVertexIndex);
            SetVertexAttributeData(destinationMesh, VertexAttribute.TexCoord1, VertexTexCoord1Buffer, destinationToSourceVertexIndex);
            SetVertexAttributeData(destinationMesh, VertexAttribute.TexCoord2, VertexTexCoord2Buffer, destinationToSourceVertexIndex);
            SetVertexAttributeData(destinationMesh, VertexAttribute.TexCoord3, VertexTexCoord3Buffer, destinationToSourceVertexIndex);
            SetVertexAttributeData(destinationMesh, VertexAttribute.TexCoord4, VertexTexCoord4Buffer, destinationToSourceVertexIndex);
            SetVertexAttributeData(destinationMesh, VertexAttribute.TexCoord5, VertexTexCoord5Buffer, destinationToSourceVertexIndex);
            SetVertexAttributeData(destinationMesh, VertexAttribute.TexCoord6, VertexTexCoord6Buffer, destinationToSourceVertexIndex);
            SetVertexAttributeData(destinationMesh, VertexAttribute.TexCoord7, VertexTexCoord7Buffer, destinationToSourceVertexIndex);
            SetVertexAttributeData(destinationMesh, VertexAttribute.BlendWeight, VertexBlendWeightBuffer, destinationToSourceVertexIndex);
            SetVertexAttributeData(destinationMesh, VertexAttribute.BlendIndices, VertexBlendIndicesBuffer.Reinterpret<int>(), destinationToSourceVertexIndex);

            destinationBlendShapes = new(BlendShapes.Length, blendShapeDataAllocator);

            for (int shapeIndex = 0; shapeIndex < BlendShapes.Length; shapeIndex++)
            {
                var sourceBlendShape = BlendShapes[shapeIndex];
                var sourceFrames = sourceBlendShape.Frames;

                UnsafeText destinationBlendShapeName = new(sourceBlendShape.Name.Length, blendShapeDataAllocator);
                destinationBlendShapeName.CopyFrom(sourceBlendShape.Name);
                UnsafeList<BlendShapeFrameData> destinationFrames = new(sourceFrames.Length, blendShapeDataAllocator);

                for (int frameIndex = 0; frameIndex < sourceFrames.Length; frameIndex++)
                {
                    var sourceFrame = sourceFrames[frameIndex];
                    var weight = sourceFrame.Weight;
                    var sourceDeltaVertices = sourceFrame.DeltaVertices;
                    var sourceDeltaNormals = sourceFrame.DeltaNormals;
                    var sourceDeltaTangents = sourceFrame.DeltaTangents;

                    UnsafeList<float3> destinationDeltaVertices = new(destinationVertexCount, blendShapeDataAllocator);
                    
                    for (int destinationVertexIndex = 0; destinationVertexIndex < destinationVertexCount; destinationVertexIndex++)
                    {
                        var sourceVertexIndex = destinationToSourceVertexIndex[destinationVertexIndex];
                        var deltaVertex = sourceDeltaVertices[sourceVertexIndex];
                        destinationDeltaVertices.Add(deltaVertex);
                    }
                    UnsafeList<float3> destinationDeltaNormals = new(destinationVertexCount, blendShapeDataAllocator);
                    for (int destinationVertexIndex = 0; destinationVertexIndex < destinationVertexCount; destinationVertexIndex++)
                    {
                        var sourceVertexIndex = destinationToSourceVertexIndex[destinationVertexIndex];
                        var deltaNormal = sourceDeltaNormals[sourceVertexIndex];
                        destinationDeltaNormals.Add(deltaNormal);
                    }
                    UnsafeList<float3> destinationDeltaTangents = new(destinationVertexCount, blendShapeDataAllocator);
                    for (int destinationVertexIndex = 0; destinationVertexIndex < destinationVertexCount; destinationVertexIndex++)
                    {
                        var sourceVertexIndex = destinationToSourceVertexIndex[destinationVertexIndex];
                        var deltaTangent = sourceDeltaTangents[sourceVertexIndex];
                        destinationDeltaTangents.Add(deltaTangent);
                    }
                    destinationFrames.Add(new()
                    {
                        Weight = weight,
                        DeltaVertices = destinationDeltaVertices,
                        DeltaNormals = destinationDeltaNormals,
                        DeltaTangents = destinationDeltaTangents,
                    });
                }
                destinationBlendShapes.Add(new()
                {
                    Name = destinationBlendShapeName,
                    Frames = destinationFrames,
                });
            }

            destinationMesh.SetIndexBufferParams(sourceMesh.GetIndexCount() - DiscardedTriangle.CountBits(0, DiscardedTriangle.Length) * 3, destinationIndexFormat);

            switch (destinationIndexFormat)
            {
                case IndexFormat.UInt16:
                    {
                        var destinationSubMeshTriangleStart = 0;
                        var destinationIndices = destinationMesh.GetIndexData<ushort>();
                        for (int subMeshIndex = 0; subMeshIndex < sourceMesh.subMeshCount; subMeshIndex++)
                        {
                            var destinationSubMesh = destinationSubMeshes[subMeshIndex];
                            var destinationSubMeshIndexEnd = destinationSubMesh.indexStart + destinationSubMesh.indexCount;

                            if (destinationSubMesh.topology is MeshTopology.Triangles)
                            {
                                var destinationSubMeshTriangleCount = destinationSubMesh.indexCount / 3;

                                var destinationSubMeshTriangleEnd = destinationSubMeshTriangleStart + destinationSubMeshTriangleCount;

                                for (int destinationSubMeshTriangleIndex = 0; destinationSubMeshTriangleIndex < destinationSubMeshTriangleCount; destinationSubMeshTriangleIndex++)
                                {
                                    var destinationTriangleIndex = destinationSubMeshTriangleStart + destinationSubMeshTriangleIndex;


                                    var sourceTriangleIndex = destinationToSourceTriangleIndex[destinationTriangleIndex];

                                    var sourceTriangle = Triangles[sourceTriangleIndex];
                                    int3 destinationTriangle = new()
                                    {
                                        x = sourceToDestinationVertexIndex[sourceTriangle.x],
                                        y = sourceToDestinationVertexIndex[sourceTriangle.y],
                                        z = sourceToDestinationVertexIndex[sourceTriangle.z],
                                    };

                                    destinationTriangle -= destinationSubMesh.baseVertex;

                                    var destinationTriangleStart = destinationSubMeshTriangleIndex * 3 + destinationSubMesh.indexStart;

                                    destinationIndices[destinationTriangleStart + 0] = (ushort)destinationTriangle.x;
                                    destinationIndices[destinationTriangleStart + 1] = (ushort)destinationTriangle.y;
                                    destinationIndices[destinationTriangleStart + 2] = (ushort)destinationTriangle.z;
                                }
                                destinationSubMeshTriangleStart += destinationSubMeshTriangleCount;
                            }
                            else
                            {
                                var sourceSubMesh = sourceMesh.GetSubMesh(subMeshIndex);

                                var subMeshVertexIndexOffset = destinationSubMesh.firstVertex - sourceSubMesh.firstVertex;

                                var destinationSubMeshIndexBuffer = destinationIndices.GetSubArray(destinationSubMesh.indexStart, destinationSubMesh.indexCount);
                                switch (sourceMesh.indexFormat)
                                {
                                    case IndexFormat.UInt16:
                                        {
                                            var sourceIndexBuffer = sourceMesh.GetIndexData<ushort>();

                                            var sourceSubMeshIndexBuffer = sourceIndexBuffer.GetSubArray(sourceSubMesh.indexStart, sourceSubMesh.indexCount);
                                            for (int subMeshIndexBufferIndex = 0; subMeshIndexBufferIndex < destinationSubMesh.indexCount; subMeshIndexBufferIndex++)
                                            {
                                                var sourceVertexIndex = sourceSubMeshIndexBuffer[subMeshIndexBufferIndex] + sourceSubMesh.baseVertex;
                                                var destinationVertexIndex = sourceVertexIndex + subMeshVertexIndexOffset;
                                                var destinationIndexBufferValue = destinationVertexIndex - destinationSubMesh.baseVertex;
                                                destinationSubMeshIndexBuffer[subMeshIndexBufferIndex] = (ushort)destinationIndexBufferValue;
                                            }
                                        }
                                        break;
                                    case IndexFormat.UInt32:
                                        {
                                            var sourceIndexBuffer = sourceMesh.GetIndexData<uint>();

                                            var sourceSubMeshIndexBuffer = sourceIndexBuffer.GetSubArray(sourceSubMesh.indexStart, sourceSubMesh.indexCount);
                                            for (int subMeshIndexBufferIndex = 0; subMeshIndexBufferIndex < destinationSubMesh.indexCount; subMeshIndexBufferIndex++)
                                            {
                                                var sourceVertexIndex = sourceSubMeshIndexBuffer[subMeshIndexBufferIndex] + sourceSubMesh.baseVertex;
                                                var destinationVertexIndex = sourceVertexIndex + subMeshVertexIndexOffset;
                                                var destinationIndexBufferValue = destinationVertexIndex - destinationSubMesh.baseVertex;
                                                destinationSubMeshIndexBuffer[subMeshIndexBufferIndex] = (ushort)destinationIndexBufferValue;
                                            }
                                        }
                                        break;
                                }
                            }
                        }
                    }
                    break;
                case IndexFormat.UInt32:
                    {
                        var destinationSubMeshTriangleStart = 0;
                        var destinationIndices = destinationMesh.GetIndexData<int>();
                        for (int subMeshIndex = 0; subMeshIndex < sourceMesh.subMeshCount; subMeshIndex++)
                        {
                            var destinationSubMesh = destinationSubMeshes[subMeshIndex];
                            var destinationSubMeshIndexEnd = destinationSubMesh.indexStart + destinationSubMesh.indexCount;

                            if (destinationSubMesh.topology is MeshTopology.Triangles)
                            {
                                var destinationSubMeshTriangleCount = destinationSubMesh.indexCount / 3;

                                var destinationSubMeshTriangleEnd = destinationSubMeshTriangleStart + destinationSubMeshTriangleCount;

                                for (int destinationSubMeshTriangleIndex = 0; destinationSubMeshTriangleIndex < destinationSubMeshTriangleCount; destinationSubMeshTriangleIndex++)
                                {
                                    var destinationTriangleIndex = destinationSubMeshTriangleStart + destinationSubMeshTriangleIndex;


                                    var sourceTriangleIndex = destinationToSourceTriangleIndex[destinationTriangleIndex];

                                    var sourceTriangle = Triangles[sourceTriangleIndex];
                                    int3 destinationTriangle = new()
                                    {
                                        x = sourceToDestinationVertexIndex[sourceTriangle.x],
                                        y = sourceToDestinationVertexIndex[sourceTriangle.y],
                                        z = sourceToDestinationVertexIndex[sourceTriangle.z],
                                    };

                                    destinationTriangle -= destinationSubMesh.baseVertex;

                                    var destinationTriangleStart = destinationSubMeshTriangleIndex * 3 + destinationSubMesh.indexStart;
                                    destinationIndices[destinationTriangleStart + 0] = destinationTriangle.x;
                                    destinationIndices[destinationTriangleStart + 1] = destinationTriangle.y;
                                    destinationIndices[destinationTriangleStart + 2] = destinationTriangle.z;
                                }
                                destinationSubMeshTriangleStart += destinationSubMeshTriangleCount;
                            }
                            else
                            {
                                var sourceSubMesh = sourceMesh.GetSubMesh(subMeshIndex);

                                var subMeshVertexIndexOffset = destinationSubMesh.firstVertex - sourceSubMesh.firstVertex;


                                var destinationSubMeshIndexBuffer = destinationIndices.GetSubArray(destinationSubMesh.indexStart, destinationSubMesh.indexCount);
                                switch (sourceMesh.indexFormat)
                                {
                                    case IndexFormat.UInt16:
                                        {
                                            var sourceIndexBuffer = sourceMesh.GetIndexData<ushort>();
                                            var sourceSubMeshIndexBuffer = sourceIndexBuffer.GetSubArray(sourceSubMesh.indexStart, sourceSubMesh.indexCount);
                                            for (int subMeshIndexBufferIndex = 0; subMeshIndexBufferIndex < destinationSubMesh.indexCount; subMeshIndexBufferIndex++)
                                            {
                                                var sourceVertexIndex = sourceSubMeshIndexBuffer[subMeshIndexBufferIndex] + sourceSubMesh.baseVertex;
                                                var destinationVertexIndex = sourceVertexIndex + subMeshVertexIndexOffset;
                                                var destinationIndexBufferValue = destinationVertexIndex - destinationSubMesh.baseVertex;
                                                destinationSubMeshIndexBuffer[subMeshIndexBufferIndex] = destinationIndexBufferValue;
                                            }
                                        }
                                        break;
                                    case IndexFormat.UInt32:
                                        {
                                            var sourceIndexBuffer = sourceMesh.GetIndexData<int>();
                                            var sourceSubMeshIndexBuffer = sourceIndexBuffer.GetSubArray(sourceSubMesh.indexStart, sourceSubMesh.indexCount);
                                            for (int subMeshIndexBufferIndex = 0; subMeshIndexBufferIndex < destinationSubMesh.indexCount; subMeshIndexBufferIndex++)
                                            {
                                                var sourceVertexIndex = sourceSubMeshIndexBuffer[subMeshIndexBufferIndex] + sourceSubMesh.baseVertex;
                                                var destinationVertexIndex = sourceVertexIndex + subMeshVertexIndexOffset;
                                                var destinationIndexBufferValue = destinationVertexIndex - destinationSubMesh.baseVertex;
                                                destinationSubMeshIndexBuffer[subMeshIndexBufferIndex] = destinationIndexBufferValue;
                                            }
                                        }
                                        break;
                                }
                            }
                        }
                    }
                    break;
            }

            destinationMesh.subMeshCount = destinationSubMeshes.Length;
            for (int subMeshIndex = 0; subMeshIndex < destinationSubMeshes.Length; subMeshIndex++)
            {
                destinationMesh.SetSubMesh(subMeshIndex, destinationSubMeshes[subMeshIndex]);
            }

        }
        static void SetVertexAttributeData(Mesh.MeshData mesh, VertexAttribute vertexAttribute, NativeArray<float4> vertexAttributeData, NativeArray<int> destinationToSourceVertexIndex)
        {
            if (!mesh.HasVertexAttribute(vertexAttribute))
            {
                return;
            }
            var format = mesh.GetVertexAttributeFormat(vertexAttribute);
            var dimension = mesh.GetVertexAttributeDimension(vertexAttribute);

            var streamIndex = mesh.GetVertexAttributeStream(vertexAttribute);
            var stream = mesh.GetVertexData<byte>(streamIndex);
            var offset = mesh.GetVertexAttributeOffset(vertexAttribute);
            var stride = mesh.GetVertexBufferStride(streamIndex);

            for (int i = 0; i < mesh.vertexCount; i++)
            {
                SetVertexAttributeDataElement(stream, stride, offset, format, dimension, i, vertexAttributeData[destinationToSourceVertexIndex[i]]);
            }
        }
        static void SetVertexAttributeData(Mesh.MeshData mesh, VertexAttribute vertexAttribute, ReadOnlySpan<float> vertexAttributeData, NativeArray<int> destinationToSourceVertexIndex)
        {
            if (!mesh.HasVertexAttribute(vertexAttribute))
            {
                return;
            }
            var format = mesh.GetVertexAttributeFormat(vertexAttribute);
            var dimension = mesh.GetVertexAttributeDimension(vertexAttribute);

            var streamIndex = mesh.GetVertexAttributeStream(vertexAttribute);
            var stream = mesh.GetVertexData<byte>(streamIndex);
            var offset = mesh.GetVertexAttributeOffset(vertexAttribute);
            var stride = mesh.GetVertexBufferStride(streamIndex);
            for (int i = 0; i < mesh.vertexCount; i++)
            {
                var sourceIndex = destinationToSourceVertexIndex[i];
                SetVertexAttributeDataElement(stream, stride, offset, format, i, vertexAttributeData.Slice(dimension * sourceIndex, dimension));
            }
        }
        static void SetVertexAttributeData(Mesh.MeshData mesh, VertexAttribute vertexAttribute, ReadOnlySpan<int> vertexAttributeData, NativeArray<int> destinationToSourceVertexIndex)
        {
            if (!mesh.HasVertexAttribute(vertexAttribute))
            {
                return;
            }
            var format = mesh.GetVertexAttributeFormat(vertexAttribute);
            var dimension = mesh.GetVertexAttributeDimension(vertexAttribute);

            var streamIndex = mesh.GetVertexAttributeStream(vertexAttribute);
            var stream = mesh.GetVertexData<byte>(streamIndex);
            var offset = mesh.GetVertexAttributeOffset(vertexAttribute);
            var stride = mesh.GetVertexBufferStride(streamIndex);
            for (int i = 0; i < mesh.vertexCount; i++)
            {
                var sourceIndex = destinationToSourceVertexIndex[i];
                SetVertexAttributeDataElement(stream, stride, offset, format, i, vertexAttributeData.Slice(dimension * sourceIndex, dimension));
            }
        }
        public static unsafe void SetVertexAttributeDataElement(NativeArray<byte> stream, int stride, int offset, VertexAttributeFormat format, int dimension, int vertexIndex, float4 value)
        {
            var ptr = (byte*)stream.GetUnsafePtr() + (stride * vertexIndex + offset);
            switch (format)
            {
                case VertexAttributeFormat.Float32:
                    {
                        var vertexComponents = (float*)ptr;

                        for (int i = 0; i < dimension; i++)
                        {
                            vertexComponents[i] = value[i];
                        }

                    }
                    break;
                case VertexAttributeFormat.Float16:
                    {
                        var vertexComponents = (half*)ptr;

                        for (int i = 0; i < dimension; i++)
                        {
                            vertexComponents[i] = (half)value[i];
                        }

                    }
                    break;
                case VertexAttributeFormat.UNorm8:
                    {
                        var vertexComponents = ptr;

                        for (int i = 0; i < dimension; i++)
                        {
                            vertexComponents[i] = (byte)(value[i] * byte.MaxValue);
                        }

                    }
                    break;
                case VertexAttributeFormat.SNorm8:
                    {
                        var vertexComponents = (sbyte*)ptr;

                        for (int i = 0; i < dimension; i++)
                        {
                            vertexComponents[i] = (sbyte)(value[i] * sbyte.MaxValue);
                        }

                    }
                    break;
                case VertexAttributeFormat.UNorm16:
                    {
                        var vertexComponents = (ushort*)ptr;
                        for (int i = 0; i < dimension; i++)
                        {
                            vertexComponents[i] = (ushort)(value[i] * ushort.MaxValue);
                        }
                    }
                    break;
                case VertexAttributeFormat.SNorm16:
                    {
                        var vertexComponents = (short*)ptr;
                        for (int i = 0; i < dimension; i++)
                        {
                            vertexComponents[i] = (short)(value[i] * short.MaxValue);
                        }

                    }
                    break;
                default:
                    throw new NotSupportedException($"The assigned {nameof(VertexAttributeFormat)} is not supported yet.");
            }
        }
        public static unsafe void SetVertexAttributeDataElement(NativeArray<byte> stream, int stride, int offset, VertexAttributeFormat format, int vertexIndex, ReadOnlySpan<float> value)
        {
            var ptr = (byte*)stream.GetUnsafePtr() + (stride * vertexIndex + offset);
            switch (format)
            {
                case VertexAttributeFormat.Float32:
                    {
                        var vertexComponents = (float*)ptr;

                        for (int i = 0; i < value.Length; i++)
                        {
                            vertexComponents[i] = value[i];
                        }

                    }
                    break;
                case VertexAttributeFormat.Float16:
                    {
                        var vertexComponents = (half*)ptr;

                        for (int i = 0; i < value.Length; i++)
                        {
                            vertexComponents[i] = (half)value[i];
                        }

                    }
                    break;
                case VertexAttributeFormat.UNorm8:
                    {
                        var vertexComponents = ptr;

                        for (int i = 0; i < value.Length; i++)
                        {
                            vertexComponents[i] = (byte)(value[i] * byte.MaxValue);
                        }

                    }
                    break;
                case VertexAttributeFormat.SNorm8:
                    {
                        var vertexComponents = (sbyte*)ptr;

                        for (int i = 0; i < value.Length; i++)
                        {
                            vertexComponents[i] = (sbyte)(value[i] * sbyte.MaxValue);
                        }

                    }
                    break;
                case VertexAttributeFormat.UNorm16:
                    {
                        var vertexComponents = (ushort*)ptr;
                        for (int i = 0; i < value.Length; i++)
                        {
                            vertexComponents[i] = (ushort)(value[i] * ushort.MaxValue);
                        }
                    }
                    break;
                case VertexAttributeFormat.SNorm16:
                    {
                        var vertexComponents = (short*)ptr;
                        for (int i = 0; i < value.Length; i++)
                        {
                            vertexComponents[i] = (short)(value[i] * short.MaxValue);
                        }

                    }
                    break;
                default:
                    throw new NotSupportedException($"The assigned {nameof(VertexAttributeFormat)} is not supported yet.");
            }
        }
        public static unsafe void SetVertexAttributeDataElement(NativeArray<byte> stream, int stride, int offset, VertexAttributeFormat format, int vertexIndex, ReadOnlySpan<int> value)
        {
            var ptr = (byte*)stream.GetUnsafePtr() + (stride * vertexIndex + offset);

            switch (format)
            {
                case VertexAttributeFormat.UInt8:
                    {
                        var vertexComponents = (byte*)ptr;

                        for (int i = 0; i < value.Length; i++)
                        {
                            vertexComponents[i] = (byte)value[i];
                        }
                    }
                    break;
                case VertexAttributeFormat.SInt8:
                    {
                        var vertexComponents = (sbyte*)ptr;

                        for (int i = 0; i < value.Length; i++)
                        {
                            vertexComponents[i] = (sbyte)value[i];
                        }
                    }
                    break;
                case VertexAttributeFormat.UInt16:
                    {
                        var vertexComponents = (ushort*)ptr;

                        for (int i = 0; i < value.Length; i++)
                        {
                            vertexComponents[i] = (ushort)value[i];
                        }
                    }
                    break;
                case VertexAttributeFormat.SInt16:
                    {
                        var vertexComponents = (short*)ptr;

                        for (int i = 0; i < value.Length; i++)
                        {
                            vertexComponents[i] = (short)value[i];
                        }
                    }
                    break;
                case VertexAttributeFormat.UInt32:
                    {
                        var vertexComponents = (uint*)ptr;

                        for (int i = 0; i < value.Length; i++)
                        {
                            vertexComponents[i] = (uint)value[i];
                        }
                    }
                    break;
                case VertexAttributeFormat.SInt32:
                    {
                        var vertexComponents = (int*)ptr;

                        for (int i = 0; i < value.Length; i++)
                        {
                            vertexComponents[i] = (int)value[i];
                        }
                    }
                    break;
                default:
                    throw new NotSupportedException($"The assigned {nameof(VertexAttributeFormat)} is not supported yet.");
            }
        }
    }
}


