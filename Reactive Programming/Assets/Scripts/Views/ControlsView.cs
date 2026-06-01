using R3;
using UnityEngine;
using UnityEngine.UIElements;
using Views.Models;

namespace Views {
     public class ControlsView : MonoBehaviour {
        [SerializeField] private Controls _controls;
        [SerializeField] private UIDocument _controlsDocument;

        private Label documentsLabel;
        private Label casesLabel;
        private Label firesLabel;
        private Label protectionsLabel;
        private Label curesLabel;


        private void Awake() {
            var root = _controlsDocument.rootVisualElement;

            documentsLabel = root.Q<Label>("DocumentsCount");
            casesLabel = root.Q<Label>("CasesCount");
            firesLabel = root.Q<Label>("FiresCount");
            protectionsLabel = root.Q<Label>("ProtectionsCount");
            curesLabel = root.Q<Label>("CuresCount"); 
        }

        private void Start() {
            _controls.DocumentsCount.Subscribe(count => documentsLabel.text = $"{count}").AddTo(this);
            _controls.CasesCount.Subscribe(count => casesLabel.text = $"{count}").AddTo(this);
            _controls.FiresCount.Subscribe(count => firesLabel.text = $"{count}").AddTo(this);
            _controls.ProtectionsCount.Subscribe(count => protectionsLabel.text = $"{count}").AddTo(this);
            _controls.CuresCount.Subscribe(count => curesLabel.text = $"{count}").AddTo(this);
        }
     }
}