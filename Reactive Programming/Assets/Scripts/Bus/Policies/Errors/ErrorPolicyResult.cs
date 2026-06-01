using System;

namespace Bus.Policies.Errors {
    public class ErrorPolicyResult {
        public Exception Exception;
        public bool Handled;
        public string Message;
        public string StackTrace;
    }
}