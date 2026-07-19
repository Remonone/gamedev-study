using System;
using System.Collections.Generic;
using Contracts;
using Data.Input;
using Data.Processed;
using Data.Rules;
using UnityEngine;

namespace Services {
    public class SignaturePreprocessor : IService, ISignaturePreprocessor {
        
        private const float _SmoothingFactor = 0.15f;
        private const float _GeometryEpsilon = 0.000001f;

        public ProcessedSignature Process(SignatureAttempt attempt, SignatureProcessingRules rules) {
            if (attempt == null) throw new ArgumentNullException(nameof(attempt));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            ValidateRules(rules);
            if (attempt.Strokes.Count == 0) return null;

            List<List<Vector2>> signatureStrokes = new List<List<Vector2>>();
            
            foreach (var stroke in attempt.Strokes) {
                if (stroke == null || stroke.Points == null) continue;
                if (stroke.Points.Count < 2) continue;
                if(stroke.Points.Count > rules.MaximumInputPointCount) continue;
                
                List<Vector2> filteredPoints = FilterStroke(stroke.Points, rules.MinimumInputPointDistance);

                if (filteredPoints.Count < rules.MinimumUsablePointCountPerStroke) {
                    continue;
                }
                var strokeLength = CalculateStrokeLength(filteredPoints);
                if (strokeLength < rules.MinimumStrokeLength) continue;
                
                
                var resampledPoints = ResampleStroke(filteredPoints, rules.ResampledPointCountPerStroke);

                if (resampledPoints.Count < 2) continue;
                
                SmoothStroke(resampledPoints, rules.SmoothingPasses);

                filteredPoints = ResampleStroke(resampledPoints, rules.ResampledPointCountPerStroke);
                signatureStrokes.Add(filteredPoints);
            }

            if (signatureStrokes.Count == 0) return null;
            
            if(!TryCalculateBounds(signatureStrokes, out Rect bounds)) return null;
            if (!NormalizeToUnitSquare(bounds, ref signatureStrokes, out Rect normalizedBounds)) return null;
            
            var processedStrokes = new List<ProcessedSignatureStroke>(signatureStrokes.Count);
            float totalLength = 0f;

            foreach (var stroke in signatureStrokes) {
                ProcessedSignatureStroke processedStroke = BuildProcessedStroke(stroke);

                if (processedStroke == null) continue;
                
                processedStrokes.Add(processedStroke);
                totalLength += processedStroke.Length;
            }
            
            if (processedStrokes.Count == 0) return null;
            
            return new ProcessedSignature(processedStrokes, normalizedBounds, totalLength);
        }

        private List<Vector2> ResampleStroke(List<Vector2> input, int outputPointCount) {
            if (input.Count < 2 || outputPointCount < 2) return new List<Vector2>();
            
            var refinedPoints = RemoveDegeneratePoints(input);
            
            if (refinedPoints.Count < 2) return new List<Vector2>();
            
            var cumulativeDistances = new float[refinedPoints.Count];

            for (int i = 1; i < refinedPoints.Count; i++) {
                cumulativeDistances[i] = cumulativeDistances[i - 1] + Vector2.Distance(refinedPoints[i - 1], refinedPoints[i]);
            }
            
            float totalLength = cumulativeDistances[^1];
            
            if (totalLength <= _GeometryEpsilon) return new List<Vector2>();

            var result = new List<Vector2>(outputPointCount) { refinedPoints[0] };
            
            int segmentIndex = 1;

            for (int outputIndex = 1; outputIndex < outputPointCount - 1; outputIndex++) {
                float targetDistance = outputIndex / ((float)outputPointCount - 1) * totalLength;
                while (segmentIndex < refinedPoints.Count - 1 &&
                       cumulativeDistances[segmentIndex] <
                       targetDistance) {
                    segmentIndex++;
                }
                float segmentStartDistance = cumulativeDistances[segmentIndex - 1];
                float segmentEndDistance = cumulativeDistances[segmentIndex];
                float segmentLength = segmentEndDistance - segmentStartDistance;

                if (segmentLength <= _GeometryEpsilon) {
                    result.Add(refinedPoints[segmentIndex]);
                    continue;
                }
                
                float interpolation = (targetDistance - segmentStartDistance) / segmentLength;
                result.Add(Vector2.LerpUnclamped(refinedPoints[segmentIndex - 1], refinedPoints[segmentIndex], interpolation));
                
            }
            result.Add(refinedPoints[^1]);
            return result;
        }

        private List<Vector2> RemoveDegeneratePoints(IReadOnlyList<Vector2> input) {
            if (input.Count < 2) return new List<Vector2>();
            
            float epsilonSquared = _GeometryEpsilon * _GeometryEpsilon;

            var result = new List<Vector2>(input.Count) { input[0] };
            
            for (int i = 1; i < input.Count; i++) {
                Vector2 point = input[i];
                
                if((point - result[^1]).sqrMagnitude <= epsilonSquared) continue;
                
                result.Add(point);
            }
            
            Vector2 endpoint = input[^1];

            if (result.Count == 1) {
                result.Add(endpoint);
            } else if ((endpoint - result[^1]).sqrMagnitude <= epsilonSquared) {
                result[^1] = endpoint;
            } else {
              result.Add(endpoint);  
            }

            return result;
        }

        private bool NormalizeToUnitSquare(Rect bounds, ref List<List<Vector2>> strokes, out Rect normalizedBounds) {
            float maxDimension = Mathf.Max(bounds.width, bounds.height);

            if (maxDimension <= _GeometryEpsilon) {
                normalizedBounds = default;
                strokes.Clear();
                return false;
            }

            float scale = 1f / maxDimension;
            
            float normalizedWidth = bounds.width * scale;
            float normalizedHeight = bounds.height * scale;
            
            Vector2 centerOffset = new((1f - normalizedWidth) / 2f, (1f - normalizedHeight) / 2f);
            
            Vector2 sourceOrigin = new Vector2(bounds.xMin, bounds.yMin);

            for (int i = 0; i < strokes.Count; i++) {
                var stroke = strokes[i];
                for (int j = 0; j < stroke.Count; j++) {
                    Vector2 normalizedPoint = (stroke[j] - sourceOrigin) * scale + centerOffset;
                    stroke[j] = normalizedPoint;
                }
            }
            
            normalizedBounds = new Rect(centerOffset.x, centerOffset.y, normalizedWidth, normalizedHeight);
            return true;
        }

        private void SmoothStroke(List<Vector2> input, int passes) {
            if (input.Count < 3 || passes <= 0) return;
            
            var source = new Vector2[input.Count];
            var destination = new Vector2[input.Count];
            for (int i = 0; i < input.Count; i++) source[i] = input[i];
            for (int pass = 0; pass < passes; pass++) {
                destination[0] = source[0];
                destination[^1] = source[^1];
                for (int i = 1; i < input.Count - 1; i++) {
                    destination[i] = source[i - 1] * _SmoothingFactor + source[i] * (1 - _SmoothingFactor * 2) + source[i + 1] * _SmoothingFactor;
                }
                (source, destination) = (destination, source);
            }
            for (int i = 0; i < input.Count; i++) input[i] = source[i];
        }
        
        private float CalculateStrokeLength(List<Vector2> rawPoints) {
            float length = 0;
            for (int i = 1; i < rawPoints.Count; i++) {
                length += Vector2.Distance(rawPoints[i - 1], rawPoints[i]);
            }
            return length;
        }

        private ProcessedSignatureStroke BuildProcessedStroke(List<Vector2> points) {
            if (points.Count < 2) return null;

            var cumulativeDistances = new float[points.Count];

            for (int i = 1; i < points.Count; i++) {
                cumulativeDistances[i] = cumulativeDistances[i - 1] + Vector2.Distance(points[i - 1], points[i]);
            }

            float strokeLength = cumulativeDistances[^1];
            
            if (strokeLength <= _GeometryEpsilon) return null;
            
            var processedPoints = new List<ProcessedSignaturePoint>(points.Count);

            for (int i = 0; i < points.Count; i++) {
                float pathProgress = cumulativeDistances[i] / strokeLength;

                Vector2 direction = CalculateDirection(points, i);
                
                processedPoints.Add(new ProcessedSignaturePoint(points[i], direction, pathProgress));
            }
            
            return new ProcessedSignatureStroke(processedPoints, strokeLength);
        }

        private static Vector2 CalculateDirection(IReadOnlyList<Vector2> points, int index) {
            Vector2 difference;

            if (index == 0) {
                difference = points[1] - points[0];
            } else if (index == points.Count - 1) {
                difference = points[^1] - points[^2];
            }
            else {
                difference = points[index + 1] - points[index - 1];
            }

            if (difference.sqrMagnitude < _GeometryEpsilon * _GeometryEpsilon) {
                return Vector2.zero;
            }
            
            return difference.normalized;
        }
        
        private List<Vector2> FilterStroke(IReadOnlyList<SignatureInputPoint> input, float pointDistance) {
            var finitePoints = new List<Vector2>(input.Count);
            
            foreach (SignatureInputPoint inputPoint in input) {
                if (inputPoint == null)
                    continue;
                Vector2 position = inputPoint.Position;

                if (!IsFinite(position))
                    continue;

                finitePoints.Add(position);
            }

            if (finitePoints.Count < 2)
                return new List<Vector2>();

            float minimumDistanceSquared = Mathf.Max(0f, pointDistance) * Mathf.Max(0f, pointDistance);

            var result = new List<Vector2>(finitePoints.Count) { finitePoints[0] };

            for (int i = 1; i < finitePoints.Count - 1; i++) {
                Vector2 currentPoint = finitePoints[i];
                Vector2 lastAcceptedPoint = result[^1];

                if ((currentPoint - lastAcceptedPoint).sqrMagnitude < minimumDistanceSquared)
                    continue;

                result.Add(currentPoint);
            }
            
            Vector2 endpoint = finitePoints[^1];

            if (result.Count == 1) {
                result.Add(endpoint);
            } else if ((endpoint - result[^1]).sqrMagnitude < minimumDistanceSquared) {
                result[^1] = endpoint;
            } else {
                result.Add(endpoint);
            }

            return result;
        }
        
        private static bool IsFinite(Vector2 point) {
            return IsFinite(point.x) && IsFinite(point.y);
        }

        private static bool IsFinite(float value) {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void ValidateRules(SignatureProcessingRules rules) {
            if (!IsFinite(rules.MinimumInputPointDistance) || rules.MinimumInputPointDistance < 0f ||
                rules.MinimumUsablePointCountPerStroke < 2 || !IsFinite(rules.MinimumStrokeLength) ||
                rules.MinimumStrokeLength < 0f || rules.ResampledPointCountPerStroke < 2 ||
                rules.SmoothingPasses < 0 || rules.MaximumInputPointCount < rules.MinimumUsablePointCountPerStroke)
                throw new ArgumentException("Processing rules are invalid.", nameof(rules));
        }
        
        private static bool TryCalculateBounds(IReadOnlyList<List<Vector2>> strokes, out Rect bounds) {
            var xMin = float.PositiveInfinity;
            var yMin = float.PositiveInfinity;
            var xMax = float.NegativeInfinity;
            var yMax = float.NegativeInfinity;

            var hasPoint = false;
            foreach (IReadOnlyList<Vector2> stroke in strokes) {
                foreach (Vector2 point in stroke) {
                    if (!IsFinite(point))
                        continue;

                    hasPoint = true;

                    xMin = Mathf.Min(xMin, point.x);
                    yMin = Mathf.Min(yMin, point.y);
                    xMax = Mathf.Max(xMax, point.x);
                    yMax = Mathf.Max(yMax, point.y);
                }
            }

            if (!hasPoint) {
                bounds = default;
                return false;
            }

            bounds = Rect.MinMaxRect(xMin, yMin, xMax, yMax);

            return true;
        }

        public void Dispose() {
            
        }
    }
}
