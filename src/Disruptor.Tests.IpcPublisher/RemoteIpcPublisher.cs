using System.Diagnostics;

namespace Disruptor.Tests.IpcPublisher;

public static class RemoteIpcPublisher
{
    public static Process Start(string command, string commandArguments)
    {
        var dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Disruptor.Tests.IpcPublisher.dll");
        var arguments = $"{dllPath} {command} {commandArguments}";

        var process = Process.Start(new ProcessStartInfo("dotnet", arguments)
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
