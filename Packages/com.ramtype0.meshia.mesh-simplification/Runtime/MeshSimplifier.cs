using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Meshia.MeshSimplification
{
    public struct MeshSimplifier : INativeDisposable
    {
        NativeList<float3> VertexPositionBuffer;

        NativeList<float4> VertexNormalBuffer;
        NativeList<float4> VertexTangentBuffer;

        NativeList<float4> VertexColorBuffer;

        NativeList<float4> VertexTexCoord0Buffer;
        NativeList<float4> VertexTexCoord1Buffer;
        NativeList<float4> VertexTexCoord2Buffer;
        NativeList<float4> VertexTexCoord3Buffer;
        NativeList<float4> VertexTexCoord4Buffer;
        NativeList<float4> VertexTexCoord5Buffer;
        NativeList<float4> VertexTexCoord6Buffer;
        NativeList<float4> VertexTexCoord7Buffer;

        NativeList<float> VertexBlendWeightBuffer;
        NativeList<uint> VertexBlendIndicesBuffer;

        NativeList<uint> VertexSubMeshIndices;

        NativeList<ErrorQuadric> VertexErrorQuadrics;

        NativeList<int3> Triangles;
        NativeList<float3> TriangleNormals;

        NativeParallelMultiHashMap<int, int> VertexMergeOpponentVertices;
        NativeParallelMultiHashMap<int, int> VertexContainingTriangles;

        NativeBitArray VertexIsDiscardedBits;
        NativeBitArray VertexIsBorderEdgeBits;
        NativeBitArray TriangleIsDiscardedBits;

        NativeHashSet<int2> SmartLinks;

        NativeList<VertexMerge> VertexMerges;

        MeshSimplifierOptions Options;

        AllocatorManager.AllocatorHandle Allocator;
        /// <summary>
        /// Simplifies the given <paramref name="mesh"/> and writes the result to <paramref name="destination"/>.
        /// </summary>
        /// <param name="mesh">The mesh to simplify.</param>
        /// <param name="target">The simplification target for this mesh simplification.</param>
        /// <param name="options">The options for this mesh simplification.</param>
        /// <param name="destination">The destination to write simplified mesh.</param>
        /// <remarks>The <paramref name="mesh"/> will not be modified.</remarks>
        public static void Simplify(Mesh mesh, MeshSimplificationTarget target, MeshSimplifierOptions options, Mesh destination)
        {
            Allocator allocator = Unity.Collections.Allocator.TempJob;
            var originalMeshDataArray = Mesh.AcquireReadOnlyMeshData(mesh);
            var originalMeshData = originalMeshDataArray[0];
            var blendShapes = BlendShapeData.GetMeshBlendShapes(mesh, allocator);

            using var meshSimplifier = new MeshSimplifier(allocator);

            var load = meshSimplifier.ScheduleLoadMeshData(originalMeshData, options);

            var simplifiedMeshDataArray = Mesh.AllocateWritableMeshData(1);
            NativeList<BlendShapeData> simplifiedBlendShapes = new(allocator);
            var simplify = meshSimplifier.ScheduleSimplify(originalMeshData, blendShapes, target, simplifiedMeshDataArray[0], simplifiedBlendShapes, load);

            JobHandle.ScheduleBatchedJobs();
            simplify.Complete();

            originalMeshDataArray.Dispose();

            foreach (var blendShape in blendShapes)
            {
                blendShape.Dispose();
            }
            blendShapes.Dispose();

            ApplySimplifiedMesh(mesh, simplifiedMeshDataArray, simplifiedBlendShapes.AsArray(), destination);

            foreach (var simplifiedBlendShape in simplifiedBlendShapes)
            {
                simplifiedBlendShape.Dispose();
            }
            simplifiedBlendShapes.Dispose();
        }
        /// <summary>
        /// Asynchronously simplifies the given <paramref name="mesh"/> and writes the result to <paramref name="destination"/>.
        /// </summary>
        /// <param name="mesh">The mesh to simplify.</param>
        /// <param name="target">The simplification target for this mesh simplification.</param>
        /// <param name="options">The options for this mesh simplification.</param>
        /// <param name="destination">The destination to write simplified mesh.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        /// <returns></returns>
        /// <remarks>The <paramref name="mesh"/> will not be modified.</remarks>
        public static async Task SimplifyAsync(Mesh mesh, MeshSimplificationTarget target, MeshSimplifierOptions options, Mesh destination, CancellationToken cancellationToken = default)
        {
            Allocator allocator = Unity.Collections.Allocator.Persistent;
            var originalMeshDataArray = Mesh.AcquireReadOnlyMeshData(mesh);
            var originalMeshData = originalMeshDataArray[0];
            var blendShapes = BlendShapeData.GetMeshBlendShapes(mesh, allocator);

            using var meshSimplifier = new MeshSimplifier(allocator);

            var load = meshSimplifier.ScheduleLoadMeshData(originalMeshData, options);

            var simplifiedMeshDataArray = Mesh.AllocateWritableMeshData(1);
            NativeList<BlendShapeData> simplifiedBlendShapes = new(allocator);
            var simplify = meshSimplifier.ScheduleSimplify(originalMeshData, blendShapes, target, simplifiedMeshDataArray[0], simplifiedBlendShapes, load);

            JobHandle.ScheduleBatchedJobs();
            while (!simplify.IsCompleted)
            {
                await Task.Yield();
            }
            simplify.Complete();

            originalMeshDataArray.Dispose();

            foreach (var blendShape in blendShapes)
            {
                blendShape.Dispose();
            }
            blendShapes.Dispose();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                ApplySimplifiedMesh(mesh, simplifiedMeshDataArray, simplifiedBlendShapes.AsArray(), destination);
            }
            catch (OperationCanceledException)
            {
                simplifiedMeshDataArray.Dispose();
                throw;
            }
            finally
            {
                foreach (var simplifiedBlendShape in simplifiedBlendShapes)
                {
                    simplifiedBlendShape.Dispose();
                }
                simplifiedBlendShapes.Dispose();
            }
        }

        private static void ApplySimplifiedMesh(Mesh mesh, Mesh.MeshDataArray simplifiedMeshDataArray, ReadOnlySpan<BlendShapeData> simplifiedBlendShapes, Mesh destination)
        {
            Mesh.ApplyAndDisposeWritableMeshData(simplifiedMeshDataArray, destination, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds);

            if (mesh != destination)
            {
                destination.bounds = mesh.bounds;
#if UNITY_2023_1_OR_NEWER
                var bindposes = mesh.GetBindposes();
                if (bindposes.Length != 0)
                {
                    destination.SetBindposes(bindposes);
                }
#else
                var bindposes = mesh.bindposes;
                if (bindposes.Length != 0)
                {
                    destination.bindposes = bindposes;
                }
#endif
            }

            BlendShapeData.SetBlendShapes(destination, simplifiedBlendShapes);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MeshSimplifier"/> with the given allocator.
        /// </summary>
        /// <param name="allocator">The <see cref="AllocatorManager.AllocatorHandle"/> for this <see cref="MeshSimplifier"/>.</param>
        public MeshSimplifier(AllocatorManager.AllocatorHandle allocator)
        {
            VertexPositionBuffer = new(allocator);

            VertexNormalBuffer = new(allocator);
            VertexTangentBuffer = new(allocator);

            VertexColorBuffer = new(allocator);

            VertexTexCoord0Buffer = new(allocator);
            VertexTexCoord1Buffer = new(allocator);
            VertexTexCoord2Buffer = new(allocator);
            VertexTexCoord3Buffer = new(allocator);
            VertexTexCoord4Buffer = new(allocator);
            VertexTexCoord5Buffer = new(allocator);
            VertexTexCoord6Buffer = new(allocator);
            VertexTexCoord7Buffer = new(allocator);

            VertexBlendWeightBuffer = new(allocator);
            VertexBlendIndicesBuffer = new(allocator);

            VertexSubMeshIndices = new(allocator);

            VertexErrorQuadrics = new(allocator);

            Triangles = new(allocator);
            TriangleNormals = new(allocator);

            VertexMergeOpponentVertices = new(0, allocator);
            VertexContainingTriangles = new(0, allocator);

            VertexIsDiscardedBits = new(0, allocator, NativeArrayOptions.UninitializedMemory);
            VertexIsBorderEdgeBits = new(0, allocator, NativeArrayOptions.UninitializedMemory);
            TriangleIsDiscardedBits = new(0, allocator, NativeArrayOptions.UninitializedMemory);

            SmartLinks = new(0, allocator);

            VertexMerges = new(allocator);
            Options = default;

            Allocator = allocator;

        }

        /// <summary>
        /// Creates and schedules a job that will load mesh data from the <paramref name="meshData"/> into this <see cref="MeshSimplifier"/>.
        /// </summary>
        /// <param name="meshData">The mesh data to load.</param>
        /// <param name="options">The options for this mesh simplification.</param>
        /// <param name="dependency">The handle of a job which the new job will depend upon.</param>
        /// <returns>The handle of a new job that will load mesh data from the <paramref name="meshData"/> into this <see cref="MeshSimplifier"/>.</returns>
        public JobHandle ScheduleLoadMeshData(Mesh.MeshData meshData, MeshSimplifierOptions options, JobHandle dependency = default)
        {
            Options = options;
            var constructVertexPositionBuffer = ScheduleCopyVertexPositionBuffer(meshData, dependency);
            var constructVertexNormalBuffer = ScheduleCopyVertexAttributeBufferAsFloat4(meshData, VertexAttribute.Normal, dependency);
            var constructVertexTangentBuffer = ScheduleCopyVertexAttributeBufferAsFloat4(meshData, VertexAttribute.Tangent, dependency);
            var constructVertexColorBuffer = ScheduleCopyVertexAttributeBufferAsFloat4(meshData, VertexAttribute.Color, dependency);
            var constructVertexTexcoord0Buffer = ScheduleCopyVertexAttributeBufferAsFloat4(meshData, VertexAttribute.TexCoord0, dependency);
            var constructVertexTexcoord1Buffer = ScheduleCopyVertexAttributeBufferAsFloat4(meshData, VertexAttribute.TexCoord1, dependency);
            var constructVertexTexcoord2Buffer = ScheduleCopyVertexAttributeBufferAsFloat4(meshData, VertexAttribute.TexCoord2, dependency);
            var constructVertexTexcoord3Buffer = ScheduleCopyVertexAttributeBufferAsFloat4(meshData, VertexAttribute.TexCoord3, dependency);
            var constructVertexTexcoord4Buffer = ScheduleCopyVertexAttributeBufferAsFloat4(meshData, VertexAttribute.TexCoord4, dependency);
            var constructVertexTexcoord5Buffer = ScheduleCopyVertexAttributeBufferAsFloat4(meshData, VertexAttribute.TexCoord5, dependency);
            var constructVertexTexcoord6Buffer = ScheduleCopyVertexAttributeBufferAsFloat4(meshData, VertexAttribute.TexCoord6, dependency);
            var constructVertexTexcoord7Buffer = ScheduleCopyVertexAttributeBufferAsFloat4(meshData, VertexAttribute.TexCoord7, dependency);
            var constructVertexBlendWeightBuffer = ScheduleCopyVertexBlendWeightBuffer(meshData, dependency);
            var constructVertexBlendIndicesBuffer = ScheduleCopyVertexBlendIndicesBuffer(meshData, dependency);
            var collectVertexSubMeshIndices = ScheduleCollectVertexSubMeshIndices(meshData, dependency);

            var constructTriangles = ScheduleCopyTriangles(meshData, dependency);

            var constructVertexContainingTrianglesAndTriangleDiscardedBits = ScheduleInitializeVertexContainingTrianglesAndTriangleIsDiscardedBits(constructTriangles);

            var constructEdgeCounts = ScheduleConstructEdgeCounts(out var edgeCounts, Triangles, constructTriangles, Allocator);

            var constructVertexIsDiscardedBits = ScheduleInitializeVertexIsDiscardedBits(meshData, dependency, constructVertexContainingTrianglesAndTriangleDiscardedBits);
            var collectSmartLinks = options.EnableSmartLink
                ? ScheduleCollectSmartLinks(
                    meshData,
                    VertexPositionBuffer,
                    VertexNormalBuffer,
                    VertexColorBuffer,
                    VertexTexCoord0Buffer,
                    VertexTexCoord1Buffer,
                    VertexTexCoord2Buffer,
                    VertexTexCoord3Buffer,
                    VertexTexCoord4Buffer,
                    VertexTexCoord5Buffer,
                    VertexTexCoord6Buffer,
                    VertexTexCoord7Buffer,
                    VertexIsDiscardedBits,
                    options,
                    SmartLinks,
                    dependency,
                    constructVertexPositionBuffer,
                    stackalloc[]
                    {
                        constructVertexNormalBuffer,
                        constructVertexColorBuffer,
                        constructVertexTexcoord0Buffer,
                        constructVertexTexcoord1Buffer,
                        constructVertexTexcoord2Buffer,
                        constructVertexTexcoord3Buffer,
                        constructVertexTexcoord4Buffer,
                        constructVertexTexcoord5Buffer,
                        constructVertexTexcoord6Buffer,
                        constructVertexTexcoord7Buffer,
                    }.CombineDependencies(),
                    constructVertexIsDiscardedBits,
                    Allocator
                    )
                : new JobHandle();


            var constructEdges = ScheduleConstructMergePairs(out var edges, edgeCounts, SmartLinks, JobHandle.CombineDependencies(constructEdgeCounts, collectSmartLinks), Allocator);

            var constructVertexIsBorderEdgeBits = ScheduleInitializeVertexIsBorderEdgeBits(meshData, edgeCounts, dependency, constructEdgeCounts);
            NativeList<ErrorQuadric> triangleErrorQuadrics = new(Allocator);
            var constructTriangleNormalsAndErrorQuadrics = ScheduleInitializeTriangleNormalsAndTriangleErrorQuadrics(meshData, triangleErrorQuadrics, dependency, constructVertexPositionBuffer, constructTriangles);

            var constructVertexErrorQuadrics = ScheduleInitializeVertexErrorQuadrics(meshData, edgeCounts, triangleErrorQuadrics, dependency, constructVertexPositionBuffer, constructTriangles, constructVertexContainingTrianglesAndTriangleDiscardedBits, constructEdgeCounts, constructTriangleNormalsAndErrorQuadrics);

            edgeCounts.Dispose(JobHandle.CombineDependencies(constructEdges, constructVertexIsBorderEdgeBits, constructVertexErrorQuadrics));

            triangleErrorQuadrics.Dispose(constructVertexErrorQuadrics);

            var constructVertexMerges = ScheduleInitializeVertexMerges(edges, constructVertexPositionBuffer, constructVertexErrorQuadrics, constructTriangleNormalsAndErrorQuadrics, constructVertexContainingTrianglesAndTriangleDiscardedBits, constructVertexIsBorderEdgeBits, constructEdges);

            edges.Dispose(constructVertexMerges);

            var constructVertexMergeOpponentVertices = ScheduleInitializeVertexMergeOpponentVertices(constructVertexMerges);

            return stackalloc[]
            {
                dependency,

                constructVertexPositionBuffer,

                constructVertexNormalBuffer,
                constructVertexTangentBuffer,

                constructVertexColorBuffer,

                constructVertexTexcoord0Buffer,
                constructVertexTexcoord1Buffer,
                constructVertexTexcoord2Buffer,
                constructVertexTexcoord3Buffer,
                constructVertexTexcoord4Buffer,
                constructVertexTexcoord5Buffer,
                constructVertexTexcoord6Buffer,
                constructVertexTexcoord7Buffer,

                constructVertexBlendWeightBuffer,
                constructVertexBlendIndicesBuffer,

                collectVertexSubMeshIndices,

                constructVertexErrorQuadrics,

                constructTriangles,
                constructTriangleNormalsAndErrorQuadrics,

                constructVertexContainingTrianglesAndTriangleDiscardedBits,
                constructVertexMergeOpponentVertices,

                constructVertexIsDiscardedBits,
                constructVertexIsBorderEdgeBits,

                collectSmartLinks,
                constructVertexMerges,

            }.CombineDependencies();

        }
        /// <summary>
        /// Creates and schedules a job that will simplify the mesh data..
        /// </summary>
        /// <param name="meshData">The mesh data to simplify. It must be the same with the mesh which was passed to <see cref="ScheduleLoadMeshData(Mesh.MeshData, MeshSimplifierOptions, JobHandle)"/>.</param>
        /// <param name="blendShapes">The blend shapes of the<paramref name="meshData"/>.</param>
        /// <param name="target">The simplification target for this mesh simplification.</param>
        /// <param name="destinationMeshData">The destination to write simplified mesh data.</param>
        /// <param name="destinationBlendShapes">The destination to write simplified blend shapes.</param>
        /// <param name="dependency">The handle of a job which the new job will depend upon.</param>
        /// <returns>The handle of the new job.</returns>
        /// <remarks>
        /// After you call <see cref="ScheduleLoadMeshData(Mesh.MeshData, MeshSimplifierOptions, JobHandle)"/>, you can call this method repeatedly to incrementally simplify the same mesh data with different targets.
        /// </remarks>
        public JobHandle ScheduleSimplify(Mesh.MeshData meshData, NativeList<BlendShapeData> blendShapes, MeshSimplificationTarget target, Mesh.MeshData destinationMeshData, NativeList<BlendShapeData> destinationBlendShapes, JobHandle dependency)
        {
            return new ExecuteProgressiveMeshSimplifyJob
            {
                Mesh = meshData,
                Target = target,
                DestinationMesh = destinationMeshData,
                DestinationBlendShapes = destinationBlendShapes,
                VertexPositionBuffer = VertexPositionBuffer.AsDeferredJobArray(),
                VertexNormalBuffer = VertexNormalBuffer.AsDeferredJobArray(),
                VertexTangentBuffer = VertexTangentBuffer.AsDeferredJobArray(),
                VertexColorBuffer = VertexColorBuffer.AsDeferredJobArray(),
                VertexTexCoord0Buffer = VertexTexCoord0Buffer.AsDeferredJobArray(),
                VertexTexCoord1Buffer = VertexTexCoord1Buffer.AsDeferredJobArray(),
                VertexTexCoord2Buffer = VertexTexCoord2Buffer.AsDeferredJobArray(),
                VertexTexCoord3Buffer = VertexTexCoord3Buffer.AsDeferredJobArray(),
                VertexTexCoord4Buffer = VertexTexCoord4Buffer.AsDeferredJobArray(),
                VertexTexCoord5Buffer = VertexTexCoord5Buffer.AsDeferredJobArray(),
                VertexTexCoord6Buffer = VertexTexCoord6Buffer.AsDeferredJobArray(),
                VertexTexCoord7Buffer = VertexTexCoord7Buffer.AsDeferredJobArray(),
                BlendShapes = blendShapes,
                VertexBlendWeightBuffer = VertexBlendWeightBuffer.AsDeferredJobArray(),
                VertexBlendIndicesBuffer = VertexBlendIndicesBuffer.AsDeferredJobArray(),
                VertexSubMeshIndices = VertexSubMeshIndices.AsDeferredJobArray(),
                Triangles = Triangles.AsDeferredJobArray(),
                TriangleNormals = TriangleNormals.AsDeferredJobArray(),
                VertexContainingTriangles = VertexContainingTriangles,
                VertexErrorQuadrics = VertexErrorQuadrics.AsDeferredJobArray(),
                VertexMergeOpponentVertices = VertexMergeOpponentVertices,
                DiscardedTriangle = TriangleIsDiscardedBits,
                DiscardedVertex = VertexIsDiscardedBits,
                PreserveVertex = VertexIsBorderEdgeBits,
                Options = Options,
                Merges = VertexMerges,
                BlendShapeDataAllocator = Allocator,
                SmartLinks = SmartLinks,
            }.Schedule(dependency);
        }
        /// <summary>
        /// Creates and schedules a job that will dispose this <see cref="MeshSimplifier"/>.
        /// </summary>
        /// <param name="inputDeps">The handle of a job which the new job will depend upon.</param>
        /// <returns>The handle of a new job that will dispose this <see cref="MeshSimplifier"/>. The new job depends upon <paramref name="inputDeps"/>.</returns>
        public JobHandle Dispose(JobHandle inputDeps)
        {
            return stackalloc[]
            {
                VertexPositionBuffer.Dispose(inputDeps),
                VertexNormalBuffer.Dispose(inputDeps),
                VertexTangentBuffer.Dispose(inputDeps),
                VertexColorBuffer.Dispose(inputDeps),
                VertexTexCoord0Buffer.Dispose(inputDeps),
                VertexTexCoord1Buffer.Dispose(inputDeps),
                VertexTexCoord2Buffer.Dispose(inputDeps),
                VertexTexCoord3Buffer.Dispose(inputDeps),
                VertexTexCoord4Buffer.Dispose(inputDeps),
                VertexTexCoord5Buffer.Dispose(inputDeps),

                VertexTexCoord6Buffer.Dispose(inputDeps),
                VertexTexCoord7Buffer.Dispose(inputDeps),

                VertexBlendWeightBuffer.Dispose(inputDeps),
                VertexBlendIndicesBuffer.Dispose(inputDeps),

                VertexSubMeshIndices.Dispose(inputDeps),

                VertexErrorQuadrics.Dispose(inputDeps),
                Triangles.Dispose(inputDeps),
                TriangleNormals.Dispose(inputDeps),
                VertexMergeOpponentVertices.Dispose(inputDeps),
                VertexContainingTriangles.Dispose(inputDeps),
                VertexIsDiscardedBits.Dispose(inputDeps),

                VertexIsBorderEdgeBits.Dispose(inputDeps),
                TriangleIsDiscardedBits.Dispose (inputDeps),
                SmartLinks.Dispose(inputDeps),
                VertexMerges.Dispose(inputDeps),
            }.CombineDependencies();
        }
        /// <summary>
        /// Disposes this <see cref="MeshSimplifier"/> and releases all its resources.
        /// </summary>
        public void Dispose()
        {
            VertexPositionBuffer.Dispose();
            VertexNormalBuffer.Dispose();
            VertexTangentBuffer.Dispose();
            VertexColorBuffer.Dispose();
            VertexTexCoord0Buffer.Dispose();
            VertexTexCoord1Buffer.Dispose();
            VertexTexCoord2Buffer.Dispose();
            VertexTexCoord3Buffer.Dispose();
            VertexTexCoord4Buffer.Dispose();
            VertexTexCoord5Buffer.Dispose();

            VertexTexCoord6Buffer.Dispose();
            VertexTexCoord7Buffer.Dispose();

            VertexBlendWeightBuffer.Dispose();
            VertexBlendIndicesBuffer.Dispose();

            VertexSubMeshIndices.Dispose();

            VertexErrorQuadrics.Dispose();
            Triangles.Dispose();
            TriangleNormals.Dispose();
            VertexMergeOpponentVertices.Dispose();
            VertexContainingTriangles.Dispose();
            VertexIsDiscardedBits.Dispose();

            VertexIsBorderEdgeBits.Dispose();
            TriangleIsDiscardedBits.Dispose();
            SmartLinks.Dispose();
            VertexMerges.Dispose();
        }
        

        JobHandle ScheduleCopyVertexPositionBuffer(Mesh.MeshData meshData, JobHandle meshDependency)
        {
            return new CopyVertexPositionBufferJob
            {
                Mesh = meshData,
                VertexPositionBuffer = VertexPositionBuffer,
            }.Schedule(meshDependency);
        }
        JobHandle ScheduleCopyVertexAttributeBufferAsFloat4(Mesh.MeshData meshData, VertexAttribute vertexAttribute, JobHandle meshDependency)
        {
            var targetBuffer = vertexAttribute switch
            {
                VertexAttribute.Normal => VertexNormalBuffer,
                VertexAttribute.Tangent => VertexTangentBuffer,
                VertexAttribute.Color => VertexColorBuffer,
                VertexAttribute.TexCoord0 => VertexTexCoord0Buffer,
                VertexAttribute.TexCoord1 => VertexTexCoord1Buffer,
                VertexAttribute.TexCoord2 => VertexTexCoord2Buffer,
                VertexAttribute.TexCoord3 => VertexTexCoord3Buffer,
                VertexAttribute.TexCoord4 => VertexTexCoord4Buffer,
                VertexAttribute.TexCoord5 => VertexTexCoord5Buffer,
                VertexAttribute.TexCoord6 => VertexTexCoord6Buffer,
                VertexAttribute.TexCoord7 => VertexTexCoord7Buffer,
                _ => throw new ArgumentOutOfRangeException(nameof(vertexAttribute)),
            };
            return new CopyVertexAttributeBufferAsFloat4Job
            {
                Mesh = meshData,
                VertexAttribute = vertexAttribute,
                VertexAttributeBuffer = targetBuffer,
            }.Schedule(meshDependency);
        }
        JobHandle ScheduleCopyVertexBlendWeightBuffer(Mesh.MeshData meshData, JobHandle meshDependency)
        {
            return new CopyVertexBlendWeightBufferJob
            {
                Mesh = meshData,
                VertexBlendWeightBuffer = VertexBlendWeightBuffer,
            }.Schedule(meshDependency);
        }
        JobHandle ScheduleCopyVertexBlendIndicesBuffer(Mesh.MeshData meshData, JobHandle meshDependency)
        {
            return new CopyVertexBlendIndicesBufferJob
            {
                Mesh = meshData,
                VertexBlendIndicesBuffer = VertexBlendIndicesBuffer,
            }.Schedule(meshDependency);
        }
        JobHandle ScheduleCollectVertexSubMeshIndices(Mesh.MeshData mesh, JobHandle meshDependency)
        {
            VertexSubMeshIndices.ResizeUninitialized(mesh.vertexCount);
            return new CollectVertexSubMeshIndicesJob
            {
                Mesh = mesh,
                VertexSubMeshIndices = VertexSubMeshIndices.AsDeferredJobArray(),
            }.Schedule(meshDependency);
        }
        JobHandle ScheduleCopyTriangles(Mesh.MeshData mesh, JobHandle meshDependency)
        {
            return new CopyTrianglesJob
            {
                Mesh = mesh,
                Triangles = Triangles,
            }.Schedule(meshDependency);
        }
        JobHandle ScheduleInitializeVertexContainingTrianglesAndTriangleIsDiscardedBits(JobHandle trianglesDependency)
        {
            return new CollectVertexContainingTrianglesAndMarkInvalidTrianglesJob
            {
                Triangles = Triangles.AsDeferredJobArray(),
                VertexContainingTriangles = VertexContainingTriangles,
                TriangleIsDiscardedBits = TriangleIsDiscardedBits,
            }.Schedule(trianglesDependency);
        }
        static JobHandle ScheduleConstructEdgeCounts(out NativeHashMap<int2, int> edgeCounts, NativeList<int3> triangles, JobHandle dependency, AllocatorManager.AllocatorHandle allocator)
        {
            edgeCounts = new(0, allocator);
            return new CountEdgesJob
            {
                Triangles = triangles.AsDeferredJobArray(),
                EdgeCounts = edgeCounts,
            }.Schedule(dependency);
        }
        JobHandle ScheduleInitializeVertexIsDiscardedBits(
            Mesh.MeshData mesh,
            JobHandle meshDependency,
            JobHandle vertexContainingTrianglesDependency)
        {
            return new FindNonReferencedVerticesJob
            {
                Mesh = mesh,
                VertexContainingTriangles = VertexContainingTriangles,
                VertexIsDiscardedBits = VertexIsDiscardedBits,
            }.Schedule(JobHandle.CombineDependencies(meshDependency, vertexContainingTrianglesDependency));
        }
        static JobHandle ScheduleCollectSmartLinks(
            Mesh.MeshData mesh,

            NativeList<float3> vertexPositionBuffer,
            NativeList<float4> vertexNormalBuffer,
            NativeList<float4> vertexColorBuffer,
            NativeList<float4> vertexTexcoord0Buffer,
            NativeList<float4> vertexTexcoord1Buffer,
            NativeList<float4> vertexTexcoord2Buffer,
            NativeList<float4> vertexTexcoord3Buffer,
            NativeList<float4> vertexTexcoord4Buffer,
            NativeList<float4> vertexTexcoord5Buffer,
            NativeList<float4> vertexTexcoord6Buffer,
            NativeList<float4> vertexTexcoord7Buffer,
            NativeBitArray vertexIsDiscardedBits,
            MeshSimplifierOptions options,
            NativeHashSet<int2> smartLinks,
            JobHandle meshDependency,
            JobHandle vertexPositionBufferDependency,
            JobHandle otherVertexAttributeBufferDependency,
            JobHandle vertexIsDiscardedBitsDependency,
            AllocatorManager.AllocatorHandle allocator
            )
        {

            NativeList<UnsafeList<int2>> subMeshSmartLinkLists = new(allocator);
            var initializeSubMeshSmartLinkLists = new InitializeSubMeshListJob<UnsafeList<int2>>
            {
                MeshData = mesh,
                Options = NativeArrayOptions.UninitializedMemory,
                Buffer = subMeshSmartLinkLists,
            }.Schedule(meshDependency);
            var collectNeighborVertexPairs = new CollectNeighborVertexPairsJob
            {
                Mesh = mesh,
                VertexPositionBuffer = vertexPositionBuffer.AsDeferredJobArray(),
                VertexIsDiscardedBits = vertexIsDiscardedBits,
                Options = options,
                SubMeshSmartLinkListAllocator = allocator,
                SubMeshSmartLinkLists = subMeshSmartLinkLists.AsDeferredJobArray(),
            }.Schedule(subMeshSmartLinkLists, 1, JobHandle.CombineDependencies(initializeSubMeshSmartLinkLists, vertexPositionBufferDependency, vertexIsDiscardedBitsDependency));
            var removeHighCostSmartLinks = new RemoveHighCostSmartLinksJob
            {
                VertexNormalBuffer = vertexNormalBuffer.AsDeferredJobArray(),
                VertexColorBuffer = vertexColorBuffer.AsDeferredJobArray(),
                VertexTexCoord0Buffer = vertexTexcoord0Buffer.AsDeferredJobArray(),
                VertexTexCoord1Buffer = vertexTexcoord1Buffer.AsDeferredJobArray(),
                VertexTexCoord2Buffer = vertexTexcoord2Buffer.AsDeferredJobArray(),
                VertexTexCoord3Buffer = vertexTexcoord3Buffer.AsDeferredJobArray(),
                VertexTexCoord4Buffer = vertexTexcoord4Buffer.AsDeferredJobArray(),
                VertexTexCoord5Buffer = vertexTexcoord5Buffer.AsDeferredJobArray(),
                VertexTexCoord6Buffer = vertexTexcoord6Buffer.AsDeferredJobArray(),
                VertexTexCoord7Buffer = vertexTexcoord7Buffer.AsDeferredJobArray(),

                Options = options,
                SubMeshSmartLinkLists = subMeshSmartLinkLists.AsDeferredJobArray(),
            }.Schedule(subMeshSmartLinkLists, 1, JobHandle.CombineDependencies(collectNeighborVertexPairs, otherVertexAttributeBufferDependency));

            var collectSmartLinks = new CollectSmartLinksJob
            {
                SubMeshSmartLinkLists = subMeshSmartLinkLists.AsDeferredJobArray(),
                SmartLinks = smartLinks,
            }.Schedule(removeHighCostSmartLinks);
            subMeshSmartLinkLists.Dispose(collectSmartLinks);
            return collectSmartLinks;
        }
        static JobHandle ScheduleConstructMergePairs(out NativeList<int2> mergePairs, NativeHashMap<int2, int> edgeCounts, NativeHashSet<int2> smartLinks, JobHandle dependency, AllocatorManager.AllocatorHandle allocator)
        {
            mergePairs = new(allocator);
            return new CollectMergePairsAndSmartLinksJob
            {
                EdgeCounts = edgeCounts,
                SmartLinks = smartLinks,
                MergePairs = mergePairs,
            }.Schedule(dependency);
        }

        JobHandle ScheduleInitializeVertexIsBorderEdgeBits(Mesh.MeshData mesh, NativeHashMap<int2, int> edgeCounts, JobHandle meshDependency, JobHandle edgeCountsDependency)
        {
            return new MarkBorderEdgeVerticesJob
            {
                Mesh = mesh,
                EdgeCounts = edgeCounts,
                VertexIsBorderEdgeBits = VertexIsBorderEdgeBits,
            }.Schedule(JobHandle.CombineDependencies(meshDependency, edgeCountsDependency));
        }
        
        
        JobHandle ScheduleInitializeTriangleNormalsAndTriangleErrorQuadrics(
            Mesh.MeshData mesh,
            NativeList<ErrorQuadric> triangleErrorQuadrics,
            JobHandle meshDependency,
            JobHandle vertexPositionBufferDependency,
            JobHandle trianglesDependency)
        {

            var initializeTriangleNormalsJob = new InitializeTriangleListJob<float3>
            {
                MeshData = mesh,
                Options = NativeArrayOptions.UninitializedMemory,
                Buffer = TriangleNormals,
            }.Schedule(meshDependency);

            var initializeTriangleErrorQuadricsJob = new InitializeTriangleListJob<ErrorQuadric>
            {
                MeshData = mesh,
                Options = NativeArrayOptions.UninitializedMemory,
                Buffer = triangleErrorQuadrics,
            }.Schedule(meshDependency);

            var computeTriangleNormalsAndErrorQuadrics = new ComputeTriangleNormalsAndErrorQuadricsJob
            {
                VertexPositionBuffer = VertexPositionBuffer.AsDeferredJobArray(),
                Triangles = Triangles.AsDeferredJobArray(),
                TriangleNormals = TriangleNormals.AsDeferredJobArray(),
                TriangleErrorQuadrics = triangleErrorQuadrics.AsDeferredJobArray(),
            }.Schedule(
                Triangles,
            JobsUtility.CacheLineSize,
            stackalloc[]
            {
                vertexPositionBufferDependency,
                trianglesDependency,
                initializeTriangleNormalsJob,
                initializeTriangleErrorQuadricsJob,
            }.CombineDependencies());

            return computeTriangleNormalsAndErrorQuadrics;
        }

        JobHandle ScheduleInitializeVertexErrorQuadrics(
            Mesh.MeshData mesh,
            NativeHashMap<int2, int> edgeCounts,
            NativeList<ErrorQuadric> triangleErrorQuadrics,
            JobHandle meshDependency,
            JobHandle vertexPositionBufferDependency,
            JobHandle trianglesDependency,
            JobHandle vertexContainingTrianglesDependency,
            JobHandle edgeCountsDependency,
            JobHandle triangleErrorQuadricsDependency)
        {

            var initializeVertexErrorQuadricsJob = new InitializeVertexListJob<ErrorQuadric>
            {
                MeshData = mesh,
                Options = NativeArrayOptions.UninitializedMemory,
                Buffer = VertexErrorQuadrics,
            }.Schedule(meshDependency);

            var computeVertexErrorQuadricsJob = new ComputeVertexErrorQuadricsJob
            {
                VertexPositionBuffer = VertexPositionBuffer.AsDeferredJobArray(),
                Triangles = Triangles.AsDeferredJobArray(),
                VertexContainingTriangles = VertexContainingTriangles,
                EdgeCounts = edgeCounts,
                TriangleErrorQuadrics = triangleErrorQuadrics.AsDeferredJobArray(),
                VertexErrorQuadrics = VertexErrorQuadrics.AsDeferredJobArray(),
            }.Schedule(VertexErrorQuadrics, JobsUtility.CacheLineSize,
            stackalloc[]
            {
                vertexPositionBufferDependency,
                trianglesDependency,
                vertexContainingTrianglesDependency,
                edgeCountsDependency,
                triangleErrorQuadricsDependency,
                initializeVertexErrorQuadricsJob,
            }.CombineDependencies());

            return computeVertexErrorQuadricsJob;
        }
        JobHandle ScheduleInitializeVertexMerges(
            NativeList<int2> edges,
            JobHandle vertexPositionBufferDependency,
            JobHandle vertexErrorQuadricsDependency,
            JobHandle triangleNormalsDependency,
            JobHandle vertexContainingTrianglesDependency,
            JobHandle vertexIsBorderEdgeBitsDependency,
            JobHandle edgesDependency
            )
        {

            var initializeVertexMergesJob = new InitializeVertexMergesJob
            {
                Edges = edges.AsDeferredJobArray(),
                VertexMerges = VertexMerges,
            }.Schedule(edgesDependency);

            var computeMergesJob = new ComputeMergesJob
            {
                VertexPositionBuffer = VertexPositionBuffer.AsDeferredJobArray(),
                VertexErrorQuadrics = VertexErrorQuadrics.AsDeferredJobArray(),
                TriangleNormals = TriangleNormals.AsDeferredJobArray(),
                VertexContainingTriangles = VertexContainingTriangles,
                VertexIsBorderEdgeBits = VertexIsBorderEdgeBits,
                Edges = edges.AsDeferredJobArray(),
                VertexMerges = VertexMerges.AsDeferredJobArray(),
                Options = Options,
            }.Schedule(edges, JobsUtility.CacheLineSize,
            stackalloc[]
            {
                vertexPositionBufferDependency,
                vertexErrorQuadricsDependency,
                triangleNormalsDependency,
                vertexContainingTrianglesDependency,
                vertexIsBorderEdgeBitsDependency,
                edgesDependency,
                initializeVertexMergesJob,
            }.CombineDependencies());

            var removeInvalidMergesJob = new RemoveInvalidMergesJob
            {
                VertexMerges = VertexMerges,
            }.Schedule(computeMergesJob);

            return removeInvalidMergesJob;
        }

        JobHandle ScheduleInitializeVertexMergeOpponentVertices(
            JobHandle vertexMergesDependency)
        {
            var collectVertexMergeOpponentsJob = new CollectVertexMergeOpponmentsJob
            {
                VertexMerges = VertexMerges.AsDeferredJobArray(),
                VertexMergeOpponentVertices = VertexMergeOpponentVertices,
            }.Schedule(vertexMergesDependency);

            return collectVertexMergeOpponentsJob;
        }
        
        
    }
}
