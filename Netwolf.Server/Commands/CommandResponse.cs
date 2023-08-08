using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Commands;

public class CommandResponse : ICommandResponse
{
    protected User User { get; init; }

    protected string? Source { get; init; }

    protected string Command { get; init; }

    protected List<string> Args { get; init; }

    public virtual bool CloseConnection => false;

    public CommandResponse(User user, string? source, string command, params string[] args)
    {
        User = user;
        Source = source;
        Command = command;
        Args = new List<string>(args);
    }

    public void Send()
    {
        User.Send(Source, Command, Args);
    }
}
