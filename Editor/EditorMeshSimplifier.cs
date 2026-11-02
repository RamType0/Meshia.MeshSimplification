#nullable enable
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Meshia.MeshSimplification.Editor
{
    /// <summary>
    /// Editor-only simplification entry points that use UnityEditor.MeshUtility
    /// to acquire mesh data without requiring Mesh.isReadable.
    /// </summary>
    public static class EditorMeshSimplifier
    {
        public static void Simplify(Mesh mesh, MeshSimplificationTarget target, MeshSimplifierOptions options, Mesh destination)
            => Simplify(mesh, target, options, null, destination);

        public static void Simplify(Mesh mesh, MeshSimplificationTarget target, MeshSimplifierOptions options, BitArray? preserveBorderEdgesBoneIndices, Mesh destination)
            => MeshSimplifier.Simplify(mesh, target, options, preserveBorderEdgesBoneIndices, destination, useMeshUtility: true);

        public static void SimplifyBatch(IReadOnlyList<(Mesh Mesh, MeshSimplificationTarget Target, MeshSimplifierOptions Options, Mesh Destination)> parameters)
            => SimplifyBatch(parameters.Select<(Mesh Mesh, MeshSimplificationTarget Target, MeshSimplifierOptions Options, Mesh Destination), (Mesh, MeshSimplificationTarget, MeshSimplifierOptions, BitArray?, Mesh)>(p => (p.Mesh, p.Target, p.Options, null, p.Destination)).ToList());

        public static void SimplifyBatch(IReadOnlyList<(Mesh Mesh, MeshSimplificationTarget Target, MeshSimplifierOptions Options, BitArray? PreserveBorderEdgesBoneIndices, Mesh Destination)> parameters)
            => MeshSimplifier.SimplifyBatch(parameters, useMeshUtility: true);

        public static Task SimplifyAsync(Mesh mesh, MeshSimplificationTarget target, MeshSimplifierOptions options, Mesh destination, CancellationToken cancellationToken = default)
            => SimplifyAsync(mesh, target, options, null, destination, cancellationToken);

        public static Task SimplifyAsync(Mesh mesh, MeshSimplificationTarget target, MeshSimplifierOptions options, BitArray? preserveBorderEdgesBoneIndices, Mesh destination, CancellationToken cancellationToken = default)
            => MeshSimplifier.SimplifyAsync(mesh, target, options, preserveBorderEdgesBoneIndices, destination, useMeshUtility: true, cancellationToken: cancellationToken);
    }
}
