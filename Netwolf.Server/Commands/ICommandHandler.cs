﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Commands
{
    public interface ICommandHandler
    {
        string Command { get; }
    }
}
