using System;

namespace Exceptions {
    public sealed class SignaturePresetConfigurationException : Exception {
        public SignaturePresetConfigurationException(string message) : base(message) { }
        public SignaturePresetConfigurationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
