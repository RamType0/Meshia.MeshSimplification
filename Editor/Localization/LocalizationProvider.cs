#nullable enable
namespace Meshia.MeshSimplification.Editor.Localization
{
    using CustomLocalization4EditorExtension;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine.UIElements;

    internal static class LocalizationProvider
    {
        private const string DefaultLocale = "en";

        [AssemblyCL4EELocalization]
        public static Localization Localization { get; } = new("ca7beb49d3e85244e803080472c014c2", DefaultLocale);
        static Dictionary<string, string> LocaleCodeToName { get; } = new()
        {
            { "en", "English" },
            { "ja", "“ú–{Œê" }
        };
        static Dictionary<string, string> LocaleNameToCode { get; } = LocaleCodeToName.ToDictionary(keyValue => keyValue.Value, keyValue => keyValue.Key);
        public static void LocalizeProperties<T>(VisualElement root)
        {
            var typeName = typeof(T).Name;
            root.Query().OfType<BindableElement>().Where(bindableElement => !string.IsNullOrEmpty(bindableElement.bindingPath))
                .ForEach(bindableElement =>
                {
                    if (Localization.TryTr($"{typeName}:{bindableElement.bindingPath}:label") is { } translatedLabel)
                    {
                        switch (bindableElement)
                        {
                            case Toggle toggle:
                                {
                                    toggle.label = translatedLabel;
                                }
                                break;
                            case FloatField floatField:
                                {
                                    floatField.label = translatedLabel;
                                }
                                break;
                            case Slider slider:
                                {
                                    slider.label = translatedLabel;
                                }
                                break;
                        }
                    }
                    if (Localization.TryTr($"{typeName}:{bindableElement.bindingPath}:tooltip") is { } translatedTooltip)
                    {
                        bindableElement.tooltip = translatedTooltip;
                    }
                });
        }
        public static DropdownField CreateLanguagePicker()
        {
            var choices = LocaleNameToCode.Keys.OrderBy(name => name).ToList();
            DropdownField languagePicker = new(choices, LocaleCodeToName[Localization.CurrentLocaleCode ?? DefaultLocale]);
            languagePicker.RegisterValueChangedCallback(changeEvent =>
            {
                Localization.CurrentLocaleCode = LocaleNameToCode[changeEvent.newValue];
            });
            return languagePicker;
        }
    }

}
