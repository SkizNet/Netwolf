using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Test.PluginFramework;

// Test exception to throw when testing exception handling,
// as an unambiguous exception type not thrown anywhere else but inside of our testing codess
internal class TestException : Exception
{
}
