using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Commands;

/// <summary>
/// A batch of responses to a single recipient
/// </summary>
public class BatchResponse : ICommandResponse
{
    private readonly User _user;
    private readonly List<ICommandResponse> _lines = new();

    public bool CloseConnection => _lines.Any(l => l.CloseConnection);

    public BatchResponse(User user)
    {
        _user = user ?? throw new ArgumentNullException(nameof(user));
    }

    public void Add(ICommandResponse response)
    {
        _lines.Add(response);
    }

    public void AddNumeric(Numeric numeric, params string[] args)
    {
        Add(new NumericResponse(_user, numeric, args));
    }
}
