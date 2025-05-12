using UnityEditor;

namespace Meshia.MeshSimplification.Ndmf.Editor
{
    [CustomEditor(typeof(NdmfMeshSimplifier))]
    public class NdmfMeshSimplifierEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
#if !ENABLE_NDMF
            EditorGUILayout.HelpBox("This component has no effect without NDMF imported to the project.", MessageType.Warning);
#endif
            base.OnInspectorGUI();
        }
    }
}
