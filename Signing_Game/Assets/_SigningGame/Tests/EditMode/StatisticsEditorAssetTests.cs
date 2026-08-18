using Data.Statistics;
using NUnit.Framework;
using Presentation;
using UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tests.EditMode {
    public sealed class StatisticsEditorAssetTests {
        [Test]
        public void StatisticsPrefabAndLayout_HaveRequiredReferences() {
            var layout = AssetDatabase.LoadAssetAtPath<StatisticsTabLayoutDefinition>(
                "Assets/_SigningGame/Statistics/DefaultStatisticsTabLayout.asset");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_SigningGame/Prefabs/Statistics/StatisticsUI.prefab");

            Assert.That(layout, Is.Not.Null);
            Assert.That(layout.Categories.Count, Is.GreaterThan(0));
            Assert.That(prefab, Is.Not.Null);
            StatisticsTabView view = prefab.GetComponentInChildren<StatisticsTabView>(true);
            PullTabView pullTab = prefab.GetComponentInChildren<PullTabView>(true);
            Assert.That(view, Is.Not.Null);
            Assert.That(pullTab, Is.Not.Null);

            var viewState = new SerializedObject(view);
            foreach (string field in new[] {
                         "_pullTab", "_layout", "_contentRoot", "_rowPrefab", "_categoryHeaderPrefab"
                     }) {
                Assert.That(viewState.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            }

            var pullState = new SerializedObject(pullTab);
            foreach (string field in new[] {
                         "_pulledObject", "_startPosition", "_stopPosition", "_disabledPosition"
                     }) {
                Assert.That(pullState.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            }
        }

        [Test]
        public void SampleScene_RegistersStatisticsAsSixthPullTab() {
            Scene scene = EditorSceneManager.OpenScene(
                "Assets/_SigningGame/Scenes/SampleScene.unity", OpenSceneMode.Additive);
            try {
                PullTabGroupView group = null;
                StatisticsTabView statisticsView = null;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int index = 0; index < roots.Length; index++) {
                    group ??= roots[index].GetComponentInChildren<PullTabGroupView>(true);
                    statisticsView ??= roots[index].GetComponentInChildren<StatisticsTabView>(true);
                }

                Assert.That(group, Is.Not.Null);
                Assert.That(statisticsView, Is.Not.Null);
                var groupState = new SerializedObject(group);
                SerializedProperty tabs = groupState.FindProperty("_tabs");
                Assert.That(tabs.arraySize, Is.EqualTo(6));
                Object sixthTab = tabs.GetArrayElementAtIndex(5).objectReferenceValue;
                Assert.That(sixthTab, Is.SameAs(statisticsView.GetComponentInParent<Transform>()
                    .parent.GetComponentInChildren<PullTabView>(true)));
            } finally {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
