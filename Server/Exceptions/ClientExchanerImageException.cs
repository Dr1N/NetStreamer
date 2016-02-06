using System;

namespace Server
{
    [Serializable]
    class ClientExchangerImageException : ClientExchangerException
    {
        public ClientExchangerImageException(string message) : base(message) { }
    }
}