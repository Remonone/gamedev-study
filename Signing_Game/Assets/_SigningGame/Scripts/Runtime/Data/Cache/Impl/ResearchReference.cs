using System;
using UnityEngine;
using Utils.Attributes;

namespace Data.Cache {
    [Serializable, CacheEntryGroup("Research")]
    public struct ResearchEntries {
        [ModifiableParameter("PointsPerAcceptedDocument", Minimum = 0d)]
        public double PointsPerAcceptedDocument;

        [ModifiableParameter("DoublePointQualityThreshold", Minimum = 0d, Maximum = 1d)]
        public float DoublePointQualityThreshold;

        [ModifiableParameter("DoublePointChance", Minimum = 0d, Maximum = 1d)]
        public float DoublePointChance;

        [ModifiableParameter("BaseRequiredPoints", Minimum = 1d)]
        public double BaseRequiredPoints;

        [ModifiableParameter("AdditionalRequiredPointsPerResolvedCycle", Minimum = 0d)]
        public double AdditionalRequiredPointsPerResolvedCycle;

        [ModifiableParameter("OfferCount", Minimum = 1d, Maximum = 64d)]
        public int OfferCount;
    }

    [CreateAssetMenu(menuName = "References/Research Reference", fileName = "Research Reference")]
    public sealed class ResearchReference : BaseEntries<ResearchEntries> { }
}
