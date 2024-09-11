﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Exceptions;

public class NoSuchChannelException : CommandException
{
    public NoSuchChannelException(string channel)
        : base(Numeric.ERR_NOSUCHCHANNEL, channel) { }
}