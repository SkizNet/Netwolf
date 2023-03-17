using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.ChannelModes
{
    public enum ParameterType
    {
        /// <summary>
        /// No parameters are required for this mode
        /// </summary>
        None,
        /// <summary>
        /// A parameter is required when setting this mode,
        /// but no parameter is required when unsetting it
        /// </summary>
        SetOnly,
        /// <summary>
        /// A parameter is required when both setting and
        /// unsetting this mode
        /// </summary>
        SetAndUnset,
        /// <summary>
        /// This mode acts as a list and can be set and unset
        /// multiple times
        /// </summary>
        List
    }
}
