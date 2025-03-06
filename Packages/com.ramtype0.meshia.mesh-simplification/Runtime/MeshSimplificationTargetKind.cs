using System;

namespace Meshia.MeshSimplification
{
    [Serializable]
    public enum MeshSimplificationTargetKind
    {
        RelativeVertexCount,
        AbsoluteVertexCount,
        ScaledTotalError,
        AbsoluteTotalError,
        RelativeTriangleCount,
        AbsoluteTriangleCount,
    }
}


