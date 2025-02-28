using System;
using UnityEngine;
namespace Meshia.MeshSimplification
{
    [Serializable]
    public struct MeshSimplifierOptions : IEquatable<MeshSimplifierOptions>
    {
        public static MeshSimplifierOptions Default => new()
        {
            PreserveBorderEdges = false,
            PreserveSurfaceCurvature = false,
            UseBarycentricCoordinateInterpolation = false,
            MinNormalDot = 0.2f,
            EnableSmartLink = true,
            VertexLinkDistance = 0.0001f,
            VertexLinkMinNormalDot = 0.95f,
            VertexLinkColorDistance = 0.01f,
            VertexLinkUvDistance = 0.001f,
        };
        public bool PreserveBorderEdges;
        public bool PreserveSurfaceCurvature;
        public bool UseBarycentricCoordinateInterpolation;
        public bool EnableSmartLink;
        [Range(-1,1)]
        public float MinNormalDot;

        public float VertexLinkDistance;
        [Range(-1, 1)]
        public float VertexLinkMinNormalDot;
        // This could be HDR color, so there is no Range.
        public float VertexLinkColorDistance;
        [Range(0, 1.41421356237f)]
        public float VertexLinkUvDistance;

        

        public override bool Equals(object obj)
        {
            return obj is MeshSimplifierOptions options && Equals(options);
        }

        public bool Equals(MeshSimplifierOptions other)
        {
            return PreserveBorderEdges == other.PreserveBorderEdges &&
                   PreserveSurfaceCurvature == other.PreserveSurfaceCurvature &&
                   UseBarycentricCoordinateInterpolation == other.UseBarycentricCoordinateInterpolation &&
                   EnableSmartLink == other.EnableSmartLink &&
                   MinNormalDot == other.MinNormalDot &&
                   VertexLinkDistance == other.VertexLinkDistance &&
                   VertexLinkMinNormalDot == other.VertexLinkMinNormalDot &&
                   VertexLinkColorDistance == other.VertexLinkColorDistance &&
                   VertexLinkUvDistance == other.VertexLinkUvDistance;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PreserveBorderEdges, PreserveSurfaceCurvature, MinNormalDot);
        }

        public static bool operator ==(MeshSimplifierOptions left, MeshSimplifierOptions right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MeshSimplifierOptions left, MeshSimplifierOptions right)
        {
            return !(left == right);
        }
    }
}


