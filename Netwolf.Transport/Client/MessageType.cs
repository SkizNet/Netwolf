using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.Client
{
    /// <summary>
    /// Type of a message to send
    /// </summary>
    public enum MessageType
    {
        /// <summary>
        /// Regular message (corresponds to PRIVMSG or CPRIVMSG)
        /// </summary>
        Message,
        /// <summary>
        /// Notice (corresponds to NOTICE or CNOTICE)
        /// </summary>
        Notice
    }
}
