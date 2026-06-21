using System;
using R3;
using Save;

namespace Services {
    public class SaveService : IService, IStartable {

        private readonly Subject<Unit> _onSaveStarted = new();
        
        private SaveManager _saveManager;
        
        public SaveService(SaveManager saveManager) {
            _saveManager = saveManager;
        }

        public void ForceSave() {
            _saveManager.Save();
            _onSaveStarted.OnNext(Unit.Default);
        }

        public void StartService() {
            Observable.Interval(TimeSpan.FromMinutes(1))
                .Subscribe(_ => {
                    _saveManager.Save();
                    _onSaveStarted.OnNext(Unit.Default);
                });
        }
    }
}