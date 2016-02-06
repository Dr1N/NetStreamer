using System;

namespace Server
{
    [Serializable]
    class ScreenProcessorException : Exception
    {
        public ScreenProcessorException(string message) : base(message) { }
    }
}
