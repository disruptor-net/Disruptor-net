using System;
using System.Threading;
using Disruptor.Dsl;

namespace Disruptor
{
    /// <summary>
    /// WorkerPool contains a pool of <see cref="WorkProcessor{T}"/> that will consume sequences so jobs can be farmed out across a pool of workers.
    /// Each of the <see cref="WorkProcessor{T}"/> manage and calls a <see cref="IWorkHandler{T}"/> to process the events.
    /// </summary>
    /// <typeparam name="T">event to be processed by a pool of workers</typeparam>
    public sealed class WorkerPool<T> where T : class 
    {
        private readonly RunningFlag _running = new RunningFlag();
        private readonly Sequence _workSequence = new Sequence(Sequence.InitialCursorValue);
        private readonly RingBuffer<T> _ringBuffer;
        private readonly WorkProcessor<T>[] _workProcessors;

        /// <summary>
        /// Create a worker pool to enable an array of <see cref="IWorkHandler{T}"/>s to consume published sequences.
        /// 
        /// This option requires a pre-configured <see cref="RingBuffer{T}"/> which must have <see cref="Sequencer.SetGatingSequences"/>
        /// called before the work pool is started.
        /// </summary>
        /// <param name="ringBuffer">ringBuffer of events to be consumed.</param>
        /// <param name="sequenceBarrier">sequenceBarrier on which the workers will depend.</param>
        /// <param name="exceptionHandler">exceptionHandler to callback when an error occurs which is not handled by the <see cref="IWorkHandler{T}"/>s.</param>
        /// <param name="workHandlers">workHandlers to distribute the work load across.</param>
        public WorkerPool(RingBuffer<T> ringBuffer,
                          ISequenceBarrier sequenceBarrier,
                          IExceptionHandler exceptionHandler,
                          params IWorkHandler<T>[] workHandlers)
        {
            _ringBuffer = ringBuffer;
            _workProcessors = new WorkProcessor<T>[workHandlers.Length];

            for (var i = 0; i < workHandlers.Length; i++)
            {
                _workProcessors[i] = new WorkProcessor<T>(ringBuffer,
                                                         sequenceBarrier,
                                                         workHandlers[i],
                                                         exceptionHandler,
                                                         _workSequence);
            }
        }

        /// <summary>
        /// Construct a work pool with an internal <see cref="RingBuffer{T}"/> for convenience.
        /// 
        /// This option does not require <see cref="Sequencer.SetGatingSequences"/> to be called before the work pool is started.
        /// </summary>
        /// <param name="eventFactory">eventFactory for filling the <see cref="RingBuffer{T}"/></param>
        /// <param name="claimStrategy">claimStrategy for the <see cref="RingBuffer{T}"/></param>
        /// <param name="waitStrategy">waitStrategy for the <see cref="RingBuffer{T}"/></param>
        /// <param name="exceptionHandler">exceptionHandler to callback when an error occurs which is not handled by the <see cref="IWorkHandler{T}"/>s.</param>
        /// <param name="workHandlers">workHandlers to distribute the work load across.</param>
        public WorkerPool(Func<T> eventFactory,
                          IClaimStrategy claimStrategy,
                          IWaitStrategy waitStrategy,
                          IExceptionHandler exceptionHandler,
                          params IWorkHandler<T>[] workHandlers)

        {
            _ringBuffer = new RingBuffer<T>(eventFactory, claimStrategy, waitStrategy); // TODO : Build with RingBuffer.CreateMultiProducer
            var barrier = _ringBuffer.NewBarrier();
            _workProcessors = new WorkProcessor<T>[workHandlers.Length];

            for (var i = 0; i < workHandlers.Length; i++)
            {
                _workProcessors[i] = new WorkProcessor<T>(_ringBuffer,
                                                         barrier,
                                                         workHandlers[i],
                                                         exceptionHandler,
                                                         _workSequence);
            }

            _ringBuffer.SetGatingSequences(WorkerSequences);
        }

        /// <summary>
        /// Get an array of <see cref="Sequence"/>s representing the progress of the workers.
        /// </summary>
        public Sequence[] WorkerSequences
        {
            get
            {
                var sequences = new Sequence[_workProcessors.Length + 1];
                for (var i = 0; i < _workProcessors.Length; i++)
                {
                    sequences[i] = _workProcessors[i].Sequence;
                }
                sequences[sequences.Length - 1] = _workSequence;

                return sequences;
            }
        }

        /// <summary>
        /// Start the worker pool processing events in sequence.
        /// </summary>
        /// <returns>the <see cref="RingBuffer{T}"/> used for the work queue.</returns>
        /// <exception cref="InvalidOperationException">if the pool has already been started and not halted yet</exception>
        public RingBuffer<T> Start(IExecutor executor)
        {
            _running.MarkAsRunning("WorkerPool has already been started and cannot be restarted until halted");

            var cursor = _ringBuffer.Cursor;
            _workSequence.Value = cursor;

            for (var i = 0; i < _workProcessors.Length; i++)
            {
                var workProcessor = _workProcessors[i];
                workProcessor.Sequence.Value = cursor;

                executor.Execute(workProcessor.Run);
            }

            return _ringBuffer;
        }

        /// <summary>
        /// Wait for the <see cref="RingBuffer{T}"/> to drain of published events then halt the workers.
       /// </summary>
        public void DrainAndHalt()
        {
            var workerSequences = WorkerSequences;
            while (_ringBuffer.Cursor > Util.GetMinimumSequence(workerSequences))
            {
                Thread.Sleep(0);
            }

            for (var i = 0; i < _workProcessors.Length; i++)
            {
                var workProcessor = _workProcessors[i];
                workProcessor.Halt();
            }

            _running.MarkAsStopped();
        }

        /// <summary>
        /// Halt all workers immediately at then end of their current cycle.
        /// </summary>
        public void Halt()
        {
            for (var i = 0; i < _workProcessors.Length; i++)
            {
                var workProcessor = _workProcessors[i];
                workProcessor.Halt();
            }

            _running.MarkAsStopped();
        }

        public bool IsRunning => _running.IsRunning;
    }
}