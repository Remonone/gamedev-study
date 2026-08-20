using System.Collections.Generic;
using NUnit.Framework;
using UI;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode {
    public sealed class SignatureGuidanceUiTests {
        private readonly List<Object> _objects = new();

        [TearDown]
        public void TearDown() {
            for (int index = _objects.Count - 1; index >= 0; index--) {
                if (_objects[index] != null) Object.DestroyImmediate(_objects[index]);
            }
            _objects.Clear();
        }

        [Test]
        public void SetStrokes_StartsEveryStrokeAtFullAlpha_AndKeepsCollectionsSynchronized() {
            SignatureGraphic graphic = Track(new GameObject("SignatureGraphic").AddComponent<SignatureGraphic>());
            var strokes = new List<IReadOnlyList<Vector2>> {
                new[] { Vector2.zero, Vector2.right },
                new[] { Vector2.up, Vector2.one }
            };

            graphic.SetStrokes(strokes);
            Assert.That(graphic.StrokeCount, Is.EqualTo(2));
            Assert.That(graphic.GetStrokeAlpha(0), Is.EqualTo(1f));
            Assert.That(graphic.GetStrokeAlpha(1), Is.EqualTo(1f));

            graphic.SetStrokeAlphas(new[] { 0.2f, 0.6f });
            Assert.That(graphic.GetStrokeAlpha(0), Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(graphic.GetStrokeAlpha(1), Is.EqualTo(0.6f).Within(0.0001f));

            graphic.Clear();
            Assert.That(graphic.StrokeCount, Is.Zero);
            Assert.That(() => graphic.GetStrokeAlpha(0), Throws.Exception);
        }

        [Test]
        public void Mapping_UsesTheGuideRectLocalCoordinates() {
            Vector2 mapped = SigningField.MapNormalizedPosition(new Rect(-10f, 20f, 100f, 50f),
                new Vector2(0.25f, 0.8f));

            Assert.That(mapped, Is.EqualTo(new Vector2(15f, 60f)));
        }

        [Test]
        public void DocumentPrefab_WiresGuideBehindPlayerInkWithoutRaycasts() {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_SigningGame/Prefabs/Document.prefab");
            Assert.That(prefab, Is.Not.Null);

            SigningField field = prefab.GetComponentInChildren<SigningField>(true);
            Assert.That(field, Is.Not.Null);
            SerializedObject serialized = new(field);
            SignatureGraphic guide = serialized.FindProperty("_guideGraphic").objectReferenceValue as SignatureGraphic;
            SignatureGraphic player = serialized.FindProperty("_signatureGraphic").objectReferenceValue as SignatureGraphic;
            Assert.That(guide, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(guide.raycastTarget, Is.False);
            Assert.That(guide.transform.GetSiblingIndex(), Is.LessThan(player.transform.GetSiblingIndex()));
        }

        private T Track<T>(T value) where T : Object {
            _objects.Add(value);
            return value;
        }
    }
}
