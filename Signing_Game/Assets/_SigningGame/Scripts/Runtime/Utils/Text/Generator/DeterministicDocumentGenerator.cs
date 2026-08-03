using System;
using System.Collections.Generic;
using System.Text;
using Data.Documents;
using UnityEngine;

namespace Utils.Text.Generator {
    public static class DeterministicDocumentGenerator {
        private const ulong _StructureStream = 100;
        private const ulong _TitleStream = 200;
        private const ulong _HeadingStream = 1_000;
        private const ulong _ParagraphStream = 10_000;
        private const ulong _FootnoteStream = 20_000;

        public static GeneratedDocument Generate(ulong seed, DocumentTextSettings settings) {
            Validate(settings);

            var structureRandom = new StableRandom(SeedUtility.Derive(seed, _StructureStream));

            int bodyFontSize = NextTriangular(ref structureRandom, settings.BodyFontSize);

            int headingFontSize = NextTriangular(ref structureRandom, settings.HeadingFontSize);

            int titleFontSize = NextTriangular(ref structureRandom, settings.TitleFontSize);

            int footnoteFontSize = NextTriangular(ref structureRandom, settings.FootnoteFontSize);

            int paragraphCount = NextTriangular(ref structureRandom, settings.ParagraphCount);

            var blocks = new List<GeneratedDocumentBlock>();

            if (structureRandom.Chance(settings.TitleProbability)) {
                var titleRandom = new StableRandom(SeedUtility.Derive(seed, _TitleStream));

                string title = GenerateLine(ref titleRandom, settings, settings.TitleWords);

                blocks.Add(new GeneratedDocumentBlock(DocumentBlockType.Title, title, titleFontSize));
            }

            int paragraphsSinceHeading = 0;

            for (int paragraphIndex = 0; paragraphIndex < paragraphCount; paragraphIndex++) {
                bool canCreateHeading = paragraphIndex > 0 &&
                                        paragraphsSinceHeading >= settings.MinimumParagraphsBetweenHeadings;

                if (canCreateHeading && structureRandom.Chance(settings.HeadingProbability)) {
                    var headingRandom = new StableRandom(SeedUtility.Derive(seed, _HeadingStream + (ulong)paragraphIndex));

                    string heading = GenerateLine(ref headingRandom, settings, settings.HeadingWords);

                    blocks.Add(new GeneratedDocumentBlock(DocumentBlockType.Heading, heading, headingFontSize));

                    paragraphsSinceHeading = 0;
                }

                var paragraphRandom = new StableRandom(SeedUtility.Derive(seed, _ParagraphStream + (ulong)paragraphIndex));

                string paragraph = GenerateParagraph(ref paragraphRandom, settings);

                blocks.Add(new GeneratedDocumentBlock(DocumentBlockType.Body, paragraph, bodyFontSize));

                paragraphsSinceHeading++;
            }

            if (structureRandom.Chance(settings.FootnoteProbability)) {
                var footnoteRandom = new StableRandom(SeedUtility.Derive(seed, _FootnoteStream));

                string footnote = GenerateSentence(ref footnoteRandom, settings);

                blocks.Add(new GeneratedDocumentBlock(DocumentBlockType.Footnote, footnote, footnoteFontSize));
            }

            return new GeneratedDocument(blocks);
        }

        private static string GenerateParagraph(ref StableRandom random, DocumentTextSettings settings) {
            int sentenceCount = NextTriangular(ref random, settings.SentencesPerParagraph);

            var builder = new StringBuilder();

            for (int i = 0; i < sentenceCount; i++) {
                if (i > 0) builder.Append(' ');

                builder.Append(GenerateSentence(ref random, settings));
            }

            return builder.ToString();
        }

        private static string GenerateSentence(ref StableRandom random, DocumentTextSettings settings) {
            int wordCount = NextTriangular(ref random, settings.WordsPerSentence);

            var builder = new StringBuilder();
            int wordsSincePunctuation = 0;

            for (int wordIndex = 0; wordIndex < wordCount; wordIndex++) {
                builder.Append(GenerateWord(ref random, settings));

                bool isLastWord = wordIndex == wordCount - 1;

                if (isLastWord)
                    continue;

                wordsSincePunctuation++;

                if (wordsSincePunctuation >= 2) {
                    if (random.Chance(settings.SemicolonProbability)) {
                        builder.Append(';');
                        wordsSincePunctuation = 0;
                    }else if (random.Chance(settings.CommaProbability)) {
                        builder.Append(',');
                        wordsSincePunctuation = 0;
                    }
                }

                builder.Append(' ');
            }

            builder.Append(random.Chance(0.03f) ? '?' : '.');

            return builder.ToString();
        }

        private static string GenerateLine(ref StableRandom random, DocumentTextSettings settings, Vector2Int wordCountRange) {
            int wordCount = NextTriangular(ref random, wordCountRange);

            var builder = new StringBuilder();

            for (int i = 0; i < wordCount; i++) {
                if (i > 0)
                    builder.Append(' ');

                builder.Append(GenerateWord(ref random, settings));
            }

            return builder.ToString();
        }

        private static string GenerateWord(ref StableRandom random, DocumentTextSettings settings) {
            int length = ChooseWeightedIndex(ref random, settings.WordLengthWeights);

            var builder = new StringBuilder();
            int previousGlyphIndex = -1;
            int repeatCount = 0;

            for (int i = 0; i < length; i++) {
                int glyphIndex = random.NextInt(0, settings.Glyphs.Length);

                if (glyphIndex == previousGlyphIndex) {
                    repeatCount++;

                    if (repeatCount >= 2 && settings.Glyphs.Length > 1) {
                        glyphIndex = (glyphIndex + 1 + random.NextInt(0, settings.Glyphs.Length - 1)) % settings.Glyphs.Length;
                        repeatCount = 0;
                    }
                }
                else {
                    repeatCount = 0;
                }

                builder.Append(settings.Glyphs[glyphIndex]);
                previousGlyphIndex = glyphIndex;
            }

            return builder.ToString();
        }

        private static int ChooseWeightedIndex(ref StableRandom random, int[] weights) {
            int totalWeight = 0;

            for (int i = 1; i < weights.Length; i++)
                totalWeight += Mathf.Max(0, weights[i]);

            if (totalWeight <= 0)
                throw new InvalidOperationException(
                    "Word length weights must contain at least one positive value.");

            int roll = random.NextInt(0, totalWeight);

            for (int i = 1; i < weights.Length; i++) {
                int weight = Mathf.Max(0, weights[i]);

                if (roll < weight)
                    return i;

                roll -= weight;
            }

            return weights.Length - 1;
        }

        private static int NextTriangular(ref StableRandom random, Vector2Int range) {
            int min = Mathf.Min(range.x, range.y);
            int max = Mathf.Max(range.x, range.y);

            if (min == max)
                return min;

            int first = random.NextInt(min, max + 1);
            int second = random.NextInt(min, max + 1);

            return (first + second) / 2;
        }

        private static void Validate(DocumentTextSettings settings) {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            if (settings.Glyphs == null || settings.Glyphs.Length == 0) {
                throw new InvalidOperationException(
                    "At least one glyph must be configured.");
            }

            foreach (string glyph in settings.Glyphs) {
                if (string.IsNullOrEmpty(glyph)) {
                    throw new InvalidOperationException(
                        "Glyph collection contains an empty value.");
                }
            }

            if (settings.WordLengthWeights == null || settings.WordLengthWeights.Length < 2) {
                throw new InvalidOperationException(
                    "Word length weights are not configured.");
            }
        }
    }
}