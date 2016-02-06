using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    class ServerExchangerException : Exception
    {
        public ServerExchangerException(string message) : base(message) { }
    }
}
