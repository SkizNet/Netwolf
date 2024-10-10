using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.IRC;

public record CommandCreationOptions(int LineLen = 512, int ClientTagLen = 4096, int ServerTagLen = 8191);
