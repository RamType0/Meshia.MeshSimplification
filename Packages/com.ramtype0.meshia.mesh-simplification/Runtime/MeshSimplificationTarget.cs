using System;
using UnityEngine;

namespace Meshia.MeshSimplification
{
    [Serializable]
    public struct MeshSimplificationTarget
    {
        public MeshSimplificationTargetKind Kind;
        [Min(0)]
        public float Value;
    }
}


