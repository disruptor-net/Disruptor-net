namespace Disruptor
{
    public interface IEventReleaseAware
    {
        void SetEventReleaser(IEventReleaser eventReleaser);
    }
}