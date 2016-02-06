using System;

namespace Server
{
    [Serializable]
    class ClientProcessorException : Exception
    {
        public ClientProcessorException(string message) : base(message) { }
    }
}
