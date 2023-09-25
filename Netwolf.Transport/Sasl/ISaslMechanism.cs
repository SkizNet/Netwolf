using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication.ExtendedProtection;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.Sasl;

public interface ISaslMechanism
{
    string Name { get; }

    /// <summary>
    /// For SASL mechanisms that use TLS channel binding, set this to true.
    /// </summary>
    bool SupportsChannelBinding => false;

    bool Authenticate(ReadOnlySpan<byte> challenge, out ReadOnlySpan<byte> response);

    /// <summary>
    /// For SASL mechanisms that use TLS channel binding, override this to capture the binding data.
    /// </summary>
    /// <param name="uniqueData"></param>
    /// <param name="endpointData"></param>
    /// <returns>true if we accept the data, false if this mechanism cannot work with the passed-in data</returns>
    bool SetChannelBindingData(ChannelBinding? uniqueData, ChannelBinding? endpointData)
    {
        throw new NotImplementedException();
    }
}
