namespace Netwolf.Server.ChannelModes;

/// <summary>
/// Base class for channel modes that implements a lot of boilerplate for
/// modes which lack parameters.
/// </summary>
public abstract class ParameterlessChannelMode<T> : IChannelMode
    where T : ParameterlessChannelMode<T>, new()
{
    public abstract char ModeChar { get; }

    public ParameterType ParameterType => ParameterType.None;

    public string? Parameter => null;

    public virtual string? ViewModePrivilege => null;

    public string? ViewParameterPrivilege => null;

    public virtual string ModifyPrivilege => $"chan:mode:{typeof(T).Name.ToLowerInvariant()}";

    public bool Set(User user, Channel channel, string? parameter)
    {
        throw new NotImplementedException();
    }

    public bool Unset(User user, Channel channel, string? parameter)
    {
        throw new NotImplementedException();
    }

    public sealed override int GetHashCode()
    {
        return ModeChar.GetHashCode();
    }

    public sealed override bool Equals(object? obj)
    {
        return obj switch
        {
            T mode => mode.ModeChar == ModeChar,
            _ => false
        };
    }
}
