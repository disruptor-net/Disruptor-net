namespace Disruptor
{
    /// <summary>
    /// Implement this interface in your <see cref="IWorkHandler{T}"/> to obtain the <see cref="IEventReleaser"/>.
    /// </summary>
    public interface IEventReleaseAware
    {
        void SetEventReleaser(IEventReleaser eventReleaser);
    }
}
