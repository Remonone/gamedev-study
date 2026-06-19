using System;
using R3;

namespace Types.Enums.Achievements {
    public interface IAchievement : IDisposable {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        
        ReadOnlyReactiveProperty<bool> IsCompleted { get; }
        bool IsLoadedAsCompleted { get; }
        
        Observable<float> Progress { get; }
        Observable<string> ProgressText { get; }
        
        void Start();
        void RestoreCompleted();
    }
}