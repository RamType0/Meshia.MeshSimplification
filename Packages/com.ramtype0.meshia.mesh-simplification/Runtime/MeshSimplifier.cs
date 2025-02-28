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
    public static partial class MeshSimplifier
    {
        public static void Simplify(Mesh mesh, int targetVertexCount, MeshSimplifierOptions options, Mesh destination)
        {
            Allocator allocator = Allocator.TempJob;
            var originalMeshDataArray = Mesh.AcquireReadOnlyMeshData(mesh);
            var originalBlendShapes = BlendShapeData.GetMeshBlendShapes(mesh, allocator);
            NativeList<int> targetVertexCounts = new(1, allocator)
            {
                targetVertexCount
            };
            var simplifiedMeshDataArray = Mesh.AllocateWritableMeshData(1);
            NativeList<UnsafeList<BlendShapeData>> simplifiedMeshesBlendShapes = new(allocator);
            var jobHandle = ScheduleSimplify(originalMeshDataArray[0], originalBlendShapes, targetVertexCounts, options, simplifiedMeshDataArray, simplifiedMeshesBlendShapes, new(), allocator);

            JobHandle.ScheduleBatchedJobs();
            jobHandle.Complete();

            originalMeshDataArray.Dispose();
            ApplySimplifiedMesh(mesh, simplifiedMeshDataArray, simplifiedMeshesBlendShapes, destination);
            BlendShapeData.Dispose(simplifiedMeshesBlendShapes);
        }
        public static async Task SimplifyAsync(Mesh mesh, int targetVertexCount, MeshSimplifierOptions options, Mesh destination, CancellationToken cancellationToken = default)
        {
            Allocator allocator = Allocator.Persistent;
            var originalMeshDataArray = Mesh.AcquireReadOnlyMeshData(mesh);
            var originalBlendShapes = BlendShapeData.GetMeshBlendShapes(mesh, allocator);
            NativeList<int> targetVertexCounts = new(1, allocator)
            {
                targetVertexCount
            };
            var simplifiedMeshDataArray = Mesh.AllocateWritableMeshData(1);
            NativeList<UnsafeList<BlendShapeData>> simplifiedMeshesBlendShapes = new(allocator);
            var jobHandle = ScheduleSimplify(originalMeshDataArray[0], originalBlendShapes, targetVertexCounts, options, simplifiedMeshDataArray, simplifiedMeshesBlendShapes, new(), allocator);

            JobHandle.ScheduleBatchedJobs();
            while (!jobHandle.IsCompleted)
            {
                await Task.Yield();
            }
            jobHandle.Complete();
            originalMeshDataArray.Dispose();
            if (cancellationToken.IsCancellationRequested)
            {
                simplifiedMeshDataArray.Dispose();
            }
            else
            {
                ApplySimplifiedMesh(mesh, simplifiedMeshDataArray, simplifiedMeshesBlendShapes, destination);
            }

            BlendShapeData.Dispose(simplifiedMeshesBlendShapes);
        }

        private static void ApplySimplifiedMesh(Mesh mesh, Mesh.MeshDataArray simplifiedMeshDataArray, NativeList<UnsafeList<BlendShapeData>> simplifiedMeshesBlendShapes, Mesh destination)
        {
            Mesh.ApplyAndDisposeWritableMeshData(simplifiedMeshDataArray, destination, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds);

            if(mesh != destination)
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
                if(bindposes.Length != 0)
                {
                    destination.bindposes = bindposes;
                }
#endif
            }

            BlendShapeData.SetBlendShapes(destination, simplifiedMeshesBlendShapes[0]);
        }

        public static JobHandle ScheduleSimplify(Mesh.MeshData mesh, NativeList<BlendShapeData> blendShapes, NativeList<int> targetVertexCounts, MeshSimplifierOptions options, Mesh.MeshDataArray simplifiedMeshes, NativeList<UnsafeList<BlendShapeData>> simplifiedMeshesBlendShapes, JobHandle inputDeps, AllocatorManager.AllocatorHandle allocator)

        {
            //MeshDataAssert.VertexPositionIsFloat3(mesh);
            //MeshDataAssert.AllSubMeshTopologyIsTriangles(mesh);
            //MeshDataAssert.HasNoInvalidBlendWeights(mesh);

            


            var constructVertexPositionBuffer = ScheduleConstructVertexPositionBuffer(out var vertexPositionBuffer, mesh, inputDeps, allocator);
            var constructVertexNormalBuffer = ScheduleConstructVertexAttributeBufferAsFloat4(out var vertexNormalBuffer, mesh, VertexAttribute.Normal, inputDeps, allocator);
            var constructVertexTangentBuffer = ScheduleConstructVertexAttributeBufferAsFloat4(out var vertexTangentBuffer, mesh, VertexAttribute.Tangent, inputDeps, allocator);
            var constructVertexColorBuffer = ScheduleConstructVertexAttributeBufferAsFloat4(out var vertexColorBuffer, mesh, VertexAttribute.Color, inputDeps, allocator);
            var constructVertexTexcoord0Buffer = ScheduleConstructVertexAttributeBufferAsFloat4(out var vertexTexCoord0Buffer, mesh, VertexAttribute.TexCoord0, inputDeps, allocator);    
            var constructVertexTexcoord1Buffer = ScheduleConstructVertexAttributeBufferAsFloat4(out var vertexTexCoord1Buffer, mesh, VertexAttribute.TexCoord1, inputDeps, allocator);
            var constructVertexTexcoord2Buffer = ScheduleConstructVertexAttributeBufferAsFloat4(out var vertexTexCoord2Buffer, mesh, VertexAttribute.TexCoord2, inputDeps, allocator);
            var constructVertexTexcoord3Buffer = ScheduleConstructVertexAttributeBufferAsFloat4(out var vertexTexCoord3Buffer, mesh, VertexAttribute.TexCoord3, inputDeps, allocator);
            var constructVertexTexcoord4Buffer = ScheduleConstructVertexAttributeBufferAsFloat4(out var vertexTexCoord4Buffer, mesh, VertexAttribute.TexCoord4, inputDeps, allocator);
            var constructVertexTexcoord5Buffer = ScheduleConstructVertexAttributeBufferAsFloat4(out var vertexTexCoord5Buffer, mesh, VertexAttribute.TexCoord5, inputDeps, allocator);
            var constructVertexTexcoord6Buffer = ScheduleConstructVertexAttributeBufferAsFloat4(out var vertexTexCoord6Buffer, mesh, VertexAttribute.TexCoord6, inputDeps, allocator);
            var constructVertexTexcoord7Buffer = ScheduleConstructVertexAttributeBufferAsFloat4(out var vertexTexCoord7Buffer, mesh, VertexAttribute.TexCoord7, inputDeps, allocator);
            var constructVertexBlendWeightBuffer = ScheduleConstructVertexBlendWeightBuffer(out var vertexBlendWeightBuffer, mesh, inputDeps, allocator);
            var constructVertexBlendIndicesBuffer = ScheduleConstructVertexBlendIndicesBuffer(out var vertexBlendIndicesBuffer, mesh, inputDeps, allocator);

            var constructTriangles = ScheduleConstructTriangles(out var triangles, mesh, inputDeps, allocator);

            var constructVertexContainingTrianglesAndTriangleDiscardedBits = ScheduleConstructVertexContainingTrianglesAndTriangleIsDiscardedBits(out var vertexContainingTriangles, out var triangleIsDiscardedBits, triangles, constructTriangles, allocator);

            var constructEdgeCounts = ScheduleConstructEdgeCounts(out var edgeCounts, triangles, constructTriangles, allocator);

            NativeHashSet<int2> smartLinks = new(0, allocator);

            var collectSmartLinks = options.EnableSmartLink
                ? ScheduleCollectSmartLinks(
                    mesh,
                    vertexPositionBuffer,
                    vertexNormalBuffer,
                    vertexColorBuffer,
                    vertexTexCoord0Buffer,
                    vertexTexCoord1Buffer,
                    vertexTexCoord2Buffer,
                    vertexTexCoord3Buffer,
                    vertexTexCoord4Buffer,
                    vertexTexCoord5Buffer,
                    vertexTexCoord6Buffer,
                    vertexTexCoord7Buffer,
                    options,

                    smartLinks,
                    inputDeps,
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
                    allocator
                    )
                : new JobHandle();


            var constructEdges = ScheduleConstructMergePairs(out var edges, edgeCounts, smartLinks, JobHandle.CombineDependencies(constructEdgeCounts, collectSmartLinks), allocator);

            var constructVertexIsBorderEdgeBits = ScheduleConstructVertexIsBorderEdgeBits(out var vertexIsBorderEdgeBits, mesh, edgeCounts, constructEdgeCounts, allocator);

            var constructTriangleNormalsAndErrorQuadrics = ScheduleConstructTriangleNormalsAndTriangleErrorQuadrics(
                out var triangleNormals, 
                out var triangleErrorQuadrics, 
                
                mesh, vertexPositionBuffer, triangles, 
                JobHandle.CombineDependencies(constructVertexPositionBuffer, constructTriangles), 
                allocator);

            var constructVertexErrorQuadrics = ScheduleConstructVertexErrorQuadrics(
                out var vertexErrorQuadrics, 
                mesh, 
                vertexPositionBuffer, 
                triangles, 
                vertexContainingTriangles, 
                edgeCounts, 
                triangleErrorQuadrics,
                stackalloc[]
                {
                    constructVertexPositionBuffer,
                    constructTriangles,
                    constructVertexContainingTrianglesAndTriangleDiscardedBits,
                    constructEdgeCounts,
                    constructTriangleNormalsAndErrorQuadrics,
                }.CombineDependencies(),
                allocator);

            edgeCounts.Dispose(JobHandle.CombineDependencies(constructEdges, constructVertexIsBorderEdgeBits, constructVertexErrorQuadrics));

            triangleErrorQuadrics.Dispose(constructVertexErrorQuadrics);

            var constructMerges = ScheduleConstructMerges(
                out var vertexMerges, 
                vertexPositionBuffer, 
                vertexErrorQuadrics, 
                vertexContainingTriangles, 
                vertexIsBorderEdgeBits, 
                triangleNormals, 
                edges, 
                options,
                stackalloc[]
                {
                    constructVertexPositionBuffer,
                    constructVertexErrorQuadrics,
                    constructVertexContainingTrianglesAndTriangleDiscardedBits,
                    constructVertexIsBorderEdgeBits,
                    constructTriangleNormalsAndErrorQuadrics,
                    constructEdges,
                }.CombineDependencies(),
                allocator);

            edges.Dispose(constructMerges);

            var constructVertexMergeOpponentVertices = ScheduleConstructVertexMergeOpponentVertices(out var vertexMergeOpponentVertices, vertexMerges, constructMerges, allocator);

            var constructVertexIsDiscardedBits = ScheduleConstructVertexIsDiscardedBits(out var vertexIsDiscardedBits, mesh, vertexContainingTriangles, constructVertexContainingTrianglesAndTriangleDiscardedBits, allocator);

            var executeProgressiveMeshSimplify = new ExecuteProgressiveMeshSimplifyJob
            {
                Mesh = mesh,
                TargetVertexCounts = targetVertexCounts.AsArray(),
                SimplifiedMeshes = simplifiedMeshes,
                SimplifiedMeshesBlendShapes = simplifiedMeshesBlendShapes,
                VertexPositionBuffer = vertexPositionBuffer.AsDeferredJobArray(),
                VertexNormalBuffer = vertexNormalBuffer.AsDeferredJobArray(),
                VertexTangentBuffer = vertexTangentBuffer.AsDeferredJobArray(),
                VertexColorBuffer = vertexColorBuffer.AsDeferredJobArray(),
                VertexTexCoord0Buffer = vertexTexCoord0Buffer.AsDeferredJobArray(),
                VertexTexCoord1Buffer = vertexTexCoord1Buffer.AsDeferredJobArray(),
                VertexTexCoord2Buffer = vertexTexCoord2Buffer.AsDeferredJobArray(),
                VertexTexCoord3Buffer = vertexTexCoord3Buffer.AsDeferredJobArray(),
                VertexTexCoord4Buffer = vertexTexCoord4Buffer.AsDeferredJobArray(),
                VertexTexCoord5Buffer = vertexTexCoord5Buffer.AsDeferredJobArray(),
                VertexTexCoord6Buffer = vertexTexCoord6Buffer.AsDeferredJobArray(),
                VertexTexCoord7Buffer = vertexTexCoord7Buffer.AsDeferredJobArray(),
                BlendShapes = blendShapes,
                VertexBlendWeightBuffer = vertexBlendWeightBuffer.AsDeferredJobArray(),
                VertexBlendIndicesBuffer = vertexBlendIndicesBuffer.AsDeferredJobArray(),
                Triangles = triangles.AsDeferredJobArray(),
                TriangleNormals = triangleNormals.AsDeferredJobArray(),
                VertexContainingTriangles = vertexContainingTriangles,
                VertexErrorQuadrics = vertexErrorQuadrics.AsDeferredJobArray(),
                VertexMergeOpponentVertices = vertexMergeOpponentVertices,
                DiscardedTriangle = triangleIsDiscardedBits,
                DiscardedVertex = vertexIsDiscardedBits,
                PreserveVertex = vertexIsBorderEdgeBits,
                Options = options,
                Merges = vertexMerges,
                BlendShapeDataAllocator = allocator,
                SmartLinks = smartLinks,
            }.Schedule(stackalloc[]
            {
                inputDeps,

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

                constructTriangles,
                constructVertexContainingTrianglesAndTriangleDiscardedBits,
                constructVertexErrorQuadrics,
                constructVertexMergeOpponentVertices,
                constructVertexIsDiscardedBits,
                constructVertexIsBorderEdgeBits,
                constructMerges,
                collectSmartLinks,
            }.CombineDependencies());
            
            targetVertexCounts.Dispose(executeProgressiveMeshSimplify);
            vertexPositionBuffer.Dispose(executeProgressiveMeshSimplify);
            vertexNormalBuffer.Dispose(executeProgressiveMeshSimplify);
            vertexTangentBuffer.Dispose(executeProgressiveMeshSimplify);
            vertexColorBuffer.Dispose(executeProgressiveMeshSimplify);
            vertexTexCoord0Buffer.Dispose(executeProgressiveMeshSimplify);
            vertexTexCoord1Buffer.Dispose(executeProgressiveMeshSimplify);
            vertexTexCoord2Buffer.Dispose(executeProgressiveMeshSimplify);
            vertexTexCoord3Buffer.Dispose(executeProgressiveMeshSimplify);
            vertexTexCoord4Buffer.Dispose(executeProgressiveMeshSimplify);
            vertexTexCoord5Buffer.Dispose(executeProgressiveMeshSimplify);
            vertexTexCoord6Buffer.Dispose(executeProgressiveMeshSimplify);
            vertexTexCoord7Buffer.Dispose(executeProgressiveMeshSimplify);
            vertexBlendWeightBuffer.Dispose(executeProgressiveMeshSimplify);
            vertexBlendIndicesBuffer.Dispose(executeProgressiveMeshSimplify);

            var disposeBlendShapesJob = new DisposeBlendShapesJob
            {
                BlendShapes = blendShapes,
            }.Schedule(executeProgressiveMeshSimplify);
            blendShapes.Dispose(disposeBlendShapesJob);
            
            triangles.Dispose(executeProgressiveMeshSimplify);
            triangleNormals.Dispose(executeProgressiveMeshSimplify);
            vertexErrorQuadrics.Dispose(executeProgressiveMeshSimplify);
            vertexContainingTriangles.Dispose(executeProgressiveMeshSimplify);

            smartLinks.Dispose(executeProgressiveMeshSimplify);

            vertexMergeOpponentVertices.Dispose(executeProgressiveMeshSimplify);

            triangleIsDiscardedBits.Dispose(executeProgressiveMeshSimplify);
            vertexIsDiscardedBits.Dispose(executeProgressiveMeshSimplify);

            vertexIsBorderEdgeBits.Dispose(executeProgressiveMeshSimplify);
            vertexMerges.Dispose(executeProgressiveMeshSimplify);

            return executeProgressiveMeshSimplify;
        }

        static JobHandle ScheduleConstructVertexPositionBuffer(out NativeList<float3> vertexPositionBuffer, Mesh.MeshData mesh, JobHandle dependency, AllocatorManager.AllocatorHandle allocator)
        {
            vertexPositionBuffer = new(allocator);
            return new CopyVertexPositionBufferJob
            {
                Mesh = mesh,
                VertexPositionBuffer = vertexPositionBuffer,
            }.Schedule(dependency);
        }

        static JobHandle ScheduleConstructVertexNormalBuffer(out NativeList<float3> vertexNormalBuffer, Mesh.MeshData mesh, JobHandle dependency, AllocatorManager.AllocatorHandle allocator)
        {
            vertexNormalBuffer = new(allocator);
            return new CopyVertexNormalBufferJob
            {
                Mesh = mesh,
                VertexNormalBuffer = vertexNormalBuffer,
            }.Schedule(dependency);
        }

        static JobHandle ScheduleConstructVertexAttributeBufferAsFloat4(out NativeList<float4> vertexAttributeBuffer, Mesh.MeshData mesh, VertexAttribute vertexAttribute, JobHandle dependency, AllocatorManager.AllocatorHandle allocator)
        {
            vertexAttributeBuffer = new(allocator);
            return new CopyVertexAttributeBufferAsFloat4Job
            {
                Mesh = mesh,
                VertexAttribute = vertexAttribute,
                VertexAttributeBuffer = vertexAttributeBuffer,
            }.Schedule(dependency);
        }

        static JobHandle ScheduleConstructVertexBlendWeightBuffer(out NativeList<float> vertexBlendWeightBuffer, Mesh.MeshData mesh, JobHandle dependency, AllocatorManager.AllocatorHandle allocator)
        {
            vertexBlendWeightBuffer = new(allocator);
            return new CopyVertexBlendWeightBufferJob
            {
                Mesh = mesh,
                VertexBlendWeightBuffer = vertexBlendWeightBuffer,
            }.Schedule(dependency);
        }

        static JobHandle ScheduleConstructVertexBlendIndicesBuffer(out NativeList<uint> vertexBlendIndicesBuffer, Mesh.MeshData mesh, JobHandle dependency, AllocatorManager.AllocatorHandle allocator)
        {
            vertexBlendIndicesBuffer = new(allocator);
            return new CopyVertexBlendIndicesBufferJob
            {
                Mesh = mesh,
                VertexBlendIndicesBuffer = vertexBlendIndicesBuffer,
            }.Schedule(dependency);
        }

        static JobHandle ScheduleConstructTriangles(out NativeList<int3> triangles, Mesh.MeshData mesh, JobHandle dependency, AllocatorManager.AllocatorHandle allocator)
        {
            triangles = new(allocator);
            return new CopyTrianglesJob
            {
                Mesh = mesh,
                Triangles = triangles,
            }.Schedule(dependency);
        }

        static JobHandle ScheduleConstructVertexContainingTrianglesAndTriangleIsDiscardedBits(out NativeParallelMultiHashMap<int, int> vertexContainingTriangles, out NativeBitArray triangleIsDiscardedBits, NativeList<int3> triangles, JobHandle dependency, AllocatorManager.AllocatorHandle allocator)
        {
            vertexContainingTriangles = new(0, allocator);
            triangleIsDiscardedBits= new(0, allocator, NativeArrayOptions.UninitializedMemory);
            return new CollectVertexContainingTrianglesAndMarkInvalidTrianglesJob
            {
                Triangles = triangles.AsDeferredJobArray(),
                VertexContainingTriangles = vertexContainingTriangles,
                TriangleIsDiscardedBits = triangleIsDiscardedBits,
            }.Schedule(dependency);
        }

        static JobHandle ScheduleConstructVertexIsDiscardedBits(out NativeBitArray vertexIsDiscardedBits, Mesh.MeshData mesh, NativeParallelMultiHashMap<int, int> vertexContainingTriangles, JobHandle dependency, AllocatorManager.AllocatorHandle allocator)
        {
            vertexIsDiscardedBits = new(0, allocator, NativeArrayOptions.UninitializedMemory);
            return new FindNonReferencedVerticesJob
            {
                Mesh = mesh,
                VertexContainingTriangles = vertexContainingTriangles,
                VertexIsDiscardedBits = vertexIsDiscardedBits,
            }.Schedule(dependency);
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
        static JobHandle ScheduleConstructVertexIsBorderEdgeBits(out NativeBitArray vertexIsBorderEdgeBits, Mesh.MeshData mesh, NativeHashMap<int2, int> edgeCounts, JobHandle dependency, AllocatorManager.AllocatorHandle allocator)
        {
            vertexIsBorderEdgeBits = new(0, allocator, NativeArrayOptions.UninitializedMemory);
            return new MarkBorderEdgeVerticesJob
            {
                Mesh = mesh,
                EdgeCounts = edgeCounts,
                VertexIsBorderEdgeBits = vertexIsBorderEdgeBits,
            }.Schedule(dependency);
        }
        static JobHandle ScheduleConstructTriangleNormalsAndTriangleErrorQuadrics(
            out NativeList<float3> triangleNormals,
            out NativeList<ErrorQuadric> triangleErrorQuadrics,
            Mesh.MeshData mesh,
            NativeList<float3> vertexPositionBuffer,
            NativeList<int3> triangles,
            JobHandle dependency,
            AllocatorManager.AllocatorHandle allocator)
        {
            triangleNormals = new(allocator);
            triangleErrorQuadrics = new(allocator);

            var initializeTriangleNormalsJob = new InitializeTriangleListJob<float3>
            {
                MeshData = mesh,
                Options = NativeArrayOptions.UninitializedMemory,
                Buffer = triangleNormals,
            }.Schedule(dependency);

            var initializeTriangleErrorQuadricsJob = new InitializeTriangleListJob<ErrorQuadric>
            {
                MeshData = mesh,
                Options = NativeArrayOptions.UninitializedMemory,
                Buffer = triangleErrorQuadrics,
            }.Schedule(dependency);

            var computeTriangleNormalsAndErrorQuadrics = new ComputeTriangleNormalsAndErrorQuadricsJob
            {
                VertexPositionBuffer = vertexPositionBuffer.AsDeferredJobArray(),
                Triangles = triangles.AsDeferredJobArray(),
                TriangleNormals = triangleNormals.AsDeferredJobArray(),
                TriangleErrorQuadrics = triangleErrorQuadrics.AsDeferredJobArray(),
            }.Schedule(
                triangles, 
            JobsUtility.CacheLineSize, 
            JobHandle.CombineDependencies(dependency, initializeTriangleNormalsJob, initializeTriangleErrorQuadricsJob));

            return computeTriangleNormalsAndErrorQuadrics;
        }

        static JobHandle ScheduleConstructVertexErrorQuadrics(
            out NativeList<ErrorQuadric> vertexErrorQuadrics,
            Mesh.MeshData mesh,
            NativeList<float3> vertexPositionBuffer,
            NativeList<int3> triangles,
            NativeParallelMultiHashMap<int, int> vertexContainingTriangles,
            NativeHashMap<int2, int> edgeCounts,
            NativeList<ErrorQuadric> triangleErrorQuadrics,
            JobHandle dependency,
            AllocatorManager.AllocatorHandle allocator)
        {
            vertexErrorQuadrics = new(allocator);

            var initializeVertexErrorQuadricsJob = new InitializeVertexListJob<ErrorQuadric>
            {
                MeshData = mesh,
                Options = NativeArrayOptions.UninitializedMemory,
                Buffer = vertexErrorQuadrics,
            }.Schedule(dependency);

            var computeVertexErrorQuadricsJob = new ComputeVertexErrorQuadricsJob
            {
                VertexPositionBuffer = vertexPositionBuffer.AsDeferredJobArray(),
                Triangles = triangles.AsDeferredJobArray(),
                VertexContainingTriangles = vertexContainingTriangles,
                EdgeCounts = edgeCounts,
                TriangleErrorQuadrics = triangleErrorQuadrics.AsDeferredJobArray(),
                VertexErrorQuadrics = vertexErrorQuadrics.AsDeferredJobArray(),
            }.Schedule(vertexErrorQuadrics, JobsUtility.CacheLineSize, 
            JobHandle.CombineDependencies(dependency, initializeVertexErrorQuadricsJob));

            return computeVertexErrorQuadricsJob;
        }

        static JobHandle ScheduleConstructMerges(
            out NativeList<VertexMerge> vertexMerges,
            NativeList<float3> vertexPositions,
            NativeList<ErrorQuadric> vertexErrorQuadrics,
            NativeParallelMultiHashMap<int, int> vertexContainingTriangles,
            NativeBitArray vertexIsBorderEdgeBits,
            NativeList<float3> triangleNormals,
            NativeList<int2> edges,
            MeshSimplifierOptions options,
            JobHandle dependency,
            AllocatorManager.AllocatorHandle allocator)
        {
            vertexMerges = new NativeList<VertexMerge>(allocator);

            var initializeVertexMergesJob = new InitializeVertexMergesJob
            {
                Edges = edges.AsDeferredJobArray(),
                VertexMerges = vertexMerges,
            }.Schedule(dependency);

            var computeMergesJob = new ComputeMergesJob
            {
                VertexPositions = vertexPositions.AsDeferredJobArray(),
                VertexErrorQuadrics = vertexErrorQuadrics.AsDeferredJobArray(),
                VertexContainingTriangles = vertexContainingTriangles,
                VertexIsBorderEdgeBits = vertexIsBorderEdgeBits,
                TriangleNormals = triangleNormals.AsDeferredJobArray(),
                Edges = edges.AsDeferredJobArray(),
                Options = options,
                VertexMerges = vertexMerges.AsDeferredJobArray(),
            }.Schedule(edges, JobsUtility.CacheLineSize, JobHandle.CombineDependencies(dependency, initializeVertexMergesJob));

            var removeInvalidMergesJob = new RemoveInvalidMergesJob
            {
                VertexMerges = vertexMerges,
            }.Schedule(computeMergesJob);

            return removeInvalidMergesJob;
        }

        static JobHandle ScheduleConstructVertexMergeOpponentVertices(
            out NativeParallelMultiHashMap<int, int> vertexMergeOpponentVertices,
            NativeList<VertexMerge> vertexMerges,
            JobHandle dependency,
            AllocatorManager.AllocatorHandle allocator)
        {
            vertexMergeOpponentVertices = new NativeParallelMultiHashMap<int, int>(0, allocator);

            var collectVertexMergeOpponentsJob = new CollectVertexMergeOpponmentsJob
            {
                Merges = vertexMerges.AsDeferredJobArray(),
                VertexMergeOpponentVertices = vertexMergeOpponentVertices,
            }.Schedule(dependency);

            return collectVertexMergeOpponentsJob;
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
            MeshSimplifierOptions options,
            NativeHashSet<int2> smartLinks,
            JobHandle meshDependency,
            JobHandle vertexPositionBufferDependency,
            JobHandle otherVertexAttributeBufferDependency,
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
                Options = options,
                SubMeshSmartLinkListAllocator = allocator,
                SubMeshSmartLinkLists = subMeshSmartLinkLists.AsDeferredJobArray(),
            }.Schedule(subMeshSmartLinkLists, 1, JobHandle.CombineDependencies(initializeSubMeshSmartLinkLists, vertexPositionBufferDependency));
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
        
    }
}


