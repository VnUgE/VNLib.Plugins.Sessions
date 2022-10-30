using System;
using System.Runtime.Serialization;

using VNLib.Plugins.Essentials.Sessions;

namespace VNLib.Plugins.Sessions.Cache.Client
{
    public class SessionStatusException : SessionException
    {
        public SessionStatusException()
        {}
        public SessionStatusException(string message) : base(message)
        {}
        public SessionStatusException(string message, Exception innerException) : base(message, innerException)
        {}
        protected SessionStatusException(SerializationInfo info, StreamingContext context) : base(info, context)
        {}
    }
}
