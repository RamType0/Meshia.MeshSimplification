using UnityEngine;
using VRC.SDKBase;


namespace Meshia.MeshSimplification.Ndmf
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public class NdmfMeshSimplifier : MonoBehaviour
#if ENABLE_VRCHAT_BASE
    , IEditorOnly
#endif
    {
        [Range(0f, 1f)]
        public float quality = 0.5f;
        public MeshSimplifierOptions options = MeshSimplifierOptions.Default;
    }

}

