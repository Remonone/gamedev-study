using System;
using System.Collections.Generic;

namespace Save {
    [Serializable]
    public class SaveData {
        public int Version = 1;
        public long SavedAtUnix;
        public Dictionary<string, string> Payload;

        public SaveData() {
            Payload = new();
        }
    }
}