using System;
using UnityEngine;
using Utils.Attributes;

namespace Data.Cache {
    [Serializable, CacheEntryGroup("Document")]
    public struct DocumentEntries {
        [ModifiableParameter("DocumentQualityLevel", Minimum = 0d, Maximum = 9d)]
        public int DocumentQualityLevel;

        [ModifiableParameter("DocumentQualityIncomeMultiplier", Minimum = 0d)]
        public float DocumentQualityIncomeMultiplier;
        
        public int SelectedDocumentQualityLevel;

        public int StampRequiredEveryNthOffer;
    }
    
    [CreateAssetMenu(menuName = "References/Document Reference")]
    public class DocumentReference : BaseEntries<DocumentEntries> { }
}
