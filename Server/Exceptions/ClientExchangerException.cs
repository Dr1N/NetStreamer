using System;

namespace Server
{
    [Serializable]
    class ClientExchangerException : Exception
    {
        public ClientExchangerException(string message) : base(message) { }
    }
}