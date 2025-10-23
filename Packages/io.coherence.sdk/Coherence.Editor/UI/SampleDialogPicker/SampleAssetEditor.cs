// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.UI
{
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine.UIElements;

    [CustomEditor(typeof(SampleAsset))]
    internal class SampleAssetEditor : Editor
    {
        private PropertyField MakeDefaultField(string label, string propertyPath, string tooltip = "")
        {
            var property = serializedObject.FindProperty(propertyPath);
            var field = new PropertyField(property, label)
            {
                tooltip = string.IsNullOrEmpty(tooltip) ? label : tooltip,
            };
            return field;
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            root.Add(MakeDefaultField("Enabled", nameof(SampleAsset.Enabled)));
            root.Add(MakeDefaultField("Name", nameof(SampleAsset.Name)));
            root.Add(MakeDefaultField("Sample Display Name", nameof(SampleAsset.SampleDisplayName)));
            root.Add(MakeDefaultField("Previous Sample Display Names", nameof(SampleAsset.PreviousSampleDisplayNames)));
            root.Add(MakeDefaultField("Asset Highlight Path", nameof(SampleAsset.AssetHighlightPath)));
            root.Add(MakeDefaultField("Open Asset Path", nameof(SampleAsset.OpenAssetPath)));
            root.Add(MakeDefaultField("Category", nameof(SampleAsset.Category)));
            root.Add(MakeDefaultField("Links", nameof(SampleAsset.Links)));
            root.Add(MakeDefaultField("Main Link", nameof(SampleAsset.MainLink)));
            root.Add(MakeDefaultField("Priority", nameof(SampleAsset.Priority)));

            var requirementsField = new TextField("Pre-Install Message")
            {
                multiline = true,
                tooltip = "Only shown when installing for the first time. Shown in a popup with a Yes/No confirmation.",
                style =
                {
                    whiteSpace = WhiteSpace.Normal,
                },
                bindingPath = nameof(SampleAsset.PreInstallMessage),
            };
            root.Add(requirementsField);

            var descriptionField = new TextField("Description")
            {
                multiline = true,
                style =
                {
                    whiteSpace = WhiteSpace.Normal,
                },
                bindingPath = nameof(SampleAsset.Description),
            };
            root.Add(descriptionField);

            root.Add(MakeDefaultField("Thumbnail", nameof(SampleAsset.Thumbnail)));
            return root;
        }
    }
}
