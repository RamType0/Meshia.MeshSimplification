using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
namespace Meshia.MeshSimplification
{
    [BurstCompile]
    struct FindNonReferencedVerticesJob : IJob
    {
        [ReadOnly] public Mesh.MeshData Mesh;
        [ReadOnly] public NativeParallelMultiHashMap<int, int> VertexContainingTriangles;
        public NativeBitArray VertexIsDiscardedBits;
        public void Execute()
        {
            VertexIsDiscardedBits.Resize(Mesh.vertexCount);


            for (int subMeshIndex = 0; subMeshIndex < Mesh.subMeshCount; subMeshIndex++)
            {
                var subMeshDescriptor = Mesh.GetSubMesh(subMeshIndex);
                if (subMeshDescriptor.topology is MeshTopology.Triangles)
                {
                    var subMeshLastVertex = subMeshDescriptor.firstVertex + subMeshDescriptor.vertexCount;
                    for (int vertex = subMeshDescriptor.firstVertex; vertex < subMeshLastVertex; vertex++)
                    {
                        VertexIsDiscardedBits.Set(vertex, !VertexContainingTriangles.ContainsKey(vertex));
                    }
                }
                else
                {
                    VertexIsDiscardedBits.SetBits(subMeshDescriptor.firstVertex, false, subMeshDescriptor.vertexCount);
                }
            }
        }
    }
}


