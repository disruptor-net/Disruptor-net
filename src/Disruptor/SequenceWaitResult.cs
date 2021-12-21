using System;

namespace Disruptor
{
    /// <summary>
    /// Contains the result of a <see cref="IWaitStrategy"/>.
    /// </summary>
    public readonly struct SequenceWaitResult : IEquatable<SequenceWaitResult>
    {
        internal const long TimeoutValue = -3;

        public SequenceWaitResult(long availableSequence)
        {
            UnsafeAvailableSequence = availableSequence;
        }

        public static readonly SequenceWaitResult Timeout = new SequenceWaitResult(TimeoutValue);

        /// <summary>
        /// The available sequence, which may be greater than the requested sequence,
        /// or null if the result is a timeout.
        /// </summary>
        public long? AvailableSequence => IsTimeout ? null : (long?)UnsafeAvailableSequence;

        /// <summary>
        /// The available sequence, which may be greater than the requested sequence.
        /// </summary>
        /// <remarks>
        /// For performance and simplicity, this property does not verify if the result is a timeout.
        /// Please always check <see cref="IsTimeout"/> before using the property, or use  <see cref="AvailableSequence"/> instead.
        /// </remarks>
        public long UnsafeAvailableSequence { get; }

        /// <summary>
        /// Indicates whether the result is a timeout.
        /// </summary>
        public bool IsTimeout => UnsafeAvailableSequence == TimeoutValue;

        public static implicit operator SequenceWaitResult(long availableSequence) => new SequenceWaitResult(availableSequence);

        public bool Equals(SequenceWaitResult other)
        {
            return UnsafeAvailableSequence == other.UnsafeAvailableSequence;
        }

        public override bool Equals(object? obj)
        {
            return obj is SequenceWaitResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            return UnsafeAvailableSequence.GetHashCode();
        }

        public static bool operator ==(SequenceWaitResult left, SequenceWaitResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SequenceWaitResult left, SequenceWaitResult right)
        {
            return !left.Equals(right);
        }
    }
}
