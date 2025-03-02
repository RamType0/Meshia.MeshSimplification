using UnityEngine;
#if ENABLE_VRCHAT_BASE
using VRC.SDKBase;
#endif


namespace Meshia.MeshSimplification.Ndmf
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public class NdmfMeshSimplifier : MonoBehaviour
#if ENABLE_VRCHAT_BASE
    , IEditorOnly
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

