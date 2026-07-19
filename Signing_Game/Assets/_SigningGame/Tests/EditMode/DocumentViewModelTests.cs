using System.Collections.Generic;
using System;
using Contracts;
using Data.Input;
using NUnit.Framework;
using Presentation;
using Authoring;
using Data.Enums;
using Data.Requests;
using Data.Results;
using Data.Rules;
using UnityEngine;

namespace SigningGame.Tests.EditMode {
    public sealed class DocumentViewModelTests {
        [Test]
        public void StartStroke_WhenIdle_AutoStartsAttemptFromFirstPointTime() {
            var recorder = new RecordingSignatureRecorder();
            var viewModel = new DocumentViewModel(recorder);
            var firstPoint = Point(0.25f, 0.75f, 7.5f);

            viewModel.StartStroke(firstPoint);

            CollectionAssert.AreEqual(
                new[] { nameof(ISignatureRecorder.BeginAttempt), nameof(ISignatureRecorder.BeginStroke) },
                recorder.Calls);
            Assert.That(recorder.BeginAttemptTime, Is.EqualTo(firstPoint.Time));
            Assert.That(recorder.BeginStrokePoint, Is.SameAs(firstPoint));
            Assert.That(viewModel.IsSigning, Is.True);
            Assert.That(viewModel.IsStrokeActive, Is.True);
            Assert.That(viewModel.CanCompleteSignature, Is.False);
        }

        [Test]
        public void StartStroke_WhenAttemptIsActive_DoesNotRestartAttempt() {
            var recorder = new RecordingSignatureRecorder {
                IsAttemptActive = true
            };
            var viewModel = new DocumentViewModel(recorder);
            var firstPoint = Point(0f, 1f, 3f);

            viewModel.StartStroke(firstPoint);

            CollectionAssert.AreEqual(
                new[] { nameof(ISignatureRecorder.BeginStroke) },
                recorder.Calls);
            Assert.That(recorder.BeginStrokePoint, Is.SameAs(firstPoint));
        }

        [Test]
        public void SignatureOperations_DelegateAndExposeRecorderState() {
            var expectedAttempt = new SignatureAttempt(
                new[] { new SignatureStrokeAttempt(new[] { Point(0f, 0f, 1f) }) },
                4f);
            var recorder = new RecordingSignatureRecorder {
                IsAttemptActive = true,
                CompleteResult = expectedAttempt
            };
            var viewModel = new DocumentViewModel(recorder);
            var firstPoint = Point(0f, 0f, 1f);
            var middlePoint = Point(0.5f, 0.5f, 2f);
            var finalPoint = Point(1f, 1f, 3f);

            viewModel.StartStroke(firstPoint);
            viewModel.AddPoint(middlePoint);
            viewModel.FinishStroke(finalPoint);

            Assert.That(viewModel.CanCompleteSignature, Is.True);

            SignatureAttempt actualAttempt = viewModel.CompleteSignature(5f);
            viewModel.CancelSignature();

            CollectionAssert.AreEqual(
                new[] {
                    nameof(ISignatureRecorder.BeginStroke),
                    nameof(ISignatureRecorder.AddPoint),
                    nameof(ISignatureRecorder.EndStroke),
                    nameof(ISignatureRecorder.CompleteAttempt),
                    nameof(ISignatureRecorder.CancelAttempt)
                },
                recorder.Calls);
            Assert.That(recorder.BeginStrokePoint, Is.SameAs(firstPoint));
            Assert.That(recorder.AddedPoint, Is.SameAs(middlePoint));
            Assert.That(recorder.EndStrokePoint, Is.SameAs(finalPoint));
            Assert.That(recorder.CompleteTime, Is.EqualTo(5f));
            Assert.That(actualAttempt, Is.SameAs(expectedAttempt));
            Assert.That(viewModel.IsSigning, Is.False);
            Assert.That(viewModel.IsStrokeActive, Is.False);
            Assert.That(viewModel.CanCompleteSignature, Is.False);
        }

        [Test]
        public void CollectSignature_WhenUnsigned_ReturnsEmptyZeroDurationAttempt() {
            var viewModel = new DocumentViewModel();

            SignatureAttempt attempt = viewModel.CollectSignature(10f);

            Assert.That(attempt.Strokes, Is.Empty);
            Assert.That(attempt.Duration, Is.Zero);
        }

        [Test]
        public void CollectSignature_WhenSigned_DelegatesCompletion() {
            var expectedAttempt = new SignatureAttempt(Array.Empty<SignatureStrokeAttempt>(), 3f);
            var recorder = new RecordingSignatureRecorder {
                IsAttemptActive = true,
                CompleteResult = expectedAttempt
            };
            var viewModel = new DocumentViewModel(recorder);

            SignatureAttempt attempt = viewModel.CollectSignature(8f);

            Assert.That(attempt, Is.SameAs(expectedAttempt));
            Assert.That(recorder.CompleteTime, Is.EqualTo(8f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void CollectSignature_WithNonFiniteTime_Throws(float endTime) {
            var viewModel = new DocumentViewModel();

            Assert.Throws<ArgumentOutOfRangeException>(() => viewModel.CollectSignature(endTime));
        }

        [Test]
        public void CollectSignature_WithActiveStroke_RemainsStrict() {
            var recorder = new RecordingSignatureRecorder { IsAttemptActive = true };
            var viewModel = new DocumentViewModel(recorder);
            viewModel.StartStroke(Point(0f, 0f, 1f));

            Assert.Throws<InvalidOperationException>(() => viewModel.CollectSignature(2f));
            Assert.That(recorder.Calls, Does.Not.Contain(nameof(ISignatureRecorder.CompleteAttempt)));
        }

        [Test]
        public void CompleteSignature_WhenIdle_RemainsStrict() {
            var viewModel = new DocumentViewModel();

            Assert.Throws<InvalidOperationException>(() => viewModel.CompleteSignature(1f));
        }

        [Test]
        public void OptionalEvaluation_PublishesAndReturnsSameResult() {
            var preset = ScriptableObject.CreateInstance<SignaturePresetDefinition>();
            var expected = new SignatureEvaluationResult(SignatureEvaluationStatus.Accepted, SignatureFailureReason.None,
                1, 1, .5f, "v", new SignatureScoreBreakdown(1, 1, 1, 1, 1),
                Array.Empty<SignatureStrokeMatchResult>());
            var evaluator = new FixedEvaluator(expected);
            var viewModel = new DocumentViewModel(new RecordingSignatureRecorder(), evaluator, preset,
                new SignatureDifficultyRules("d", .5f, 1, 1, 1, new SignatureScoreWeights(1, 0, 0, 0)), SignatureRuleModifiers.None);
            SignatureEvaluationResult actual = viewModel.Evaluate(new SignatureAttempt(Array.Empty<SignatureStrokeAttempt>(), 0));
            Assert.That(viewModel.CanEvaluate, Is.True); Assert.That(actual, Is.SameAs(expected));
            viewModel.Dispose(); Assert.Throws<ObjectDisposedException>(() => viewModel.Evaluate(new SignatureAttempt(Array.Empty<SignatureStrokeAttempt>(), 0)));
            UnityEngine.Object.DestroyImmediate(preset);
        }

        private static SignatureInputPoint Point(float x, float y, float time) {
            return new SignatureInputPoint(new Vector2(x, y), time);
        }

        private sealed class RecordingSignatureRecorder : ISignatureRecorder {
            public readonly List<string> Calls = new();

            public bool IsAttemptActive { get; set; }
            public bool IsStrokeActive { get; private set; }

            public float BeginAttemptTime { get; private set; }
            public SignatureInputPoint BeginStrokePoint { get; private set; }
            public SignatureInputPoint AddedPoint { get; private set; }
            public SignatureInputPoint EndStrokePoint { get; private set; }
            public float CompleteTime { get; private set; }
            public SignatureAttempt CompleteResult { get; set; }

            public void BeginAttempt(float startTime) {
                Calls.Add(nameof(ISignatureRecorder.BeginAttempt));
                BeginAttemptTime = startTime;
                IsAttemptActive = true;
            }

            public void BeginStroke(SignatureInputPoint firstPoint) {
                Calls.Add(nameof(ISignatureRecorder.BeginStroke));
                BeginStrokePoint = firstPoint;
                IsStrokeActive = true;
            }

            public void AddPoint(SignatureInputPoint point) {
                Calls.Add(nameof(ISignatureRecorder.AddPoint));
                AddedPoint = point;
            }

            public void EndStroke(SignatureInputPoint finalPoint) {
                Calls.Add(nameof(ISignatureRecorder.EndStroke));
                EndStrokePoint = finalPoint;
                IsStrokeActive = false;
            }

            public SignatureAttempt CompleteAttempt(float endTime) {
                Calls.Add(nameof(ISignatureRecorder.CompleteAttempt));
                CompleteTime = endTime;
                IsAttemptActive = false;
                return CompleteResult;
            }

            public void CancelAttempt() {
                Calls.Add(nameof(ISignatureRecorder.CancelAttempt));
                IsAttemptActive = false;
                IsStrokeActive = false;
            }
        }

        private sealed class FixedEvaluator : ISignatureEvaluator {
            private readonly SignatureEvaluationResult _result; public FixedEvaluator(SignatureEvaluationResult result) => _result = result;
            public SignatureEvaluationResult Evaluate(SignatureEvaluationRequest request) => _result;
            public SignatureEvaluationResult Evaluate(SignatureAttempt attempt, SignaturePresetDefinition preset,
                SignatureDifficultyRules difficulty, SignatureRuleModifiers modifiers) => _result;
        }
    }
}
