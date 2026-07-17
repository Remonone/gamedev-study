using Contracts;
using Data.Requests;
using Data.Results;
using Data.Rules;
using Services.Locator;
using UnityEngine;

namespace Services {
    public class SignatureEvaluator : IService, ISignatureEvaluator, IInitialize, IPostInitialize {
        
        private SignaturePreprocessor _signaturePreprocessor;
        private AddressablesService _addressablesService;
        
        private SignatureProcessingRules _processingRules;
        
        Awaitable IInitialize.InitializeAsync(ServiceLocator container) {
            container.Get(out _signaturePreprocessor);
            ServiceLocator.Application.Get(out _addressablesService);
            return null;
        }
        
        Awaitable IPostInitialize.PostInitializeAsync(ServiceLocator container) {
            // TODO: Fetch processing rules from addressables
            return null;
        }
        
        public SignatureEvaluationResult Evaluate(SignatureEvaluationRequest request) {
            var attempt = request.Attempt;
            var processed = _signaturePreprocessor.Process(attempt, _processingRules);
            // TODO: Fetch template
            // TODO: Fetch rules
            // TODO: Evaluate signature
            return null;
        }
        
        public void Dispose() {
            
        }
    }
}