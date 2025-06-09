using Netwolf.PluginFramework.Commands;
using Netwolf.Transport.IRC;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Test.PluginFramework;

public class Commands
{
    public class TestA : ICommandHandler<int>
    {
        public string Command => "TESTA";

        public Task<int> ExecuteAsync(ICommand command, IContext sender, CancellationToken cancellationToken) => Task.FromResult(1);
    }

    public class TestB : ICommandHandler<int>
    {
        public string Command => "TESTB";

        public string? Privilege => "command:testb";

        public Task<int> ExecuteAsync(ICommand command, IContext sender, CancellationToken cancellationToken) => Task.FromResult(2);
    }

    public class TestC : ICommandHandler<short>
    {
        public string Command => "TESTC";

        public Task<short> ExecuteAsync(ICommand command, IContext sender, CancellationToken cancellationToken) => Task.FromResult((short)3);
    }

    public class TestD : ICommandHandler<int>
    {
        public string Command => "TESTD";

        public Task<int> ExecuteAsync(ICommand command, IContext sender, CancellationToken cancellationToken) => throw new TestException();
    }

    public class TestE : ICommandHandler<int>
    {
        public string Command => "TESTE";

        public async Task<int> ExecuteAsync(ICommand command, IContext sender, CancellationToken cancellationToken)
        {
            // This command is intended to be cancelled as part of test execution
            await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
            return 5;
        }
    }

    internal class TestF : ICommandHandler<int>
    {
        public string Command => "TESTF";

        public Task<int> ExecuteAsync(ICommand command, IContext sender, CancellationToken cancellationToken) => Task.FromResult(6);
    }

    public class TestG<T> : ICommandHandler<int>
    {
        public string Command => "TESTG";

        public Task<int> ExecuteAsync(ICommand command, IContext sender, CancellationToken cancellationToken) => Task.FromResult(7);
    }

    public class TestH : ICommandHandler<int>
    {
        public string Command => "testH";

        public Task<int> ExecuteAsync(ICommand command, IContext sender, CancellationToken cancellationToken) => Task.FromResult(8);
    }
}
