using System;
using Disruptor.Tests.Support;

namespace Disruptor.Tests.Dsl
{
    internal class EvilEqualsEventHandler : IEventHandler<TestEvent>
    {   
        public void OnNext(TestEvent data, long sequence, bool endOfBatch)
        {    
        }    

        public override bool Equals(Object o)
        {
            return true;
        }

        public override int GetHashCode()
        {
            return 1;
        }
    }
}