using System.Threading.Tasks;
using Disruptor.Dsl;

namespace Disruptor.PerfTests.Sequenced
{
    /// <summary>
    /// UniCast a series of items between 1 publisher and 1 event processor.
    /// 
    /// <code>
    /// +----+    +-----+
    /// | P1 |--->| EP1 |
    /// +----+    +-----+
    /// Disruptor:
    /// ==========
    ///              track to prevent wrap
    ///              +------------------+
    ///              |                  |
    ///              |                  v
    /// +----+    +====+    +====+   +-----+
    /// | P1 |---›| RB |‹---| SB |   | EP1 |
    /// +----+    +====+    +====+   +-----+
    ///      claim      get    ^        |
    ///                        |        |
    ///                        +--------+
    ///                          waitFor
    /// P1  - Publisher 1
    /// RB  - RingBuffer
    /// SB  - SequenceBarrier
    /// EP1 - EventProcessor 1    
    /// </code>
    /// </summary>
    public class OneToOneSequencedLongArrayThroughputTest
    {
        private const int _bufferSize = 1024 * 1;
        private const long _iterations = 1000L * 1000L * 1L;
        private const int _arraySize = 2 * 1024;
        private static readonly IExecutor _executor = new BasicExecutor(TaskScheduler.Current);
    }
}