using System;

namespace Exceptions {
    public sealed class AssetLoadException : Exception
    {
        public AssetLoadException(
            string message,
            Exception innerException)
            : base(message, innerException)
        {
        }
    }
}