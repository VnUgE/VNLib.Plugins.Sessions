using System;
using System.Runtime.Serialization;

using VNLib.Net.Messaging.FBM;

namespace VNLib.Plugins.Sessions.Cache.Client.Exceptions
{
    /// <summary>
    /// Raised when a request message is too large to send to 
    /// the server and the server may close the connection.
    /// </summary>
    public class MessageTooLargeException : FBMException
    {
        ///<inheritdoc/>
        public MessageTooLargeException()
        { }
        ///<inheritdoc/>
        public MessageTooLargeException(string message) : base(message)
        { }
        ///<inheritdoc/>
        public MessageTooLargeException(string message, Exception innerException) : base(message, innerException)
        { }
        ///<inheritdoc/>
        protected MessageTooLargeException(SerializationInfo info, StreamingContext context) : base(info, context)
        { }
    }
}
