using System;
using System.Collections.Generic;
using Authoring;
using Data.Input;
using UnityEngine;

namespace SigningGame.Editor.Signatures {
    public sealed class SignatureSampleSet {
        private SignaturePresetDefinition _targetPreset;
        private List<RecordedSignatureSampleDefinition> _samples = new();

        public SignaturePresetDefinition TargetPreset => _targetPreset;
        public List<RecordedSignatureSampleDefinition> Samples => _samples;
        
        public void SetTargetPreset(SignaturePresetDefinition preset) => _targetPreset = preset;
        
        public void AddSample(RecordedSignatureSampleDefinition sample) => _samples.Add(sample);
    }

    [Serializable]
    public sealed class RecordedSignatureSampleDefinition {
        [SerializeField] private string _id;
        [SerializeField] private string _comment;
        [SerializeField] private List<RecordedSignatureStrokeDefinition> _strokes;
        public string Id => _id;
        public IReadOnlyList<RecordedSignatureStrokeDefinition> Strokes => _strokes;

        public RecordedSignatureSampleDefinition(string id, IReadOnlyList<RecordedSignatureStrokeDefinition> strokes) {
            _id = id;
            _strokes = strokes == null ? new List<RecordedSignatureStrokeDefinition>() : new List<RecordedSignatureStrokeDefinition>(strokes);
        }

        public SignatureAttempt ToAttempt() {
            var strokes = new List<SignatureStrokeAttempt>(_strokes.Count);
            float minimumTime = float.PositiveInfinity, maximumTime = float.NegativeInfinity;
            foreach (RecordedSignatureStrokeDefinition stroke in _strokes) {
                if (stroke == null) continue;
                var points = new List<SignatureInputPoint>(stroke.Points.Count);
                foreach (RecordedSignaturePointDefinition point in stroke.Points) {
                    if (point == null) continue;
                    points.Add(new SignatureInputPoint(point.Position, point.Time));
                    minimumTime = Mathf.Min(minimumTime, point.Time);
                    maximumTime = Mathf.Max(maximumTime, point.Time);
                }
                strokes.Add(new SignatureStrokeAttempt(points));
            }
            float duration = float.IsInfinity(minimumTime) ? 0f : Mathf.Max(0f, maximumTime - minimumTime);
            return new SignatureAttempt(strokes, duration);
        }

        public void AddStroke(RecordedSignatureStrokeDefinition stroke) {
            _strokes.Add(stroke);
        }
    }

    [Serializable]
    public sealed class RecordedSignatureStrokeDefinition {
        [SerializeField] private string _id;
        [SerializeField] private List<RecordedSignaturePointDefinition> _points = new();
        public string Id => _id;
        public IReadOnlyList<RecordedSignaturePointDefinition> Points => _points;
        public RecordedSignatureStrokeDefinition(string id, IReadOnlyList<RecordedSignaturePointDefinition> points) {
            _id = id; _points = points == null ? new List<RecordedSignaturePointDefinition>() : new List<RecordedSignaturePointDefinition>(points);
        }
        
        public void AddPoint(RecordedSignaturePointDefinition point) => _points.Add(point);
    }

    [Serializable]
    public sealed class RecordedSignaturePointDefinition {
        [SerializeField] private string _id;
        [SerializeField] private Vector2 _position;
        [SerializeField] private float _time;
        public string Id => _id;
        public Vector2 Position => _position;
        public float Time => _time;
        public RecordedSignaturePointDefinition(string id, Vector2 position, float time) {
            _id = id; _position = position; _time = time;
        }
    }
}
