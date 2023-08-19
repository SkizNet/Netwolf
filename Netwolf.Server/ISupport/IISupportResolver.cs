﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.ISupport;

public interface IISupportResolver
{
    public IReadOnlyDictionary<string, object?> Resolve(Network network, User user);
}
