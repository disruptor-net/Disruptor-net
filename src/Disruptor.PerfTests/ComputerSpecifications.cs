using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Disruptor.PerfTests
{
    /// <summary>
    /// A class that uses the System.Management APIS (WMI) to fetch the most 
    /// interesting attributes about the computer hardware we are running on.  
    /// Based on Vance Morrison's MeasureIt tool: http://blogs.msdn.com/b/vancem/archive/2009/02/06/measureit-update-tool-for-doing-microbenchmarks.aspx
    /// </summary>
    public class ComputerSpecifications
    {
        public readonly string Name;
        public readonly string Manufacturer;
        public readonly string Model;

        public readonly string OperatingSystem;
        public readonly string OperatingSystemVersion;
        public readonly string OperatingSystemServicePack;

        public readonly int NumberOfProcessors;
        public readonly string ProcessorName;
        public readonly string ProcessorDescription;
        public readonly int ProcessorClockSpeedMhz;

        public readonly int MemoryMBytes;
        public readonly int L1KBytes;
        public readonly int L2KBytes;
        public readonly int NumberOfCores;
        public readonly int NumberOfLogicalProcessors;
        public readonly int L3KBytes;

        public ComputerSpecifications()
        {
            NumberOfLogicalProcessors = Environment.ProcessorCount;

            OperatingSystem = RuntimeInformation.OSDescription;
            OperatingSystemVersion = Environment.OSVersion.VersionString;
            OperatingSystemServicePack = Environment.OSVersion.ServicePack.ToString();
        }

        public bool IsHyperThreaded
        {
            get { return NumberOfCores != NumberOfLogicalProcessors; }
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append("Name: ").AppendLine(Name);
            builder.Append("Manufacturer: ").AppendLine(Manufacturer);
            builder.Append("Model: ").AppendLine(Model);
            builder.AppendLine();
            builder.Append("Operating System: ").AppendLine(OperatingSystem);
            builder.Append(" - Version: ").AppendLine(OperatingSystemVersion);
            builder.Append(" - ServicePack: ").Append(OperatingSystemServicePack).AppendLine();
            builder.AppendLine();
            builder.Append("Number of Processors: ").Append(NumberOfProcessors).AppendLine();
            builder.Append(" - Name: ").AppendLine(ProcessorName);
            builder.Append(" - Description: ").AppendLine(ProcessorDescription);
            builder.Append(" - ClockSpeed: ").Append(ProcessorClockSpeedMhz).AppendLine(" Mhz");
            builder.Append(" - Number of cores: ").AppendLine(NumberOfCores.ToString());
            builder.Append(" - Number of logical processors: ").AppendLine(NumberOfLogicalProcessors.ToString());
            builder.Append(" - Hyperthreading: ").AppendLine(IsHyperThreaded ? "ON" : "OFF");
            builder.AppendLine();
            builder.Append("Memory: ").Append(MemoryMBytes).AppendLine(" MBytes");
            builder.Append(" - L1Cache: ").Append(L1KBytes).AppendLine(" KBytes");
            builder.Append(" - L2Cache: ").Append(L2KBytes).AppendLine(" KBytes");
            builder.Append(" - L3Cache: ").Append(L3KBytes).AppendLine(" KBytes");
            return builder.ToString();
        }

        public void AppendHtml(StringBuilder builder)
        {
            builder.Append("Name: ").Append(Name).AppendLine("<br>");
            builder.Append("Manufacturer: ").Append(Manufacturer).AppendLine("<br>");
            builder.Append("Model: ").Append(Model).AppendLine("<br>");
            builder.AppendLine("<br>");
            builder.Append("Operating System: ").Append(OperatingSystem).AppendLine("<br>");
            builder.Append(" - Version: ").Append(OperatingSystemVersion).AppendLine("<br>");
            builder.Append(" - ServicePack: ").Append(OperatingSystemServicePack).AppendLine("<br>");
            builder.AppendLine("<br>");
            builder.Append("Number of Processors: ").Append(NumberOfProcessors).AppendLine("<br>");
            builder.Append(" - Name: ").Append(ProcessorName).AppendLine("<br>");
            builder.Append(" - Description: ").Append(ProcessorDescription).AppendLine("<br>");
            builder.Append(" - ClockSpeed: ").Append(ProcessorClockSpeedMhz).AppendLine(" Mhz").AppendLine("<br>");
            builder.Append(" - Number of cores: ").Append(NumberOfCores.ToString()).AppendLine("<br>");
            builder.Append(" - Number of logical processors: ").Append(NumberOfLogicalProcessors.ToString()).AppendLine("<br>");
            builder.Append(" - Hyperthreading: ").Append(IsHyperThreaded ? "ON" : "OFF").AppendLine("<br>");
            builder.AppendLine("<br>");
            builder.Append("Memory: ").Append(MemoryMBytes).Append(" MBytes").AppendLine("<br>");
            builder.Append("L1Cache: ").Append(L1KBytes).Append(" KBytes").AppendLine("<br>");
            builder.Append("L2Cache: ").Append(L2KBytes).Append(" KBytes").AppendLine("<br>");
            builder.Append("L3Cache: ").Append(L3KBytes).Append(" KBytes").AppendLine("<br>");
        }
    }
}
