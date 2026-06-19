using System;
using R3;
using Services.Achievements;
using Services.Statistics;

namespace Types.Enums.Achievements {
    public abstract class AchievementItem : IAchievement {

    protected readonly IStatisticsReader _statistics;
    protected readonly CompositeDisposable _disposable = new CompositeDisposable();
    
    private readonly ReactiveProperty<bool> _isCompleted = new();
    private readonly ReactiveProperty<float> _progress = new(0f);
    private readonly ReactiveProperty<string> _progressText = new(string.Empty);
    
    protected readonly AchievementService _achievementService;

    private bool _isLoadedAsCompleted;

    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public ReadOnlyReactiveProperty<bool> IsCompleted => _isCompleted;
    public bool IsLoadedAsCompleted => _isLoadedAsCompleted; 

    public Observable<float> Progress => _progress;
    public Observable<string> ProgressText => _progressText;

    public AchievementItem(IStatisticsReader reader) {
        _statistics = reader;
    }

    public void Start() {
        if (_isCompleted.Value) return;
        StartTracking();
    }

    public void RestoreCompleted() {
        _isLoadedAsCompleted = true;
        _isCompleted.Value = true;
        _progress.Value = 1f;
        _progressText.Value = "Completed";
    }

    protected void ReportProgress(float progress, string text) {
        _progress.Value = progress;
        _progressText.Value = text;
    }

    protected void Complete() {
        if (_isCompleted.Value) return;
        _isCompleted.Value = true;
        _progress.Value = 1f;
        _progressText.Value = "Completed";
        _disposable.Dispose();
    }

    protected abstract void StartTracking();

    public virtual void Dispose() {
        _disposable.Dispose();
        _isCompleted.Dispose();
        _progress.Dispose();
        _progressText.Dispose();
    }
    
    }
}