#nullable enable

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using nadena.dev.ndmf.preview;
using Meshia.MeshSimplification.Ndmf.Editor.Preview;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace Meshia.MeshSimplification.Ndmf.Editor
{
    [CustomEditor(typeof(MeshiaCascadingAvatarMeshSimplifier))]
    internal class MeshiaCascadingAvatarMeshSimplifierEditor : UnityEditor.Editor
    {
        [SerializeField] VisualTreeAsset visualTreeAsset = null!;
        private MeshiaCascadingAvatarMeshSimplifier _component = null!;

        MeshiaCascadingAvatarMeshSimplifier Target => (MeshiaCascadingAvatarMeshSimplifier)target;

        private SerializedProperty AutoAdjustEnabled = null!;
        private SerializedProperty TargetTriangleCount = null!;

        private (SerializedProperty property, Renderer renderer, int totalTriangleCount)[] _simplifierTargets = Array.Empty<(SerializedProperty, Renderer, int)>();

        private void OnEnable()
        {
            _component = (MeshiaCascadingAvatarMeshSimplifier)target;

            AutoAdjustEnabled = serializedObject.FindProperty(nameof(MeshiaCascadingAvatarMeshSimplifier.AutoAdjustEnabled));
            TargetTriangleCount = serializedObject.FindProperty(nameof(MeshiaCascadingAvatarMeshSimplifier.TargetTriangleCount));

            GetTargets();
        }

        private void GetTargets()
        {
            // 有効な対象を取得し、Triangleが多い順にソート。
            // SerializedPropertyの取得及びRendererや簡略化前のTriangleCountを取得しておく。

            Undo.RecordObject(_component, "GetTargets");
            _component.AddTargets();
            var validTargets = _component.GetValidTargets();

            if (validTargets.Count == 0) return;

            var targetProperties = new List<(SerializedProperty property, Renderer renderer, int totalTriangleCount)>();

            var targetsProperty = serializedObject.FindProperty(nameof(MeshiaCascadingAvatarMeshSimplifier.Targets));
            if (targetsProperty == null || !targetsProperty.isArray) throw new Exception("Targets property is not found");

            for (int i = 0; i < targetsProperty.arraySize; i++)
            {
                var elementProperty = targetsProperty.GetArrayElementAtIndex(i);
                if (validTargets.TryGetValue(i, out var target))
                {
                    var renderer = target.GetTargetRenderer(_component);
                    if (renderer == null) continue;
                    var totalTriangleCount = RendererUtility.GetRequiredMesh(renderer).GetTriangleCount();
                    targetProperties.Add((elementProperty, renderer, totalTriangleCount));
                }
            }
            _simplifierTargets = targetProperties.OrderByDescending(tp => tp.totalTriangleCount).ToArray();

        }

        public override VisualElement CreateInspectorGUI()
        {
            //return base.CreateInspectorGUI();
            VisualElement root = new();
            visualTreeAsset.CloneTree(root);

            serializedObject.Update();

            root.Bind(serializedObject);
            var targetTriangleCountField = root.Q<IntegerField>("TargetTriangleCountField");
            var targetTriangleCountPresetDropdownField = root.Q<DropdownField>("TargetTriangleCountPresetDropdownField");
            var adjustButton = root.Q<Button>("AdjustButton");
            var autoAdjustEnabledToggle = root.Q<Toggle>("AutoAdjustEnabledToggle");
            var imguiContainer = root.Q<IMGUIContainer>();
            targetTriangleCountField.RegisterValueChangedCallback(changeEvent =>
            {
                if (!TargetTriangleCountPresetValueToName.TryGetValue(changeEvent.newValue, out var name))
                {
                    name = "Custom";
                }

                targetTriangleCountPresetDropdownField.SetValueWithoutNotify(name);
                if (AutoAdjustEnabled.boolValue)
                {
                    AdjustQuality();
                }
            });

            targetTriangleCountPresetDropdownField.choices = TargetTriangleCountPresetNameToValue.Keys.ToList();
            targetTriangleCountPresetDropdownField.RegisterValueChangedCallback(changeEvent =>
            {

                if(TargetTriangleCountPresetNameToValue.TryGetValue(changeEvent.newValue, out var value))
                {
                    TargetTriangleCount.intValue = value;
                    serializedObject.ApplyModifiedProperties();
                }

            });

            adjustButton.clicked += () =>
            {
                AdjustQuality();
            };

            autoAdjustEnabledToggle.RegisterValueChangedCallback(changeEvent =>
            {
                if (AutoAdjustEnabled.boolValue)
                {
                    AdjustQuality();
                }
            });

            imguiContainer.onGUIHandler = () =>
            {
                serializedObject.Update();

                EditorGUILayout.Space();
                TargetGUI();
                EditorGUILayout.Space();
                TogglePreviewGUI(MeshiaCascadingAvatarMeshSimplifierPreview.ToggleNode);

                serializedObject.ApplyModifiedProperties();
            };

            return root;
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            AutoAdjustGUI();
            EditorGUILayout.Space();
            TargetGUI();
            EditorGUILayout.Space();
            TogglePreviewGUI(MeshiaCascadingAvatarMeshSimplifierPreview.ToggleNode);

            serializedObject.ApplyModifiedProperties();
        }

        static Dictionary<string, int> TargetTriangleCountPresetNameToValue { get; } = new()
        {
            ["PC-Poor-Medium-Good"] = 70000,
            ["PC-Excellent"] = 32000,
            ["Mobile-Poor"] = 20000,
            ["Mobile-Medium"] = 15000,
            ["Mobile-Good"] = 10000,
            ["Mobile-Excellent"] = 7500,
        };

        static Dictionary<int, string> TargetTriangleCountPresetValueToName { get; } = TargetTriangleCountPresetNameToValue.ToDictionary(keyValue => keyValue.Value, keyValue => keyValue.Key);

        GUIContent[] displayTriangleOptions = new GUIContent[]
        {
            new GUIContent("PC-Poor-Medium-Good"),
            new GUIContent("PC-Excellent"),
            new GUIContent("Mobile-Poor"),
            new GUIContent("Mobile-Medium"),
            new GUIContent("Mobile-Good"),
            new GUIContent("Mobile-Excellent")
        };
        int[] triangleOptions = new int[]
        {
            70000,
            32000,
            20000,
            15000,
            10000,
            7500
        };
        private void AutoAdjustGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Target Triangle Count", GUILayout.Width(150f));
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(TargetTriangleCount, GUIContent.none);
                EditorGUILayout.IntPopup(TargetTriangleCount, displayTriangleOptions, triangleOptions, GUIContent.none, GUILayout.Width(200f));
                if (EditorGUI.EndChangeCheck() && AutoAdjustEnabled.boolValue)
                {
                    AdjustQuality();
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Adjust", GUILayout.Width(90f))) { AdjustQuality(); }
                EditorGUI.BeginChangeCheck();
                AutoAdjustEnabled.boolValue = EditorGUILayout.ToggleLeft("Enable Auto Adjust", AutoAdjustEnabled.boolValue);
                if (EditorGUI.EndChangeCheck() && AutoAdjustEnabled.boolValue)
                {
                    AdjustQuality();
                }
            }
        }

        private int _currentSimplifySettingTargetIndex = -1;
        private bool _showCurrentSimplifySetting = true;
        private void TargetGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var current = GetTotalSimplifiedTriangleCount(true);
                var sum = GetTotalTriangleCount();
                var countLabel = $"Current: {current} / {sum}";
                var labelWidth1 = 7f * countLabel.ToString().Count();
                var isOverflow = TargetTriangleCount.intValue < current;
                if (isOverflow) EditorGUILayout.LabelField(countLabel + " - Overflow!", GUIStyleHelper.RedStyle, GUILayout.Width(labelWidth1));
                else EditorGUILayout.LabelField(countLabel, GUILayout.Width(labelWidth1));

                GUILayout.FlexibleSpace();
                
                if (GUILayout.Button("Set 50%", GUILayout.Width(90f))) { SetQualityAll(0.5f); }
                if (GUILayout.Button("Set 100%", GUILayout.Width(90f))) { SetQualityAll(1.0f); }

                var iconContent = EditorGUIUtility.IconContent("AssemblyLock");
                iconContent.tooltip = "Lock value";
                EditorGUILayout.LabelField(iconContent, GUILayout.Width(18f));
                
                EditorGUILayout.LabelField(GUIContent.none, GUILayout.Width(18f));
            }

            for (int i = 0; i < _simplifierTargets.Length; i++)
            {
                var enabledTarget = _simplifierTargets[i].property;
                var renderer = _simplifierTargets[i].renderer;
                var targetTriangleCount = enabledTarget.FindPropertyRelative(nameof(MeshiaCascadingAvatarMeshSimplifierTarget.TargetTriangleCount));
                var totalTriangleCount = GetTriangleCount(i, true);
                var fixedValue = enabledTarget.FindPropertyRelative(nameof(MeshiaCascadingAvatarMeshSimplifierTarget.Fixed));
                var enabledValue = enabledTarget.FindPropertyRelative(nameof(MeshiaCascadingAvatarMeshSimplifierTarget.Enabled));

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(enabledValue, GUIContent.none, GUILayout.Width(18f));
                    
                    EditorGUI.BeginDisabledGroup(!enabledValue.boolValue);
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.ObjectField(renderer, typeof(Renderer), false, GUILayout.MinWidth(100f)); // ReadOnly
                    EditorGUILayout.IntSlider(targetTriangleCount, 0, totalTriangleCount, GUIContent.none, GUILayout.MinWidth(140f));
                    if (EditorGUI.EndChangeCheck() && AutoAdjustEnabled.boolValue)
                    {
                        AdjustQuality(i);
                    }
                    EditorGUILayout.LabelField(new GUIContent($"/ {totalTriangleCount}"), GUILayout.Width(70f));
                    EditorGUILayout.PropertyField(fixedValue, GUIContent.none, GUILayout.Width(18f));
                    if (GUILayout.Button(EditorGUIUtility.IconContent("Settings@2x"), GUIStyleHelper.IconButtonStyle, GUILayout.Width(16f), GUILayout.Height(16f))) 
                    { 
                        _currentSimplifySettingTargetIndex = i;
                        _showCurrentSimplifySetting = true;
                    }
                    EditorGUI.EndDisabledGroup();
                }
            }

            if (_currentSimplifySettingTargetIndex != -1)
            {
                var renderer = _simplifierTargets[_currentSimplifySettingTargetIndex].renderer;
                var options = _simplifierTargets[_currentSimplifySettingTargetIndex].property.FindPropertyRelative(nameof(MeshiaCascadingAvatarMeshSimplifierTarget.Options));
                
                _showCurrentSimplifySetting = EditorGUILayout.Foldout(_showCurrentSimplifySetting, $"Simplifier Options for {renderer?.name}");
                if (_showCurrentSimplifySetting)
                {
                    var iterator = options;
                    iterator.NextVisible(true);
                    while (iterator.NextVisible(false) && iterator.depth == 3)
                    {
                        EditorGUILayout.PropertyField(iterator);
                    }
                }
            }
        }

        private void TogglePreviewGUI(TogglablePreviewNode? toggleNode)
        {
            if(toggleNode == null)
            {
                return; 
            }
            if (toggleNode.IsEnabled.Value)
            {
                if (GUILayout.Button("Disable NDMF Preview"))
                {
                    toggleNode.IsEnabled.Value = false;
                }
            }
            else
            {
                if (GUILayout.Button("Enable NDMF Preview"))
                {
                    toggleNode.IsEnabled.Value = true;
                }
            }
        }

        private int GetTotalSimplifiedTriangleCount(bool usePreview)
        {
            var sum = 0;
            for (int i = 0; i < _simplifierTargets.Length; i++)
            {
                sum += GetSimplifiedTriangleCount(i, usePreview);
            }
            return sum;
        }

        private int GetTotalTriangleCount()
        {
            var sum = 0;
            for (int i = 0; i < _simplifierTargets.Length; i++)
            {
                sum += GetTriangleCount(i, false);
            }
            return sum;
        }

        private int GetSimplifiedTriangleCount(int index, bool usePreview)
        {   
            var target = _simplifierTargets[index];
            var targetProp = target.property;
            
            if (!targetProp.FindPropertyRelative(nameof(MeshiaCascadingAvatarMeshSimplifierTarget.Enabled)).boolValue)
            {
                return GetTriangleCount(index, usePreview);
            }
            // プレビューが有効な場合は実際に簡略された後の値を用い、無効な場合は目標値を用いる
            else if (usePreview && MeshiaCascadingAvatarMeshSimplifierPreview.IsEnabled() && MeshiaCascadingAvatarMeshSimplifierPreview.TriangleCountCache.TryGetValue(target.renderer, out var triCount))
            {
                return triCount.simplified;
            }
            else
            {
                return targetProp.FindPropertyRelative(nameof(MeshiaCascadingAvatarMeshSimplifierTarget.TargetTriangleCount)).intValue;
            }
        }

        private int GetTriangleCount(int index, bool usePreview)
        {
            var target = _simplifierTargets[index];
            var targetProp = target.property;

            // プレビューが有効かつEnabledな対象な場合はProxyRendererの値を用い、無効な場合は事前の値を用いる
            if (usePreview && MeshiaCascadingAvatarMeshSimplifierPreview.IsEnabled() && targetProp.FindPropertyRelative(nameof(MeshiaCascadingAvatarMeshSimplifierTarget.Enabled)).boolValue && MeshiaCascadingAvatarMeshSimplifierPreview.TriangleCountCache.TryGetValue(target.renderer, out var triCount))
            {
                return triCount.proxy;
            }
            else
            {
                return target.totalTriangleCount;
            }
        }

        private void AdjustQuality(int fixedIndex = -1)
        {

            var targetCount = TargetTriangleCount.intValue;

            // 比例配分で差分を分配（目標値に到達するまでループ）
            for (int iteration = 0; iteration < 5; iteration++)
            {
                var currentTotal = 0;
                var adjustableTotal = 0;
                
                // 現在の調整可能なTriangleCountを取得
                for (int i = 0; i < _simplifierTargets.Length; i++)
                {
                    var target = _simplifierTargets[i].property;
                    var enabled = target.FindPropertyRelative(nameof(MeshiaCascadingAvatarMeshSimplifierTarget.Enabled)).boolValue;
                    var fixedValue = target.FindPropertyRelative(nameof(MeshiaCascadingAvatarMeshSimplifierTarget.Fixed)).boolValue;
                    
                    var triangleCount = GetSimplifiedTriangleCount(i, false);
                    
                    currentTotal += triangleCount;
                    
                    if (enabled && !fixedValue && i != fixedIndex)
                    {
                        adjustableTotal += triangleCount;
                    }
                }
                
                if (adjustableTotal == 0) { Debug.LogError("Adjustable total is 0"); break; }
                
                // 目標値より少し小さい値（<10の差）で収束判定
                if (currentTotal <= targetCount && targetCount - currentTotal < 10) { Debug.Log("Converged"); break; }
                
                var adjustableTargetCount = targetCount - (currentTotal - adjustableTotal);
                if (adjustableTargetCount <= 0) { Debug.LogError("Adjustable target count is 0"); break; }
                
                // 比例配分で調整
                var proportion = (float)adjustableTargetCount / adjustableTotal;
                for (int i = 0; i < _simplifierTargets.Length; i++)
                {
                    if (i == fixedIndex) continue;
                    
                    var target = _simplifierTargets[i].property;
                    var enabled = target.FindPropertyRelative(nameof(MeshiaCascadingAvatarMeshSimplifierTarget.Enabled)).boolValue;
                    var fixedValue = target.FindPropertyRelative(nameof(MeshiaCascadingAvatarMeshSimplifierTarget.Fixed)).boolValue;
                    
                    if (enabled && !fixedValue)
                    {
                        var targetTriangleCountProp = target.FindPropertyRelative(nameof(MeshiaCascadingAvatarMeshSimplifierTarget.TargetTriangleCount));

                        var currentValue = GetSimplifiedTriangleCount(i, false);
                        var maxTriangleCount = GetTriangleCount(i, false);
                        
                        var newValue = Mathf.Clamp((int)(currentValue * proportion), 0, maxTriangleCount);
                        
                        targetTriangleCountProp.intValue = newValue;
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void SetQualityAll(float ratio)
        {
            for (int i = 0; i < _simplifierTargets.Length; i++)
            {
                var simplifierTarget = _simplifierTargets[i].property;
                var fixedValue = simplifierTarget.FindPropertyRelative(nameof(MeshiaCascadingAvatarMeshSimplifierTarget.Fixed));
                var targetTriangleCount = simplifierTarget.FindPropertyRelative(nameof(MeshiaCascadingAvatarMeshSimplifierTarget.TargetTriangleCount));
                var totalTriangleCount = GetTriangleCount(i, true);

                if (!fixedValue.boolValue)
                {
                    targetTriangleCount.intValue = (int)(totalTriangleCount * ratio);
                }
            }
        }

    }

    internal static class GUIStyleHelper
    {
        private static GUIStyle? m_iconButtonStyle;
        public static GUIStyle IconButtonStyle
        {
            get
            {
                if (m_iconButtonStyle == null) m_iconButtonStyle = InitIconButtonStyle();
                return m_iconButtonStyle;
            }
        }
        static GUIStyle InitIconButtonStyle()
        {
            var style = new GUIStyle();
            return style;
        }

        private static GUIStyle? m_redStyle;
        public static GUIStyle RedStyle
        {
            get
            {
                if (m_redStyle == null) m_redStyle = InitRedStyle();
                return m_redStyle;
            }
        }
        static GUIStyle InitRedStyle()
        {
            var style = new GUIStyle();
            style.normal = new GUIStyleState() { textColor = Color.red };
            return style;
        }
    }
}