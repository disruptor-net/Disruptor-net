using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Hardware.Info;

namespace Disruptor.PerfTests;

public class ComputerSpecifications
{
    private readonly HardwareInfo _hardwareInfo;

    private ComputerSpecifications(HardwareInfo hardwareInfo)
    {
        _hardwareInfo = hardwareInfo;
    }

    public static ComputerSpecifications GetCurrent()
    {
        var hardwareInfo = new HardwareInfo();
        hardwareInfo.RefreshOperatingSystem();
        hardwareInfo.RefreshCPUList();

        return new ComputerSpecifications(hardwareInfo);
    }

    public int? PhysicalCoreCount => _hardwareInfo.CpuList.Count;
    public int? LogicalCoreCount => _hardwareInfo.CpuList.Sum(x => x.CpuCoreList.Count);
    public bool IsHyperThreaded => LogicalCoreCount > PhysicalCoreCount;

    public override string ToString()
    {
        var builder = new StringBuilder();

        foreach (var line in GetLines())
        {
            builder.AppendLine(line);
        }

        return builder.ToString();
    }

    public void AppendHtml(StringBuilder builder)
    {
        foreach (var line in GetLines())
        {
            builder.Append(line);
            builder.AppendLine("<br>");
        }
    }

    private IEnumerable<string> GetLines()
    {
        yield return $"OperatingSystem: {_hardwareInfo.OperatingSystem.Name} {RuntimeInformation.OSArchitecture} ({_hardwareInfo.OperatingSystem.VersionString})";
        yield return $"Runtime: {RuntimeInformation.FrameworkDescription} ({RuntimeInformation.RuntimeIdentifier})";

        if (_hardwareInfo.CpuList.Count == 1)
        {
            foreach (var cpuInfo in GetCpuInfos("", _hardwareInfo.CpuList[0]))
            {
                yield return cpuInfo;
            }
        }
        else
        {
            foreach (var (index, cpu) in _hardwareInfo.CpuList.Index())
            {
                foreach (var cpuInfo in GetCpuInfos($" #{index}", cpu))
                {
                    yield return cpuInfo;
                }
            }
        }

        IEnumerable<string> GetCpuInfos(string suffix, CPU cpu)
        {
            yield return $"Processor{suffix}: {cpu.Name} ({cpu.NumberOfLogicalProcessors} logical cores, {cpu.NumberOfCores} physical cores)";
        }
    }
}
