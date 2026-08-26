using System.Linq;
using NUnit.Framework;
using Presentation;
using UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tests.EditMode {
    public sealed class GameplaySettingsPopupSceneTests {
        [Test]
        public void SettingsPopup_IsSceneAuthoredCenteredAndWiredToMenu() {
            Scene scene = EditorSceneManager.OpenScene(
                "Assets/_SigningGame/Scenes/SampleScene.unity", OpenSceneMode.Additive);
            try {
                GameplaySettingsPopupView popup = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<GameplaySettingsPopupView>(true))
                    .Single();
                RectTransform popupRect = popup.GetComponent<RectTransform>();
                Assert.That(popup.gameObject.activeSelf, Is.False);
                Assert.That(popupRect.parent.GetComponent<Canvas>(), Is.Not.Null);
                Assert.That(popupRect.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(popupRect.anchorMax, Is.EqualTo(Vector2.one));

                Transform panel = popup.transform.Find("Settings Panel");
                Assert.That(panel, Is.Not.Null);
                RectTransform panelRect = panel.GetComponent<RectTransform>();
                Assert.That(panelRect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(panelRect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(panelRect.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(panelRect.sizeDelta, Is.EqualTo(new Vector2(360f, 270f)));
                Assert.That(panel.Find("Settings Title"), Is.Not.Null);
                Assert.That(panel.Find("Music Label"), Is.Not.Null);
                Assert.That(panel.Find("Music Slider")?.GetComponent<Slider>(), Is.Not.Null);
                Assert.That(panel.Find("Sound Label"), Is.Not.Null);
                Assert.That(panel.Find("Sound Slider")?.GetComponent<Slider>(), Is.Not.Null);
                Assert.That(panel.Find("Settings Close")?.GetComponent<Button>(), Is.Not.Null);

                var popupState = new SerializedObject(popup);
                foreach (string field in new[] { "_musicSlider", "_soundSlider", "_closeButton" }) {
                    Assert.That(popupState.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
                }

                GameMenuTabView menu = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<GameMenuTabView>(true))
                    .Single();
                SerializedProperty popupReference = new SerializedObject(menu).FindProperty("_settingsPopup");
                Assert.That(popupReference.objectReferenceValue, Is.EqualTo(popup));

                WalletView wallet = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<WalletView>(true))
                    .Single();
                var walletState = new SerializedObject(wallet);
                RewardIncomeDisplay incomePrefab = walletState.FindProperty("_incomePrefab").objectReferenceValue
                    as RewardIncomeDisplay;
                Canvas incomeCanvas = walletState.FindProperty("_incomeCanvas").objectReferenceValue as Canvas;
                RectTransform incomeSpawnRoot = walletState.FindProperty("_incomeSpawnRoot").objectReferenceValue
                    as RectTransform;
                Assert.That(incomePrefab, Is.Not.Null);
                Assert.That(incomeCanvas, Is.Not.Null);
                Assert.That(incomeSpawnRoot, Is.Not.Null);
                Assert.That(incomeSpawnRoot.parent, Is.EqualTo(incomeCanvas.transform));
                Assert.That(incomeSpawnRoot, Is.Not.EqualTo(wallet.transform));
                Assert.That(incomeSpawnRoot.IsChildOf(wallet.transform), Is.False);
                Assert.That(incomeSpawnRoot.GetSiblingIndex(), Is.LessThan(popup.transform.GetSiblingIndex()));

                StampView stamp = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<StampView>(true))
                    .Single();
                Assert.That(stamp.transform.parent, Is.EqualTo(popup.transform.parent));
                Assert.That(stamp.transform.GetSiblingIndex(), Is.LessThan(popup.transform.GetSiblingIndex()));
            } finally {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [TestCase("Assets/_SigningGame/Prefabs/UpgradeUI.prefab")]
        [TestCase("Assets/_SigningGame/Prefabs/MetaUpgradeUI.prefab")]
        public void UpgradeEdgeGraphic_FillsItsContentRect(string prefabPath) {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null);
            UpgradeEdgeGraphic graphic = prefab.GetComponentInChildren<UpgradeEdgeGraphic>(true);
            Assert.That(graphic, Is.Not.Null);

            RectTransform rect = graphic.rectTransform;
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(rect.sizeDelta, Is.EqualTo(Vector2.zero));
        }
    }
}
