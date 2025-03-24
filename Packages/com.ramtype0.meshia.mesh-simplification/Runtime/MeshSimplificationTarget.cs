using System;
using UnityEngine;

namespace Meshia.MeshSimplification
{
    [Serializable]
    public struct MeshSimplificationTarget : IEquatable<MeshSimplificationTarget>
    {
        public MeshSimplificationTargetKind Kind;
        [Min(0)]
        public float Value;

        public override bool Equals(object obj)
        {
            return obj is MeshSimplificationTarget target && Equals(target);
        }

        public bool Equals(MeshSimplificationTarget other)
        {
            return Kind == other.Kind &&
                   Value == other.Value;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Kind, Value);
        }

        public static bool operator ==(MeshSimplificationTarget left, MeshSimplificationTarget right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MeshSimplificationTarget left, MeshSimplificationTarget right)
        {
            return !(left == right);
        }
    }
}


