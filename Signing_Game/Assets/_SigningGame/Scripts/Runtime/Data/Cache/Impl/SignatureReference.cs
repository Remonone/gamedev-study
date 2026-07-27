using System;
using UnityEngine;
using Utils.Attributes;

namespace Data.Cache {
    
    [Serializable, CacheEntryGroup("Signature")]
    public struct SignatureEntries {
        public string SignatureId;
        
        public SignatureEntries(string signatureId) {
            SignatureId = signatureId;
        }
    }
    
    [CreateAssetMenu(menuName = "References/Signature Reference")]
    public class SignatureReference : BaseEntries<SignatureEntries> { }
}