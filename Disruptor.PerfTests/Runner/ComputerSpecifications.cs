using System;
using System.Management;
using System.Text;

namespace Disruptor.PerfTests.Runner
{
	/// <summary>
	/// A class that uses the System.Management APIS (WMI) to fetch the most 
	/// interesting attributes about the computer hardware we are running on.  
	/// Based on Vance Morrison's MeasureIt tool: http://blogs.msdn.com/b/vancem/archive/2009/02/06/measureit-update-tool-for-doing-microbenchmarks.aspx
	/// </summary>
	public abstract class ComputerSpecifications
	{
		public virtual string Name { get; protected set; }

		public virtual string Manufacturer { get; protected set; }

		public virtual string Model { get; protected set; }

		public virtual string OperatingSystem { get; protected set; }

		public virtual string OperatingSystemVersion { get; protected set; }

		public virtual int OperatingSystemServicePack { get; protected set; }

		public virtual int NumberOfProcessors { get; protected set; }

		public virtual string ProcessorName { get; protected set; }

		public virtual string ProcessorDescription { get; protected set; }

		public virtual int ProcessorClockSpeedMhz { get; protected set; }

		public virtual int MemoryMBytes { get; protected set; }

		public virtual int L1KBytes { get; protected set; }

		public virtual int L2KBytes { get; protected set; }

		public virtual int NumberOfCores { get; protected set; }

		public virtual int NumberOfLogicalProcessors { get; protected set; }

		public virtual int L3KBytes { get; protected set; }

		public static ComputerSpecifications GetSpecificationsForPlatform ()
		{
			switch ((int)Environment.OSVersion.Platform) {
			case 0:
			case 1:
			case 2:
			case 3:
				return new WindowsComputerSpecifications ();
			case 4:
			case 6:
			case 128:
				return GetUnixSpecifications ();
			default:
				//You're probably running on Windows with the .NET framework
				return new WindowsComputerSpecifications ();
			}
		}
		
		private static ComputerSpecifications GetUnixSpecifications ()
		{
			return new DarwinComputerSpecifications();
		}

		public bool IsHyperThreaded {
			get { return NumberOfCores != NumberOfLogicalProcessors; }
		}

		public override string ToString ()
		{
			var builder = new StringBuilder ();
			builder.Append ("Name: ").AppendLine (Name);
			builder.Append ("Manufacturer: ").AppendLine (Manufacturer);
			builder.Append ("Model: ").AppendLine (Model);
			builder.AppendLine ();
			builder.Append ("Operating System: ").AppendLine (OperatingSystem);
			builder.Append (" - Version: ").AppendLine (OperatingSystemVersion);
			builder.Append (" - ServicePack: ").Append (OperatingSystemServicePack).AppendLine ();
			builder.AppendLine ();
			builder.Append ("Number of Processors: ").Append (NumberOfProcessors).AppendLine ();
			builder.Append (" - Name: ").AppendLine (ProcessorName);
			builder.Append (" - Description: ").AppendLine (ProcessorDescription);
			builder.Append (" - ClockSpeed: ").Append (ProcessorClockSpeedMhz).AppendLine (" Mhz");
			builder.Append (" - Number of cores: ").AppendLine (NumberOfCores.ToString ());
			builder.Append (" - Number of logical processors: ").AppendLine (NumberOfLogicalProcessors.ToString ());
			builder.Append (" - Hyperthreading: ").AppendLine (IsHyperThreaded ? "ON" : "OFF");
			builder.AppendLine ();
			builder.Append ("Memory: ").Append (MemoryMBytes).AppendLine (" MBytes");
			builder.Append (" - L1Cache: ").Append (L1KBytes).AppendLine (" KBytes");
			builder.Append (" - L2Cache: ").Append (L2KBytes).AppendLine (" KBytes");
			builder.Append (" - L3Cache: ").Append (L3KBytes).AppendLine (" KBytes");
			return builder.ToString ();
		}

		public void AppendHtml (StringBuilder builder)
		{
			builder.Append ("Name: ").Append (Name).AppendLine ("<br>");
			builder.Append ("Manufacturer: ").Append (Manufacturer).AppendLine ("<br>");
			builder.Append ("Model: ").Append (Model).AppendLine ("<br>");
			builder.AppendLine ("<br>");
			builder.Append ("Operating System: ").Append (OperatingSystem).AppendLine ("<br>");
			builder.Append (" - Version: ").Append (OperatingSystemVersion).AppendLine ("<br>");
			builder.Append (" - ServicePack: ").Append (OperatingSystemServicePack).AppendLine ("<br>");
			builder.AppendLine ("<br>");
			builder.Append ("Number of Processors: ").Append (NumberOfProcessors).AppendLine ("<br>");
			builder.Append (" - Name: ").Append (ProcessorName).AppendLine ("<br>");
			builder.Append (" - Description: ").Append (ProcessorDescription).AppendLine ("<br>");
			builder.Append (" - ClockSpeed: ").Append (ProcessorClockSpeedMhz).AppendLine (" Mhz").AppendLine ("<br>");
			builder.Append (" - Number of cores: ").Append (NumberOfCores.ToString ()).AppendLine ("<br>");
			builder.Append (" - Number of logical processors: ").Append (NumberOfLogicalProcessors.ToString ()).AppendLine ("<br>");
			builder.Append (" - Hyperthreading: ").Append (IsHyperThreaded ? "ON" : "OFF").AppendLine ("<br>");
			builder.AppendLine ("<br>");
			builder.Append ("Memory: ").Append (MemoryMBytes).Append (" MBytes").AppendLine ("<br>");
			builder.Append ("L1Cache: ").Append (L1KBytes).Append (" KBytes").AppendLine ("<br>");
			builder.Append ("L2Cache: ").Append (L2KBytes).Append (" KBytes").AppendLine ("<br>");
			builder.Append ("L3Cache: ").Append (L3KBytes).Append (" KBytes").AppendLine ("<br>");
		}
		
		private class DarwinComputerSpecifications : ComputerSpecifications
		{
			public DarwinComputerSpecifications ()
			{
			}
		}
		
		internal class LinuxComputerSpecification : ComputerSpecifications
		{
			public LinuxComputerSpecification ()
			{
			}
		}
		
		internal class WindowsComputerSpecifications : ComputerSpecifications
		{
			public WindowsComputerSpecifications ()
			{
				var searcher = new ManagementObjectSearcher ("Select * from Win32_ComputerSystem");

				foreach (ManagementObject mo in searcher.Get()) {
					Name = (string)mo ["Caption"];
					Manufacturer = (string)mo ["Manufacturer"];
					Model = (string)mo ["Model"];
					MemoryMBytes = (int)(((ulong)mo ["TotalPhysicalMemory"]) / (1024 * 1024));
				}

				NumberOfLogicalProcessors = Environment.ProcessorCount;

				searcher = new ManagementObjectSearcher ("Select * from Win32_OperatingSystem");
				foreach (ManagementObject mo in searcher.Get()) {
					OperatingSystem = (string)mo ["Caption"];
					OperatingSystemVersion = (string)mo ["Version"];
					OperatingSystemServicePack = (ushort)mo ["ServicePackMajorVersion"];
					break;
				}
            
				searcher = new ManagementObjectSearcher ("Select * from Win32_Processor");
				ManagementObjectCollection processors = searcher.Get ();
				NumberOfProcessors = processors.Count;
				foreach (ManagementObject mo in processors) {
					ProcessorName = (string)mo ["Name"];
					ProcessorDescription = (string)mo ["Description"];
					ProcessorClockSpeedMhz = (int)(uint)mo ["MaxClockSpeed"];
					L3KBytes = (int)(uint)mo ["L3CacheSize"];
					NumberOfCores += int.Parse (mo ["NumberOfCores"].ToString ());

					break;
				}

				searcher = new ManagementObjectSearcher ("Select * from Win32_CacheMemory");
				foreach (ManagementObject mo in searcher.Get()) {
					int level = (ushort)mo ["Level"] - 2;
					if (level == 1)
						L1KBytes += (int)(uint)mo ["InstalledSize"];
					else if (level == 2)
						L2KBytes += (int)(uint)mo ["InstalledSize"];
				}
			}
		}
	}
}
