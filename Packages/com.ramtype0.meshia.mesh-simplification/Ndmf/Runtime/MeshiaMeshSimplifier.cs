using UnityEngine;


namespace Meshia.MeshSimplification.Ndmf
{
    [AddComponentMenu("Meshia Mesh Simplification/MeshiaMeshSimplifier")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public class MeshiaMeshSimplifier : MonoBehaviour
#if ENABLE_VRCHAT_BASE
    , VRC.SDKBase.IEditorOnly
#endif
    {
        public MeshSimplificationTarget target = new()
        {
            Kind = MeshSimplificationTargetKind.RelativeVertexCount,
            Value = 0.5f,
        };
        public MeshSimplifierOptions options = MeshSimplifierOptions.Default;
    }

}

