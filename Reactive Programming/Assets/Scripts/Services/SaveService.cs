using System;
using R3;
using Save;

namespace Services {
    public class SaveService : IService {

        private readonly Subject<Unit> _onSaveStarted = new();
        
        private SaveManager _saveManager;
        
        public SaveService(SaveManager saveManager) {
            _saveManager = saveManager;
            Observable.Interval(TimeSpan.FromMinutes(1))
                .Subscribe(_ => {
                    saveManager.Save();
                    _onSaveStarted.OnNext(Unit.Default);
                });
        }

        public void ForceSave() {
            _saveManager.Save();
            _onSaveStarted.OnNext(Unit.Default);
        }
    }
}