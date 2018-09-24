using System;

namespace Disruptor.Tests.Example.PullWithBatchedPoller
{
    public class BatchedPoller<T> where T : class 
    {
        private readonly EventPoller<DataEvent> _poller;
        private readonly int _maxBatchSize;
        private readonly BatchedData _polledData;

        public BatchedPoller(RingBuffer<DataEvent> ringBuffer, int batchSize)
        {
            _poller = ringBuffer.NewPoller();
            ringBuffer.AddGatingSequences(_poller.Sequence);

            if (batchSize < 1)
            {
                batchSize = 20;
            }
            _maxBatchSize = batchSize;
            _polledData = new BatchedData(_maxBatchSize);
        }

        public T Poll()
        {
            if (_polledData.GetMsgCount() > 0)
            {
                return _polledData.PollMessage(); // we just fetch from our local
            }

            LoadNextValues(_poller, _polledData); // we try to load from the ring
            return _polledData.GetMsgCount() > 0 ? _polledData.PollMessage() : null;
        }

        private PollState LoadNextValues(EventPoller<DataEvent> poller, BatchedData batch)
        {
            return poller.Poll((ev, sequence, endOfBatch) =>
                               {
                                   var item = ev.CopyOfData();
                                   return item != null && batch.AddDataItem(item);
                               });
        }

        public class DataEvent
        {
            public T Data { get; set; }

            public T CopyOfData()
            {
                // Copy the data out here. In this case we have a single reference
                // object, so the pass by
                // reference is sufficient. But if we were reusing a byte array,
                // then we
                // would need to copy
                // the actual contents.
                return Data;
            }

            void Set(T d)
            {
                Data = d;
            }
        }

        public class BatchedData
        {

            private int _msgHighBound;
            private readonly int _capacity;
            private readonly T[] _data;
            private int _cursor;

            public BatchedData(int size)
            {
                _capacity = size;
                _data = new T[_capacity];
            }

            private void ClearCount()
            {
                _msgHighBound = 0;
                _cursor = 0;
            }

            public int GetMsgCount()
            {
                return _msgHighBound - _cursor;
            }

            public bool AddDataItem(T item)
            {
                if (_msgHighBound >= _capacity)
                {
                    throw new ArgumentOutOfRangeException("Attempting to add item to full batch");
                }

                _data[_msgHighBound++] = item;
                return _msgHighBound < _capacity;
            }

            public T PollMessage()
            {
                T rtVal = default(T);
                if (_cursor < _msgHighBound)
                {
                    rtVal = _data[_cursor++];
                }
                if (_cursor > 0 && _cursor >= _msgHighBound)
                {
                    ClearCount();
                }
                return rtVal;
            }
        }
    }

}
