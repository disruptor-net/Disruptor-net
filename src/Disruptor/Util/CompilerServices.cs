// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices;

#if !NET9_0_OR_GREATER
internal sealed class OverloadResolutionPriorityAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OverloadResolutionPriorityAttribute"/> class.
    /// </summary>
    /// <param name="priority">The priority of the attributed member. Higher numbers are prioritized, lower numbers are deprioritized. 0 is the default if no attribute is present.</param>
    public OverloadResolutionPriorityAttribute(int priority)
    {
        Priority = priority;
    }

    /// <summary>
    /// The priority of the member.
    /// </summary>
    public int Priority { get; }
}
#endif
