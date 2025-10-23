namespace Coherence.Editor.Tests
{
    using System.Linq;
    using Editor;
    using Coherence.Tests;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;

    public class PrefabUtilsTests : CoherenceTest
    {
        private const string ResourcesPath = "FindAllVariants";

        [TestCase("TestBasePrefab", new[]
         {
             "TestPrefabVariant1",
             "TestPrefabVariant2",
             "TestPrefabVariant1NestedVariant1",
             "TestPrefabVariant1NestedVariant2"
         }),
         TestCase("TestPrefabVariant1", new[]
         {
             "TestPrefabVariant1NestedVariant1",
             "TestPrefabVariant1NestedVariant2"
         }),
         TestCase("TestPrefabVariant2", new string[0]),
         TestCase("NotVariant", new string[0]),
         TestCase("TestPrefabVariant1NestedVariant1", new string[0]),
         TestCase("TestPrefabVariant1NestedVariant2", new string[0])]
        public void FindAllVariants_Finds_All_Variants_Of_Prefab(string basePrefabName, string[] expectedResults)
        {
            var prefabs = Resources.LoadAll<Transform>(ResourcesPath);
            var basePrefab = prefabs.Single(x => x.name == basePrefabName);
            var results = PrefabUtils.FindAllVariants(basePrefab, prefabs).Select(x => x.name);
            Assert.That(results, Is.EquivalentTo(expectedResults));
        }

        [TestCase("TestBasePrefab", new[]
         {
             "TestPrefabVariant1",
             "TestPrefabVariant2",
             "TestPrefabVariant1NestedVariant1",
             "TestPrefabVariant1NestedVariant2"
         }),
         TestCase("TestPrefabVariant1", new[]
         {
             "TestPrefabVariant1NestedVariant1",
             "TestPrefabVariant1NestedVariant2"
         }),
         TestCase("TestPrefabVariant2", new string[0]),
         TestCase("NotVariant", new string[0]),
         TestCase("TestPrefabVariant1NestedVariant1", new string[0]),
         TestCase("TestPrefabVariant1NestedVariant2", new string[0])]
        public void FindAllVariants_Finds_All_Variants_Of_PrefabStage_Root_Object(string basePrefabName, string[] expectedResults)
        {
            var prefabs = Resources.LoadAll<Transform>(ResourcesPath);
            var basePrefab = prefabs.Single(x => x.name == basePrefabName);
            var prefabStage = PrefabStageUtility.OpenPrefab(AssetDatabase.GetAssetPath(basePrefab));
            var prefabStageRoot = prefabStage.prefabContentsRoot.transform;
            var results = PrefabUtils.FindAllVariants(prefabStageRoot, prefabs).Select(x => x.name);
            Assert.That(results, Is.EquivalentTo(expectedResults));
            StageUtility.GoToMainStage();
        }
    }
}
