using System;
using System.Runtime.Serialization;

namespace VNLib.Plugins.Sessions.Cache.Client
{
    public class SessionUpdateFailedException : SessionStatusException
    {
        public SessionUpdateFailedException()
        {}
        public SessionUpdateFailedException(string message) : base(message)
        {}
        public SessionUpdateFailedException(string message, Exception innerException) : base(message, innerException)
        {}
        protected SessionUpdateFailedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {}
    }
}
