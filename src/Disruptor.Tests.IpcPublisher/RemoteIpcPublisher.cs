using System.Diagnostics;

namespace Disruptor.Tests.IpcPublisher;

public static class RemoteIpcPublisher
{
    public static Process Start(string command, string commandArguments, string? ipcPublisherPath = null)
    {
        var processStartInfo = CreateProcessStartInfo();
        processStartInfo.RedirectStandardOutput = true;
        processStartInfo.RedirectStandardError = true;

        var process = Process.Start(processStartInfo)!;

        Forward(process.StandardOutput, Console.Out);
        Forward(process.StandardError, Console.Out);

        return process;

        ProcessStartInfo CreateProcessStartInfo()
        {
            var publisherPath = ipcPublisherPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Disruptor.Tests.IpcPublisher.dll");
            return Path.GetExtension(publisherPath).Equals(".dll", StringComparison.OrdinalIgnoreCase)
                ? new ProcessStartInfo("dotnet", $"{publisherPath} {command} {commandArguments}")
                : new ProcessStartInfo(publisherPath, $"{command} {commandArguments}");
        }
    }

    private static void Forward(StreamReader reader, TextWriter writer)
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
