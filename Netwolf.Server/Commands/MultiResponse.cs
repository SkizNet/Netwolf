using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Commands;

/// <summary>
/// Reflects multiple lines of responses, potentially to different recipients
/// </summary>
public class MultiResponse : ICommandResponse, IEnumerable<ICommandResponse>
{
    private readonly List<ICommandResponse> _lines = new();

    public bool CloseConnection => _lines.Any(l => l.CloseConnection);

    public void AddNumeric(User user, Numeric numeric, params string[] args)
    {
        _lines.Add(new NumericResponse(user, numeric, args));
    }

    public void Add(ICommandResponse item)
    {
        _lines.Add(item);
    }

    public void AddRange(IEnumerable<ICommandResponse> items)
    {
        _lines.AddRange(items);
    }

    public void Send()
    {
        foreach (var line in _lines)
        {
            line.Send();
        }
    }

    public IEnumerator<ICommandResponse> GetEnumerator()
    {
        return ((IEnumerable<ICommandResponse>)_lines).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)_lines).GetEnumerator();
    }
}
