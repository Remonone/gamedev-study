using System;
using System.Collections.Generic;
using Contracts;
using Data.Processed;
using Data.Results;
using Data.Rules;
using Data.Templates;
using UnityEngine;

namespace Services {
    public sealed class SignatureMatcher : IService, ISignatureMatcher {
        private const float TieEpsilon = 0.000001f;
        private const float DirectionEpsilonSquared = 0.000000000001f;
        private const float HalfLog = 0.6931471805599453f;

        public SignatureVariantMatchResult Match(ProcessedSignature input, SignatureTemplateVariant template,
            ResolvedSignatureRules rules) {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            int templateCount = template.Strokes.Count;
            int inputCount = input.Strokes.Count;
            var diagnostics = new LocalDiagnostics[templateCount, inputCount];
            for (int i = 0; i < templateCount; i++)
                for (int j = 0; j < inputCount; j++)
                    diagnostics[i, j] = Diagnose(template.Strokes[i], input.Strokes[j], rules);

            var costs = new float[templateCount + 1, inputCount + 1];
            var choices = new Choice[templateCount + 1, inputCount + 1];
            for (int i = templateCount; i >= 0; i--) {
                for (int j = inputCount; j >= 0; j--) {
                    if (i == templateCount && j == inputCount) continue;
                    float best = float.PositiveInfinity;
                    Choice bestChoice = Choice.None;
                    int bestPriority = int.MaxValue;
                    if (i < templateCount && j < inputCount)
                        Select(1f - diagnostics[i, j].Similarity + costs[i + 1, j + 1], Choice.Match, 0,
                            ref best, ref bestChoice, ref bestPriority);
                    if (i < templateCount && !template.Strokes[i].Required)
                        Select(costs[i + 1, j], Choice.SkipOptional, 1, ref best, ref bestChoice, ref bestPriority);
                    if (j < inputCount)
                        Select(1f + costs[i, j + 1], Choice.SkipInput, 2, ref best, ref bestChoice, ref bestPriority);
                    if (i < templateCount && template.Strokes[i].Required)
                        Select(1f + costs[i + 1, j], Choice.SkipRequired, 3, ref best, ref bestChoice, ref bestPriority);
                    costs[i, j] = best;
                    choices[i, j] = bestChoice;
                }
            }

            var results = new List<SignatureStrokeMatchResult>();
            float fitSum = 0f, coverageSum = 0f, geometryWeight = 0f;
            float directionSum = 0f, directionWeight = 0f;
            int requiredCount = 0, matchedRequired = 0, extras = 0;
            bool absentPositiveRequired = false;
            for (int k = 0; k < templateCount; k++) if (template.Strokes[k].Required) requiredCount++;

            int ti = 0, ii = 0;
            while (ti < templateCount || ii < inputCount) {
                Choice choice = choices[ti, ii];
                if (choice == Choice.Match) {
                    SignatureTemplateStroke stroke = template.Strokes[ti];
                    LocalDiagnostics local = diagnostics[ti, ii];
                    results.Add(new SignatureStrokeMatchResult(ii, stroke.Id, local.Fit, local.Coverage,
                        local.Direction, local.Similarity));
                    if (stroke.Required) matchedRequired++;
                    if (stroke.Importance > 0f) {
                        geometryWeight += stroke.Importance;
                        fitSum += local.Fit * stroke.Importance;
                        coverageSum += local.Coverage * stroke.Importance;
                        float dw = stroke.Importance * stroke.DirectionImportance;
                        directionWeight += dw;
                        directionSum += local.Direction * dw;
                    }
                    ti++; ii++;
                } else if (choice == Choice.SkipOptional) {
                    ti++;
                } else if (choice == Choice.SkipInput) {
                    extras++; ii++;
                } else if (choice == Choice.SkipRequired) {
                    SignatureTemplateStroke stroke = template.Strokes[ti];
                    results.Add(new SignatureStrokeMatchResult(-1, stroke.Id, 0f, 0f, 0f, 0f));
                    if (stroke.Importance > 0f) absentPositiveRequired = true;
                    ti++;
                } else {
                    throw new InvalidOperationException("Stroke alignment reconstruction failed.");
                }
            }

            float fitComponent, coverageComponent, directionComponent;
            if (geometryWeight > 0f) {
                fitComponent = fitSum / geometryWeight;
                coverageComponent = coverageSum / geometryWeight;
                directionComponent = directionWeight > 0f ? directionSum / directionWeight : 1f;
            } else if (absentPositiveRequired) {
                fitComponent = coverageComponent = directionComponent = 0f;
            } else {
                fitComponent = coverageComponent = directionComponent = 1f;
            }
            float structure = requiredCount > 0
                ? matchedRequired / (float)(requiredCount + extras)
                : 1f / (1f + extras);
            fitComponent = Mathf.Clamp01(fitComponent);
            coverageComponent = Mathf.Clamp01(coverageComponent);
            directionComponent = Mathf.Clamp01(directionComponent);
            structure = Mathf.Clamp01(structure);
            SignatureScoreWeights weights = rules.ScoreWeights;
            float total = Mathf.Clamp01(fitComponent * weights.CorridorFit + coverageComponent * weights.Coverage +
                directionComponent * weights.Direction + structure * weights.StrokeStructure);
            var breakdown = new SignatureScoreBreakdown(fitComponent, coverageComponent, directionComponent,
                structure, total);
            return new SignatureVariantMatchResult(template.Id, total, breakdown, results);
        }

        private static LocalDiagnostics Diagnose(SignatureTemplateStroke template, ProcessedSignatureStroke input,
            ResolvedSignatureRules rules) {
            LocalDiagnostics forward = DiagnoseOrientation(template, input, rules, false);
            if (!template.AllowReverseDirection) return forward;
            LocalDiagnostics reverse = DiagnoseOrientation(template, input, rules, true);
            return reverse.Similarity > forward.Similarity + TieEpsilon ? reverse : forward;
        }

        private static LocalDiagnostics DiagnoseOrientation(SignatureTemplateStroke template,
            ProcessedSignatureStroke input, ResolvedSignatureRules rules, bool reverse) {
            int count = Math.Min(template.Nodes.Count, input.Points.Count);
            float weightedFit = 0f, insideWeight = 0f, totalImportance = 0f;
            float directionSum = 0f, directionImportance = 0f;
            for (int i = 0; i < count; i++) {
                SignatureCorridorNode node = template.Nodes[i];
                ProcessedSignaturePoint point = input.Points[reverse ? input.Points.Count - 1 - i : i];
                float importance = node.Importance;
                float radius = node.Radius * rules.CorridorWidthMultiplier;
                float error = Vector2.Distance(node.Position, point.Position) / radius;
                float fit = Mathf.Exp(-HalfLog * error * error);
                weightedFit += fit * importance;
                if (error <= 1f) insideWeight += importance;
                totalImportance += importance;
                if (node.Direction.sqrMagnitude > DirectionEpsilonSquared && point.Direction.sqrMagnitude > DirectionEpsilonSquared) {
                    float dot = Mathf.Clamp(Vector2.Dot(node.Direction.normalized, point.Direction.normalized), -1f, 1f);
                    float direction = template.AllowReverseDirection ? Mathf.Abs(dot) : (1f + dot) * 0.5f;
                    directionSum += direction * importance;
                    directionImportance += importance;
                }
            }
            float fitScore = totalImportance > 0f ? weightedFit / totalImportance : 0f;
            float rawCoverage = totalImportance > 0f ? insideWeight / totalImportance : 0f;
            float requiredCoverage = Mathf.Clamp01(template.MinimumCoverage * rules.CoverageRequirementMultiplier);
            float coverage = requiredCoverage <= 0f ? 1f : Mathf.Clamp01(rawCoverage / requiredCoverage);
            float directionScore = template.DirectionImportance == 0f || directionImportance <= 0f
                ? 1f : directionSum / directionImportance;
            float orientation = (fitScore + coverage + directionScore * template.DirectionImportance) /
                                (2f + template.DirectionImportance);
            return new LocalDiagnostics(Mathf.Clamp01(fitScore), Mathf.Clamp01(coverage),
                Mathf.Clamp01(directionScore), Mathf.Clamp01(orientation));
        }

        private static void Select(float candidate, Choice choice, int priority, ref float best,
            ref Choice bestChoice, ref int bestPriority) {
            if (candidate < best - TieEpsilon || Mathf.Abs(candidate - best) <= TieEpsilon && priority < bestPriority) {
                best = candidate; bestChoice = choice; bestPriority = priority;
            }
        }

        private readonly struct LocalDiagnostics {
            public readonly float Fit, Coverage, Direction, Similarity;
            public LocalDiagnostics(float fit, float coverage, float direction, float similarity) {
                Fit = fit; Coverage = coverage; Direction = direction; Similarity = similarity;
            }
        }
        private enum Choice { None, Match, SkipOptional, SkipInput, SkipRequired }
        public void Dispose() { }
    }
}
