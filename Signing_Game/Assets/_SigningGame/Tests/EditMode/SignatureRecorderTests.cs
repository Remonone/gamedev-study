using System;
using Data.Input;
using NUnit.Framework;
using Services;
using UnityEngine;

namespace SigningGame.Tests.EditMode {
    public sealed class SignatureRecorderTests {
        [Test]
        public void CompleteAttempt_ReturnsRecordedStrokeAndDuration_ThenResetsState() {
            var recorder = new SignatureRecorder();
            var firstPoint = Point(0f, 0f, 10.1f);
            var middlePoint = Point(0.5f, 0.5f, 11f);
            var finalPoint = Point(1f, 1f, 12f);

            Assert.That(recorder.IsAttemptActive, Is.False);
            Assert.That(recorder.IsStrokeActive, Is.False);

            recorder.BeginAttempt(10f);
            recorder.BeginStroke(firstPoint);
            recorder.AddPoint(middlePoint);
            recorder.EndStroke(finalPoint);

            Assert.That(recorder.IsAttemptActive, Is.True);
            Assert.That(recorder.IsStrokeActive, Is.False);

            SignatureAttempt attempt = recorder.CompleteAttempt(13.5f);

            Assert.That(attempt.Duration, Is.EqualTo(3.5f));
            Assert.That(attempt.Strokes.Count, Is.EqualTo(1));
            CollectionAssert.AreEqual(
                new[] { firstPoint, middlePoint, finalPoint },
                attempt.Strokes[0].Points);
            Assert.That(recorder.IsAttemptActive, Is.False);
            Assert.That(recorder.IsStrokeActive, Is.False);
        }

        [Test]
        public void CompleteAttempt_TwoStrokesRemainSnapshotAfterRecorderReuse() {
            var recorder = new SignatureRecorder();
            var firstStrokeStart = Point(0f, 0f, 1f);
            var firstStrokeEnd = Point(0.25f, 0.25f, 1.5f);
            var secondStrokeStart = Point(0.5f, 0.5f, 2f);
            var secondStrokeEnd = Point(1f, 1f, 2.5f);

            recorder.BeginAttempt(1f);
            recorder.BeginStroke(firstStrokeStart);
            recorder.EndStroke(firstStrokeEnd);
            recorder.BeginStroke(secondStrokeStart);
            recorder.EndStroke(secondStrokeEnd);
            SignatureAttempt firstAttempt = recorder.CompleteAttempt(3f);

            var reusedStart = Point(0.1f, 0.9f, 10f);
            var reusedEnd = Point(0.9f, 0.1f, 11f);
            recorder.BeginAttempt(10f);
            recorder.BeginStroke(reusedStart);
            recorder.EndStroke(reusedEnd);
            SignatureAttempt secondAttempt = recorder.CompleteAttempt(12f);

            Assert.That(firstAttempt.Strokes.Count, Is.EqualTo(2));
            CollectionAssert.AreEqual(
                new[] { firstStrokeStart, firstStrokeEnd },
                firstAttempt.Strokes[0].Points);
            CollectionAssert.AreEqual(
                new[] { secondStrokeStart, secondStrokeEnd },
                firstAttempt.Strokes[1].Points);
            Assert.That(secondAttempt.Strokes.Count, Is.EqualTo(1));
            CollectionAssert.AreEqual(
                new[] { reusedStart, reusedEnd },
                secondAttempt.Strokes[0].Points);
        }

        [Test]
        public void CancelAttempt_DiscardsActiveStrokeAndAllowsCleanReuse() {
            var recorder = new SignatureRecorder();

            recorder.BeginAttempt(1f);
            recorder.BeginStroke(Point(0f, 0f, 1f));
            recorder.AddPoint(Point(0.5f, 0.5f, 2f));
            recorder.CancelAttempt();

            Assert.That(recorder.IsAttemptActive, Is.False);
            Assert.That(recorder.IsStrokeActive, Is.False);
            Assert.DoesNotThrow(recorder.CancelAttempt);

            recorder.BeginAttempt(10f);
            SignatureAttempt nextAttempt = recorder.CompleteAttempt(11f);

            Assert.That(nextAttempt.Strokes, Is.Empty);
            Assert.That(nextAttempt.Duration, Is.EqualTo(1f));
        }

        [Test]
        public void OperationsWithoutRequiredState_ThrowInvalidOperationException() {
            var recorder = new SignatureRecorder();
            SignatureInputPoint point = Point(0f, 0f, 1f);

            Assert.Throws<InvalidOperationException>(() => recorder.BeginStroke(point));
            Assert.Throws<InvalidOperationException>(() => recorder.AddPoint(point));
            Assert.Throws<InvalidOperationException>(() => recorder.EndStroke(point));
            Assert.Throws<InvalidOperationException>(() => recorder.CompleteAttempt(1f));

            recorder.BeginAttempt(1f);

            Assert.Throws<InvalidOperationException>(() => recorder.BeginAttempt(2f));
            Assert.Throws<InvalidOperationException>(() => recorder.AddPoint(point));
            Assert.Throws<InvalidOperationException>(() => recorder.EndStroke(point));

            recorder.BeginStroke(point);

            Assert.Throws<InvalidOperationException>(() => recorder.BeginStroke(point));
            Assert.Throws<InvalidOperationException>(() => recorder.CompleteAttempt(2f));
        }

        [Test]
        public void CompleteAttempt_EndBeforeStartThrowsAndKeepsAttemptActive() {
            var recorder = new SignatureRecorder();
            recorder.BeginAttempt(5f);

            Assert.Throws<ArgumentOutOfRangeException>(() => recorder.CompleteAttempt(4f));
            Assert.That(recorder.IsAttemptActive, Is.True);

            recorder.CancelAttempt();
        }

        private static SignatureInputPoint Point(float x, float y, float time) {
            return new SignatureInputPoint(new Vector2(x, y), time);
        }
    }
}
