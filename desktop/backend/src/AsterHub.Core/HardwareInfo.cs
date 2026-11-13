using System;
using System.Collections.Generic;
public class MemoryModuleInfo {
 public string Slot="未提供", Manufacturer="未提供", PartNumber="未提供", Generation="未提供", RatedSpeed="未提供", ConfiguredSpeed="未提供";
 public long CapacityBytes;
}
public class HardwareInfo {
 public string OperatingSystem="未提供",CpuModel="未提供", Source="", Note="", Error="";
 public int? PhysicalSockets, PhysicalCores, SystemThreads, AvailableThreads;
 public long? MemoryBytes;
 public List<MemoryModuleInfo> MemoryModules=new List<MemoryModuleInfo>();
 public DateTime CollectedUtc;
}
