using System;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace Disruptor.Tests;

[TestFixture]
public class ExplicitStructLayoutTests
{
    public static StructDescriptor[] StructDescriptors { get; } =
    [
        new(typeof(IpcRingBufferMemory.Header), 0, 0),
        new(typeof(IpcPublisherFields), 56, 56),
        new(typeof(IpcRingBufferFields), 56, 56),
        new(typeof(IpcSequenceBlock), 0, 56),
        new(typeof(IpcSequencer.SequenceCache), 64, 56),
        new(typeof(MultiProducerSequencer.SequenceCache), 64, 56),
        new(typeof(RingBuffer), 56, 56),
        new(typeof(Sequence), 56, 56),
        new(typeof(SequencePointer), 0, 0),
        new(typeof(SingleProducerSequencer.PaddedSequences), 64, 56),
        new(typeof(UnmanagedRingBuffer), 56, 56),
    ];

    [TestCaseSource(nameof(StructDescriptors))]
    public void ValidateStructLayout(StructDescriptor structDescriptor)
    {
        var layoutAttribute = structDescriptor.Type.StructLayoutAttribute;
        Assert.That(layoutAttribute, Is.Not.Null);
        Assert.That(layoutAttribute!.Value, Is.EqualTo(LayoutKind.Explicit));

        var nextOffset = structDescriptor.LeadingPadding;

        var fields = structDescriptor.Type.GetFields(BindingFlags.Instance | BindingFlags.Public);
        foreach (var field in fields)
        {
            var offsetAttribute = field.GetCustomAttribute<FieldOffsetAttribute>();
            Assert.That(offsetAttribute, Is.Not.Null);
            Assert.That(offsetAttribute!.Value, Is.GreaterThanOrEqualTo(nextOffset));

            nextOffset = offsetAttribute.Value + GetSize(field.FieldType);
        }

        var minimumSize = nextOffset + structDescriptor.TrailingPadding;
        Assert.That(layoutAttribute.Size, Is.GreaterThanOrEqualTo(minimumSize));
    }

    private static int GetSize(Type type)
    {
        if (type == typeof(bool))
            return sizeof(bool);

        if (type == typeof(int))
            return sizeof(int);

        if (type == typeof(long))
            return sizeof(long);

        if (!type.IsValueType)
            return IntPtr.Size;

        throw new NotSupportedException($"Unsupported type: {type.Name}");
    }

    public record StructDescriptor(Type Type, int LeadingPadding, int TrailingPadding)
    {
        public override string ToString() => Type.ToString();
    }
}
