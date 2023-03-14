namespace Netwolf.Server.ChannelModes
{
    /// <summary>
    /// Denotes a channel mode: a Singleton class that encapsulates
    /// information and behavior for a channel setting
    /// </summary>
    internal abstract class ChannelMode<T> : IChannelMode
        where T : ChannelMode<T>, new()
    {
        public IChannelMode Singleton { get; private init; } = new T();

        public abstract char ModeChar { get; }

        public virtual ParameterType ParameterType => ParameterType.None;

        public virtual string? ViewModePrivilege => null;

        public virtual string? ViewParameterPrivilege => null;

        public abstract string ModifyPrivilege { get; }

        public bool TrySet(User user, Channel channel, string? parameter)
        {
            throw new NotImplementedException();
        }

        public bool TryUnset(User user, Channel channel, string? parameter)
        {
            throw new NotImplementedException();
        }

        protected virtual object? ValidateAndTransform(string parameter) => null;
    }
}
