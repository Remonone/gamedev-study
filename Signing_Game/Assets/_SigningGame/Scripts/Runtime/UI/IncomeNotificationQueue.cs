using System.Collections.Generic;
using Utils;

namespace UI {
    internal sealed class IncomeNotificationQueue {
        internal const int Capacity = 100;
        internal const double SpawnDelaySeconds = 0.1d;

        private readonly Queue<Value> _items = new();
        private double _nextEligibleTime;

        public int Count => _items.Count;

        public void Enqueue(Value income, double now) {
            if (income.IsZero) return;

            bool wasEmpty = _items.Count == 0;
            if (_items.Count == Capacity) _items.Dequeue();
            _items.Enqueue(income);
            if (wasEmpty) _nextEligibleTime = now + SpawnDelaySeconds;
        }

        public bool TryDequeueDue(double now, out Value income) {
            if (_items.Count == 0 || now < _nextEligibleTime) {
                income = Value.Zero;
                return false;
            }

            income = _items.Dequeue();
            _nextEligibleTime = now + SpawnDelaySeconds;
            return true;
        }

        public void Clear() {
            _items.Clear();
            _nextEligibleTime = 0d;
        }
    }
}
