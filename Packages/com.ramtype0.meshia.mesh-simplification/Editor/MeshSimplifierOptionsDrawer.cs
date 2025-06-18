#nullable enable
using Meshia.MeshSimplification;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
namespace Meshia.MeshSimplification.Editor
{
    [CustomPropertyDrawer(typeof(MeshSimplifierOptions))]
    public class MeshSimplifierOptionsDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(AssetDatabase.GUIDToAssetPath("29eaabb0631cacc44913c34b86fc38f0"));

            var root = visualTreeAsset.CloneTree();

            root.BindProperty(property);
            return root;
        }
    }

}
