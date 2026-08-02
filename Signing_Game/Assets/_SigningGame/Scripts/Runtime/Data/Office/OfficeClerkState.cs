using System;

namespace Data.Office {
    public sealed class OfficeClerkState {
        public int Id { get; }
        public float Progress { get; internal set; }

        internal OfficeClerkState(int id, float progress = 0f) {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            Id = id;
            Progress = progress;
        }
    }
}
