using Microsoft.Extensions.Options;

namespace Netwolf.Test;

internal class TestOptionsMonitor<TOptions> : IOptionsMonitor<TOptions>
{
    public required TOptions CurrentValue { get; set; }

    public TOptions Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
}
