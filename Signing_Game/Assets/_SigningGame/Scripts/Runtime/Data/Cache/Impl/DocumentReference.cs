using System;
using UnityEngine;
using Utils.Attributes;

namespace Data.Cache {
    [Serializable, CacheEntryGroup("Document")]
    public struct DocumentEntries {
        [ModifiableParameter("DocumentQualityLevel", Minimum = 0d, Maximum = 9d)]
        public int DocumentQualityLevel;
        
        public int SelectedDocumentQualityLevel;
    }
    
    [CreateAssetMenu(menuName = "References/Document Reference")]
    public class DocumentReference : BaseEntries<DocumentEntries> { }
}
