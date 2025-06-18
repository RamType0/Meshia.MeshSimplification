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
using System.Reflection;

namespace Meshia.MeshSimplification.Ndmf.Editor
{
    [CustomEditor(typeof(MeshiaCascadingAvatarMeshSimplifier))]
    internal class MeshiaCascadingAvatarMeshSimplifierEditor : UnityEditor.Editor
    {
        [SerializeField] VisualTreeAsset editorVisualTreeAsset = null!;
        [SerializeField] VisualTreeAsset entryEditorVisualTreeAsset;
        private MeshiaCascadingAvatarMeshSimplifier _component = null!;

        private SerializedProperty AutoAdjustEnabled = null!;
        private SerializedProperty TargetTriangleCount = null!;


        Action<bool>? onNdmfPreviewEnabledChanged;

        private (SerializedProperty property, Renderer Renderer, int OriginalTriangleCount)[] _validEntries = Array.Empty<(SerializedProperty, Renderer, int)>();

        private void OnEnable()
        {
            _component = (MeshiaCascadingAvatarMeshSimplifier)target;

            AutoAdjustEnabled = serializedObject.FindProperty(nameof(MeshiaCascadingAvatarMeshSimplifier.AutoAdjustEnabled));
            TargetTriangleCount = serializedObject.FindProperty(nameof(MeshiaCascadingAvatarMeshSimplifier.TargetTriangleCount));

            RefreshValidEntries();
        }

        private void OnDisable()
        {
            if(onNdmfPreviewEnabledChanged != null)
            {
                MeshiaCascadingAvatarMeshSimplifierPreview.PreviewControlNode.IsEnabled.OnChange -= onNdmfPreviewEnabledChanged;
                onNdmfPreviewEnabledChanged = null;
            }
        }

        private void RefreshValidEntries()
        {
            // 有効な対象を取得し、Triangleが多い順にソート。
            // SerializedPropertyの取得及びRendererや簡略化前のTriangleCountを取得しておく。

            Undo.RecordObject(_component, "Get valid entries");
            _component.RefreshEntries();

            serializedObject.Update();

            var entries = _component.Entries;

            var targetsProperty = serializedObject.FindProperty(nameof(MeshiaCascadingAvatarMeshSimplifier.Entries));

            var targetProperties = new List<(SerializedProperty property, Renderer renderer, int OriginalTriangleCount)>();

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var renderer = entry.GetTargetRenderer(_component);
                if (MeshiaCascadingAvatarMeshSimplifierRendererEntry.IsValidTarget(renderer))
                {
                    var elementProperty = targetsProperty.GetArrayElementAtIndex(i);

                    var OriginalTriangleCount = RendererUtility.GetRequiredMesh(renderer).GetTriangleCount();
                    targetProperties.Add((elementProperty, renderer, OriginalTriangleCount));
                }
            }

            _validEntries = targetProperties.OrderByDescending(tp => tp.OriginalTriangleCount).ToArray();

        }

        public override VisualElement CreateInspectorGUI()
        {
            //return base.CreateInspectorGUI();
            VisualElement root = new();
            editorVisualTreeAsset.CloneTree(root);

            serializedObject.Update();

            root.Bind(serializedObject);
            var targetTriangleCountField = root.Q<IntegerField>("TargetTriangleCountField");
            var targetTriangleCountPresetDropdownField = root.Q<DropdownField>("TargetTriangleCountPresetDropdownField");
            var adjustButton = root.Q<Button>("AdjustButton");
            var autoAdjustEnabledToggle = root.Q<Toggle>("AutoAdjustEnabledToggle");


            var triangleCountLabel = root.Q<IMGUIContainer>("TriangleCountLabel");
            var set50Button = root.Q<Button>("Set50Button");
            var set100Button = root.Q<Button>("Set100Button");
            var entriesListView = root.Q<ListView>("EntriesListView");
            var imguiArea = root.Q<IMGUIContainer>("IMGUIArea");
            var ndmfPreviewToggle = root.Q<Toggle>("NdmfPreviewToggle");
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
                var autoAdjustEnabled = AutoAdjustEnabled.boolValue;

                set50Button.SetEnabled(!autoAdjustEnabled);
                set100Button.SetEnabled(!autoAdjustEnabled);

                if (autoAdjustEnabled)
                {
                    AdjustQuality();
                }
            });


            triangleCountLabel.onGUIHandler = () =>
            {
                var current = GetTotalSimplifiedTriangleCount(true);
                var sum = GetTotalTriangleCount();
                var countLabel = $"Current: {current} / {sum}";
                var labelWidth1 = 7f * countLabel.ToString().Count();
                var isOverflow = TargetTriangleCount.intValue < current;
                if (isOverflow) EditorGUILayout.LabelField(countLabel + " - Overflow!", GUIStyleHelper.RedStyle, GUILayout.Width(labelWidth1));
                else EditorGUILayout.LabelField(countLabel, GUILayout.Width(labelWidth1));
            };

            set50Button.clicked += () =>
            {
                SetQualityAll(0.5f);
            }; 
            set100Button.clicked += () =>
            {
                SetQualityAll(1f);
            };
            entriesListView.itemsSource = _validEntries;
            entriesListView.bindItem = (itemElement, index) =>
            {

                var entry = _validEntries[index];
                var itemRoot = (TemplateContainer)itemElement;
                var targetTriangleCountSlider = itemRoot.Q<SliderInt>("TargetTriangleCountSlider");
                var originalTriangleCountField = itemRoot.Q<IntegerField>("OriginalTriangleCountField");
                itemRoot.BindProperty(entry.property);
                itemRoot.userData = index;


                targetTriangleCountSlider.highValue = entry.OriginalTriangleCount;
                originalTriangleCountField.value = entry.OriginalTriangleCount;
            };


            entriesListView.makeItem = () =>
            {
                var itemRoot = entryEditorVisualTreeAsset.CloneTree();
                var targetObjectField = itemRoot.Q<ObjectField>("TargetObjectField");
                var targetTriangleCountSlider = itemRoot.Q<SliderInt>("TargetTriangleCountSlider");
                targetObjectField.SetEnabled(false);

                targetTriangleCountSlider.RegisterValueChangedCallback(changeEvent =>
                {
                    if (itemRoot.userData is int itemIndex && AutoAdjustEnabled.boolValue)
                    {
                        AdjustQuality(itemIndex);
                    }
                });

                return itemRoot;
            };
            ndmfPreviewToggle.RegisterValueChangedCallback(changeEvent =>
            {
                MeshiaCascadingAvatarMeshSimplifierPreview.PreviewControlNode.IsEnabled.Value = changeEvent.newValue;
            });

            ndmfPreviewToggle.SetValueWithoutNotify(MeshiaCascadingAvatarMeshSimplifierPreview.PreviewControlNode.IsEnabled.Value);
            onNdmfPreviewEnabledChanged = (newValue) =>
            {
                ndmfPreviewToggle.SetValueWithoutNotify(newValue);
            };
            MeshiaCascadingAvatarMeshSimplifierPreview.PreviewControlNode.IsEnabled.OnChange += onNdmfPreviewEnabledChanged;



            return root;
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            AutoAdjustGUI();
            EditorGUILayout.Space();
            TargetGUI();
            EditorGUILayout.Space();
            TogglePreviewGUI(MeshiaCascadingAvatarMeshSimplifierPreview.PreviewControlNode);

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

            EntriesGUI();
        }

        private void EntriesGUI()
        {
            for (int i = 0; i < _validEntries.Length; i++)
            {
                var enabledTarget = _validEntries[i].property;
                var renderer = _validEntries[i].Renderer;
                var targetTriangleCount = enabledTarget.FindPropertyRelative(nameof(MeshiaCascadingAvatarMeshSimplifierRendererEntry.TargetTriangleCount));
                var OriginalTriangleCount = GetOriginalTriangleCount(i, true);
                var fixedValue = enabledTarget.FindPropertyRelative(nameof(MeshiaCascadingAvatarMeshSimplifierRendererEntry.Fixed));
                var enabledValue = enabledTarget.FindPropertyRelative(nameof(MeshiaCascadingAvatarMeshSimplifierRendererEntry.Enabled));

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(enabledValue, GUIContent.none, GUILayout.Width(18f));

                    EditorGUI.BeginDisabledGroup(!enabledValue.boolValue);
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.ObjectField(renderer, typeof(Renderer), false, GUILayout.MinWidth(100f)); // ReadOnly
                    EditorGUILayout.IntSlider(targetTriangleCount, 0, OriginalTriangleCount, GUIContent.none, GUILayout.MinWidth(140f));
                    if (EditorGUI.EndChangeCheck() && AutoAdjustEnabled.boolValue)
                    {
                        AdjustQuality(i);
                    }
                    EditorGUILayout.LabelField(new GUIContent($"/ {OriginalTriangleCount}"), GUILayout.Width(70f));
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
                var renderer = _validEntries[_currentSimplifySettingTargetIndex].Renderer;
                var options = _validEntries[_currentSimplifySettingTargetIndex].property.FindPropertyRelative(nameof(MeshiaCascadingAvatarMeshSimplifierRendererEntry.Options));

                _showCurrentSimplifySetting = EditorGUILayout.Foldout(_showCurrentSimplifySetting, $"Simplifier Options for {renderer?.name}");
                if (_showCurrentSimplifySetting)
                {
                    var iterator = options;
                    iterator.NextVisible(true);
                    EditorGUILayout.PropertyField(iterator);
                    while (iterator.NextVisible(false) && iterator.depth == 3)
                    {
                        EditorGUILayout.PropertyField(iterator);
                    }
                }
            }
        }

        private void TogglePreviewGUI(TogglablePreviewNode toggleNode)
        {
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
            for (int i = 0; i < _validEntries.Length; i++)
            {
                sum += GetSimplifiedTriangleCount(i, usePreview);
            }
            return sum;
        }

        private int GetTotalTriangleCount()
        {
            var sum = 0;
            for (int i = 0; i < _validEntries.Length; i++)
            {
                sum += GetOriginalTriangleCount(i, false);
            }
            return sum;
        }

        private int GetSimplifiedTriangleCount(int index, bool usePreview)
        {   
            var target = _validEntries[index];
            var targetProp = target.property;
            
            if (!targetProp.FindPropertyRelative(nameof(MeshiaCascadingAvatarMeshSimplifierRendererEntry.Enabled)).boolValue)
            {
                return GetOriginalTriangleCount(index, usePreview);
            }
            // プレビューが有効な場合は実際に簡略された後の値を用い、無効な場合は目標値を用いる
            else if (usePreview && MeshiaCascadingAvatarMeshSimplifierPreview.IsEnabled() && MeshiaCascadingAvatarMeshSimplifierPreview.TriangleCountCache.TryGetValue(target.Renderer, out var triCount))
            {
                return triCount.simplified;
            }
            else
            {
                return targetProp.FindPropertyRelative(nameof(MeshiaCascadingAvatarMeshSimplifierRendererEntry.TargetTriangleCount)).intValue;
            }
        }

        private int GetOriginalTriangleCount(int index, bool usePreview)
        {
            var target = _validEntries[index];
            var targetProp = target.property;

            // プレビューが有効かつEnabledな対象な場合はProxyRendererの値を用い、無効な場合は事前の値を用いる
            if (usePreview && MeshiaCascadingAvatarMeshSimplifierPreview.IsEnabled() && targetProp.FindPropertyRelative(nameof(MeshiaCascadingAvatarMeshSimplifierRendererEntry.Enabled)).boolValue && MeshiaCascadingAvatarMeshSimplifierPreview.TriangleCountCache.TryGetValue(target.Renderer, out var triCount))
            {
                return triCount.proxy;
            }
            else
            {
                return target.OriginalTriangleCount;
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
                for (int i = 0; i < _validEntries.Length; i++)
                {
                    var target = _validEntries[i].property;
                    var enabled = target.FindPropertyRelative(nameof(MeshiaCascadingAvatarMeshSimplifierRendererEntry.Enabled)).boolValue;
                    var fixedValue = target.FindPropertyRelative(nameof(MeshiaCascadingAvatarMeshSimplifierRendererEntry.Fixed)).boolValue;
                    
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
                for (int i = 0; i < _validEntries.Length; i++)
                {
                    if (i == fixedIndex) continue;
                    
                    var target = _validEntries[i].property;
                    var enabled = target.FindPropertyRelative(nameof(MeshiaCascadingAvatarMeshSimplifierRendererEntry.Enabled)).boolValue;
                    var fixedValue = target.FindPropertyRelative(nameof(MeshiaCascadingAvatarMeshSimplifierRendererEntry.Fixed)).boolValue;
                    
                    if (enabled && !fixedValue)
                    {
                        var targetTriangleCountProp = target.FindPropertyRelative(nameof(MeshiaCascadingAvatarMeshSimplifierRendererEntry.TargetTriangleCount));

                        var currentValue = GetSimplifiedTriangleCount(i, false);
                        var maxTriangleCount = GetOriginalTriangleCount(i, false);
                        
                        var newValue = Mathf.Clamp((int)(currentValue * proportion), 0, maxTriangleCount);
                        
                        targetTriangleCountProp.intValue = newValue;
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void SetQualityAll(float ratio)
        {
            for (int i = 0; i < _validEntries.Length; i++)
            {
                var simplifierTarget = _validEntries[i].property;
                var fixedValue = simplifierTarget.FindPropertyRelative(nameof(MeshiaCascadingAvatarMeshSimplifierRendererEntry.Fixed));
                var targetTriangleCount = simplifierTarget.FindPropertyRelative(nameof(MeshiaCascadingAvatarMeshSimplifierRendererEntry.TargetTriangleCount));
                var OriginalTriangleCount = GetOriginalTriangleCount(i, true);

                if (!fixedValue.boolValue)
                {
                    targetTriangleCount.intValue = (int)(OriginalTriangleCount * ratio);
                }
            }
            serializedObject.ApplyModifiedProperties();
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