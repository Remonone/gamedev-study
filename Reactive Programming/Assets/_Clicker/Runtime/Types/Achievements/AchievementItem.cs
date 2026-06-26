using R3;
using Services.Statistics;

namespace Types.Achievements {
    public abstract class AchievementItem : IAchievement {

    protected readonly IStatisticsReader Statistics;
    protected readonly CompositeDisposable Disposable = new CompositeDisposable();
    
    private readonly ReactiveProperty<bool> _isCompleted = new();
    private readonly ReactiveProperty<float> _progress = new(0f);
    private readonly ReactiveProperty<string> _progressText = new(string.Empty);

    private bool _isLoadedAsCompleted;

    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public ReadOnlyReactiveProperty<bool> IsCompleted => _isCompleted;
    public bool IsLoadedAsCompleted => _isLoadedAsCompleted; 

    public Observable<float> Progress => _progress;
    public Observable<string> ProgressText => _progressText;

    public AchievementItem(IStatisticsReader reader) {
        Statistics = reader;
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
        Disposable.Dispose();
    }

    protected abstract void StartTracking();

    public virtual void Dispose() {
        Disposable.Dispose();
        _isCompleted.Dispose();
        _progress.Dispose();
        _progressText.Dispose();
    }
    
    }
}