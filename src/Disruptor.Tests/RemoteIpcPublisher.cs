using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Disruptor.Tests;

public static class RemoteIpcPublisher
{
    public static void Run(string command, string commandArguments)
    {
        var process = Start(command, commandArguments);

        Assert.That(process.WaitForExit(5000));
        Assert.That(process.ExitCode, Is.EqualTo(0));
    }

    public static Process Start(string command, string commandArguments)
    {
        var arguments = $"Disruptor.Tests.IpcPublisher.dll {command} {commandArguments}";

        var process = Process.Start(new ProcessStartInfo($"dotnet", arguments)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        })!;

        Forward(process.StandardOutput, Console.Out);
        Forward(process.StandardError, Console.Out);

        return process;

        static void Forward(StreamReader reader, TextWriter writer)
        {
            ForwardImpl(reader, writer).ContinueWith(t => Console.Error.WriteLine(t.Exception!.ToString()), TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);

            static async Task ForwardImpl(StreamReader reader, TextWriter writer)
            {
                while (await reader.ReadLineAsync() is { } s)
                {
                    await writer.WriteLineAsync(s);
                }
            }
        }
    }
}
