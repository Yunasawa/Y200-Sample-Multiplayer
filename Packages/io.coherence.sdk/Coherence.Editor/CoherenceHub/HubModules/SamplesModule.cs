// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

// This module uses UI Toolkit's TabView, which was introduced in Unity 2023.2.
// However, GetTab was introduced in 6000.0.38f1.
// To be safe, we're bumping the minimum version required to use the TabView to 6.1
// https://docs.unity3d.com/Manual/UIE-uxml-element-TabView.html
// If this condition is changed, update SampleAsset.cs too.
#if UNITY_6000_1_OR_NEWER
#define USE_TABVIEW
#endif

namespace Coherence.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UI;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;

    [HubModule(Priority = 30)]
    public class SamplesModule : HubModule
    {
        public override string ModuleName => "Samples";
        private IEnumerable<SampleAsset> dialogs = Enumerable.Empty<SampleAsset>();
        private IEnumerable<SampleAsset> visibleDialogs = Enumerable.Empty<SampleAsset>();
        private GridView<SampleAsset> gridView;
        private Label descriptionLabel;
        private VisualElement footerRightContainer;
        private VisualElement footerLeftContainer;
        private VisualElement headerContainer;
        private SampleAsset selected;
        private ToolbarSearchField searchField;

#if USE_TABVIEW
        private TabView tabView;
#endif

        public override VisualElement CreateGUI()
        {
            var assets = UIUtils.SampleAssets;
            if (assets.Length == 0)
            {
                return new VisualElement();
            }

            dialogs = assets
                .Where(asset => asset.Enabled)
                .OrderBy(dialog => dialog.Priority);

            var root = new VisualElement();
            root.AddToClassList(Styles.unityThemeVariables);
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(Paths.sampleDialogPickerUss);
            root.styleSheets.Add(styleSheet);

            headerContainer = new VisualElement();
            headerContainer.AddToClassList(Styles.sampleDialogPickerHeader);
            searchField = new ToolbarSearchField
            {
                viewDataKey = "hub_search_field",
            };
            searchField.AddToClassList(Styles.gridViewHeaderSearchField);
            searchField.RegisterValueChangedCallback(evt => gridView.FilterString = evt.newValue);
            headerContainer.Add(searchField);

            footerRightContainer = new VisualElement();
            footerRightContainer.AddToClassList(Styles.classButtons);
            footerLeftContainer = new VisualElement();
            footerLeftContainer.AddToClassList(Styles.classButtonsLeft);

            visibleDialogs = dialogs;
            gridView = new GridView<SampleAsset>
            {
                multiSelection = false,
                viewDataKey = "hub_samples_gridview",
            };
            gridView.onSelectionChanged += OnSelectionChanged;
            gridView.onItemsActivated += OnItemsActivated;
            gridView.onCreateItem += OnCreateItem;
            gridView.Items = visibleDialogs;

#if USE_TABVIEW
            tabView = CreateTabView();
            root.Add(tabView);
#endif
            root.Add(headerContainer);
            root.Add(gridView);

            var footer = new VisualElement();
            footer.AddToClassList(Styles.sceneTemplateDialogFooter);
            footer.Add(footerLeftContainer);
            footer.Add(footerRightContainer);
            root.Add(footer);

            gridView.SetSelection(visibleDialogs.FirstOrDefault());

            return root;
        }

        private void OnCreateItem(SampleAsset sampleAsset, VisualElement icon, VisualElement label)
        {
            var stamp = new Label();
            stamp.AddToClassList(Styles.stamp);
            icon.Add(stamp);
            var image = new VisualElement();
            if (UIUtils.TryGetSample(sampleAsset.SampleDisplayName, out var sample))
            {
                var isUnityPackage = UIUtils.IsUnityPackageSample(sample);
                if (isUnityPackage)
                {
                    stamp.AddToClassList(Styles.stampPackage);
                    stamp.text = ".UNITYPACKAGE";
                }
                else
                {
                    var imports = UIUtils.GetSampleImports(sample, sampleAsset.PreviousSampleDisplayNames).ToArray();
                    if (imports.Length > 0)
                    {
                        var isInstalled = Array.IndexOf(imports, sample.importPath) != -1;
                        if (isInstalled)
                        {
                            stamp.AddToClassList(Styles.stampValid);
                            stamp.text = "IMPORTED";
                        }
                        else
                        {
                            stamp.text = "UPDATE AVAILABLE";
                        }
                    }
                    else
                    {
                        icon.Remove(stamp);
                    }
                }
            }
            else
            {
                stamp.AddToClassList(Styles.stampLink);
                stamp.text = "LINK";
            }

            image.AddToClassList(Styles.itemIcon);
            icon.Add(image);
        }

        private void OnClickImport()
        {
            var success = UIUtils.ImportSample(selected);
            Analytics.Capture(Analytics.Events.Sample,
                ("success", success),
                ("type", "import"),
                ("name", selected.Name),
                ("category", selected.Category),
                ("through_footer", true));
        }

#if USE_TABVIEW
        private TabView CreateTabView()
        {
            var categories = dialogs.Select(dialog => dialog.Category).Distinct();
            var tabView = new TabView
            {
                viewDataKey = nameof(SamplesModule),
            };
            tabView.AddToClassList(Styles.tabView);

            tabView.activeTabChanged += OnActiveTabChanged;
            void OnActiveTabChanged(Tab previous, Tab current)
            {
                visibleDialogs = dialogs.Where(dialog => dialog.Category == current.label).ToList();
                gridView.Items = visibleDialogs;
                if (previous != current)
                {
                    gridView.SetSelection(visibleDialogs.FirstOrDefault());
                }
            }

            foreach (var category in categories)
            {
                var tab = new Tab
                {
                    label = category,
                    viewDataKey = "hub_samples_tab_" + category,
                };
                tabView.Add(tab);
            }

            return tabView;
        }
#endif

        public void SetCategory(string category)
        {
#if USE_TABVIEW
            for (var i = 0; i < tabView.childCount; i++)
            {
                var tab = tabView.GetTab(i);
                if (tab.label == category)
                {
                    tabView.selectedTabIndex = i;
                    tabView.MarkDirtyRepaint();
                    return;
                }
            }
#endif
        }

        private void UpdateFooter()
        {
            Debug.Assert(footerLeftContainer != null);
            Debug.Assert(footerRightContainer != null);

            footerLeftContainer.Clear();
            footerRightContainer.Clear();

            if (!selected)
            {
                return;
            }

            var hasSample = !string.IsNullOrEmpty(selected.SampleDisplayName);
            if (hasSample)
            {
                var importButton = new Button(OnClickImport)
                {
                    text = "Import",
                    viewDataKey = "import_button",
                };
                importButton.AddToClassList(Styles.classButton);
                footerRightContainer.Add(importButton);
                importButton.AddToClassList("button-main");
            }

            // add in reverse order, since we draw right to left
            for (var index = selected.Links.Length - 1; index >= 0; index--)
            {
                var link = selected.Links[index];
                var button = new Button(() =>
                {
                    Application.OpenURL(link.GetResolvedUrl());
                    Analytics.Capture(Analytics.Events.Sample,
                        ("success", true),
                        ("type", "link"),
                        ("name", selected.Name),
                        ("category", selected.Category),
                        ("link_name", link.Name),
                        ("through_footer", true));
                })
                {
                    text = link.Name,
                    viewDataKey = "link_button_" + index,
                };
                button.AddToClassList(Styles.classButton);
                footerRightContainer.Add(button);
                if (!hasSample && index == selected.MainLink)
                {
                    button.AddToClassList("button-main");
                }
            }

            if (string.IsNullOrEmpty(selected.OpenAssetPath))
            {
                return;
            }

            if (UIUtils.TryGetSample(selected.SampleDisplayName, out var sample))
            {
                string path = null;
                if (UIUtils.IsUnityPackageSample(sample))
                {
                    path = selected.OpenAssetPath;
                }
                else
                {
                    var import = UIUtils.GetSampleImports(sample, selected.PreviousSampleDisplayNames).FirstOrDefault();
                    if (import != null)
                    {
                        path = PathUtils.GetRelativePath(import) + "/" + selected.OpenAssetPath;
                    }
                }

                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (obj is GameObject go)
                {
                    var button = new Button(() =>
                    {
                        var instances = PrefabUtility.FindAllInstancesOfPrefab(go);
                        var instance = instances.Length > 0 ? instances[0] : PrefabUtility.InstantiatePrefab(go);
                        EditorGUIUtility.PingObject(instance);
                        Selection.activeObject = instance;
                        Analytics.Capture(Analytics.Events.Sample,
                            ("success", true),
                            ("type", "add_to_scene"),
                            ("name", selected.Name),
                            ("category", selected.Category));
                    })
                    {
                        text = "Add to Scene",
                        viewDataKey = "button_add_to_scene",
                    };
                    button.AddToClassList(Styles.classButtonLeft);
                    footerLeftContainer.Add(button);
                }
                else if (obj is SceneAsset)
                {
                    var button = new Button(() =>
                    {
                        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                        EditorGUIUtility.PingObject(scene.handle);
                        var sceneAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                        Selection.activeObject = sceneAsset;
                        Analytics.Capture(Analytics.Events.Sample,
                            ("success", true),
                            ("type", "open_scene"),
                            ("name", selected.Name),
                            ("category", selected.Category));
                    })
                    {
                        text = "Open Scene",
                        viewDataKey = "button_open_scene",
                    };
                    button.AddToClassList(Styles.classButtonLeft);
                    footerLeftContainer.Add(button);
                }
            }
        }

        private void OnSelectionChanged(IEnumerable<SampleAsset> previous, IEnumerable<SampleAsset> current)
        {
            var asset = current.FirstOrDefault();
            selected = asset;
            UpdateFooter();
        }

        private static void OnItemsActivated(IEnumerable<SampleAsset> items)
        {
            var asset = items.FirstOrDefault();
            if (!asset)
            {
                return;
            }

            if (UIUtils.HasImportableSample(asset))
            {
                var success = UIUtils.ImportSample(asset);
                Analytics.Capture(Analytics.Events.Sample,
                    ("success", success),
                    ("type", "import"),
                    ("name", asset.Name),
                    ("category", asset.Category),
                    ("through_footer", false));
            }
            else if (asset.Links.Length > 0)
            {
                var link = asset.Links[asset.MainLink];
                Application.OpenURL(link.GetResolvedUrl());
                Analytics.Capture(Analytics.Events.Sample,
                    ("success", true),
                    ("type", "link"),
                    ("name", asset.Name),
                    ("category", asset.Category),
                    ("link_name", link.Name),
                    ("through_footer", false));
            }
        }
    }
}
