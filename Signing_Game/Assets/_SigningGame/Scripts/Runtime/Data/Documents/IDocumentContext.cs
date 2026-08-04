using System;
using Contracts;

namespace Data.Documents {
    public interface IDocumentContext : IDisposable {
        void GetBehavior<T>(out T behavior) where T : notnull;
        IDocumentSession TakeSession();
    }
}
