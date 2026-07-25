using System;
using UnityEngine;

namespace Utils.Metadata {
    [Serializable]
    public sealed class CacheParameterReference {
        [SerializeField]
        private string _groupId;

        [SerializeField]
        private string _parameterId;

        public string GroupId => _groupId;
        public string ParameterId => _parameterId;
    }
}