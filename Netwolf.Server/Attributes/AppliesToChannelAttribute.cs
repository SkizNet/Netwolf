namespace Netwolf.Server.Attributes;

/// <summary>
/// Used on channel modes to indicate that the mode can be applied
/// to a particular type of channel. Specify multiple times to allow
/// a mode to be applied to various channel types.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public class AppliesToChannelAttribute<T> : Attribute, IAppliesTo
    where T : Channel
{
    public Type AllowedType => typeof(T);

    public bool CanApply(object obj)
    {
        return obj is T;
    }
}
