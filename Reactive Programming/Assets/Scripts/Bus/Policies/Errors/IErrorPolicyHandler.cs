using System;

namespace Bus.Policies.Errors {
    public interface IErrorPolicyHandler {
        ErrorPolicyResult ExecuteHandle(Action executable);
    }
}