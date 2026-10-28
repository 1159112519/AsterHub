using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

// ConPTY owns a new process/job; it never attaches/reparents an existing console window.
public sealed class ConPtySession : ITerminalSession {
 IntPtr pseudo, process, job; FileStream input,output; int disposed; readonly object gate=new object(); readonly BlockingCollection<byte[]> inputQueue=new BlockingCollection<byte[]>(128); int queuedBytes;
 public Func<string,Task> Output {get;set;} public event Action<int> Exited;
 public static string Quote(string value){var b=new StringBuilder("\"");int slashes=0;foreach(char c in value??""){if(c=='\\'){slashes++;continue;}if(c=='\"'){b.Append('\\',slashes*2+1);b.Append(c);}else{b.Append('\\',slashes);b.Append(c);}slashes=0;}b.Append('\\',slashes*2);b.Append('\"');return b.ToString();}
 public ConPtySession(string command,int columns,int rows){
  IntPtr readIn=IntPtr.Zero,writeIn=IntPtr.Zero,readOut=IntPtr.Zero,writeOut=IntPtr.Zero,attributes=IntPtr.Zero,value=IntPtr.Zero;
  PROCESS_INFORMATION pi=new PROCESS_INFORMATION();
  try {
   if(!CreatePipe(out readIn,out writeIn,IntPtr.Zero,0)||!CreatePipe(out readOut,out writeOut,IntPtr.Zero,0))Throw();
   int hr=CreatePseudoConsole(new COORD((short)columns,(short)rows),readIn,writeOut,0,out pseudo);if(hr!=0)Marshal.ThrowExceptionForHR(hr);
   CloseHandle(readIn);readIn=IntPtr.Zero;CloseHandle(writeOut);writeOut=IntPtr.Zero;
   IntPtr size=IntPtr.Zero;InitializeProcThreadAttributeList(IntPtr.Zero,1,0,ref size);attributes=Marshal.AllocHGlobal(size);if(!InitializeProcThreadAttributeList(attributes,1,0,ref size))Throw();
   if(!UpdateProcThreadAttribute(attributes,0,(IntPtr)0x00020016,pseudo,(IntPtr)IntPtr.Size,IntPtr.Zero,IntPtr.Zero))Throw();
   STARTUPINFOEX si=new STARTUPINFOEX();si.StartupInfo.cb=Marshal.SizeOf(typeof(STARTUPINFOEX));si.lpAttributeList=attributes;
   if(!CreateProcess(null,new StringBuilder(command),IntPtr.Zero,IntPtr.Zero,false,0x00080004,IntPtr.Zero,null,ref si,out pi))Throw();
   process=pi.hProcess;job=CreateJobObject(IntPtr.Zero,null);if(job==IntPtr.Zero)Throw();
   JOBOBJECT_EXTENDED_LIMIT_INFORMATION limits=new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();limits.BasicLimitInformation.LimitFlags=0x2000;
   value=Marshal.AllocHGlobal(Marshal.SizeOf(limits));Marshal.StructureToPtr(limits,value,false);
   if(!SetInformationJobObject(job,9,value,(uint)Marshal.SizeOf(limits))||!AssignProcessToJobObject(job,process))Throw();
   input=new FileStream(new SafeFileHandle(writeIn,true),FileAccess.Write,4096,false);writeIn=IntPtr.Zero;
   output=new FileStream(new SafeFileHandle(readOut,true),FileAccess.Read,4096,false);readOut=IntPtr.Zero;
   ResumeThread(pi.hThread);CloseHandle(pi.hThread);pi.hThread=IntPtr.Zero;
  }catch{if(pi.hThread!=IntPtr.Zero)CloseHandle(pi.hThread);if(process!=IntPtr.Zero)TerminateProcess(process,1);Dispose();throw;}
  finally{if(attributes!=IntPtr.Zero){DeleteProcThreadAttributeList(attributes);Marshal.FreeHGlobal(attributes);}if(value!=IntPtr.Zero)Marshal.FreeHGlobal(value);foreach(IntPtr h in new[]{readIn,writeIn,readOut,writeOut})if(h!=IntPtr.Zero)CloseHandle(h);}
 }
 public void Start(){Task.Run(delegate{try{foreach(var bytes in inputQueue.GetConsumingEnumerable()){if(disposed!=0)break;try{input.Write(bytes,0,bytes.Length);input.Flush();}finally{Interlocked.Add(ref queuedBytes,-bytes.Length);}}}catch(IOException){}catch(ObjectDisposedException){}});Task.Run(async delegate{try{using(var reader=new StreamReader(output,new UTF8Encoding(false,false),false,4096,true)){char[] chars=new char[4096];int n;while(disposed==0&&(n=reader.Read(chars,0,chars.Length))>0){var handler=Output;if(handler!=null)await handler(new string(chars,0,n)).ConfigureAwait(false);}}}catch(IOException){}catch(ObjectDisposedException){}finally{uint code=0;if(process!=IntPtr.Zero){WaitForSingleObject(process,2000);GetExitCodeProcess(process,out code);}var handler=Exited;if(handler!=null)handler((int)code);}});}
 public void Write(string text){if(disposed!=0||string.IsNullOrEmpty(text))return;byte[] bytes=Encoding.UTF8.GetBytes(text);int count=Interlocked.Add(ref queuedBytes,bytes.Length);if(count>1024*1024){Interlocked.Add(ref queuedBytes,-bytes.Length);throw new InvalidOperationException("终端输入繁忙，请稍后重试");}if(!inputQueue.TryAdd(bytes)){Interlocked.Add(ref queuedBytes,-bytes.Length);throw new InvalidOperationException("终端输入队列已满，请稍后重试");}}
 public void Resize(int columns,int rows){if(disposed==0&&pseudo!=IntPtr.Zero)ResizePseudoConsole(pseudo,new COORD((short)Math.Max(2,Math.Min(500,columns)),(short)Math.Max(2,Math.Min(300,rows))));}
 public void Dispose(){if(Interlocked.Exchange(ref disposed,1)!=0)return;inputQueue.CompleteAdding();if(job!=IntPtr.Zero){CloseHandle(job);job=IntPtr.Zero;}if(input!=null)input.Dispose();IntPtr p=pseudo;pseudo=IntPtr.Zero;if(p!=IntPtr.Zero)Task.Run(delegate{ClosePseudoConsole(p);if(output!=null)output.Dispose();});if(process!=IntPtr.Zero){CloseHandle(process);process=IntPtr.Zero;}}
 static void Throw(){throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}
 [StructLayout(LayoutKind.Sequential)]struct COORD{public short X,Y;public COORD(short x,short y){X=x;Y=y;}}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct STARTUPINFO{public int cb;public string lpReserved,lpDesktop,lpTitle;public int dwX,dwY,dwXSize,dwYSize,dwXCountChars,dwYCountChars,dwFillAttribute,dwFlags;public short wShowWindow,cbReserved2;public IntPtr lpReserved2,hStdInput,hStdOutput,hStdError;}
 [StructLayout(LayoutKind.Sequential)]struct STARTUPINFOEX{public STARTUPINFO StartupInfo;public IntPtr lpAttributeList;}
 [StructLayout(LayoutKind.Sequential)]struct PROCESS_INFORMATION{public IntPtr hProcess,hThread;public uint dwProcessId,dwThreadId;}
 [StructLayout(LayoutKind.Sequential)]struct JOBOBJECT_BASIC_LIMIT_INFORMATION{public long PerProcessUserTimeLimit,PerJobUserTimeLimit;public uint LimitFlags;public UIntPtr MinimumWorkingSetSize,MaximumWorkingSetSize;public uint ActiveProcessLimit;public UIntPtr Affinity;public uint PriorityClass,SchedulingClass;}
 [StructLayout(LayoutKind.Sequential)]struct IO_COUNTERS{public ulong ReadOperationCount,WriteOperationCount,OtherOperationCount,ReadTransferCount,WriteTransferCount,OtherTransferCount;}
 [StructLayout(LayoutKind.Sequential)]struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION{public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;public IO_COUNTERS IoInfo;public UIntPtr ProcessMemoryLimit,JobMemoryLimit,PeakProcessMemoryUsed,PeakJobMemoryUsed;}
 [DllImport("kernel32",SetLastError=true)]static extern bool CreatePipe(out IntPtr read,out IntPtr write,IntPtr attrs,int size);
 [DllImport("kernel32")]static extern int CreatePseudoConsole(COORD size,IntPtr input,IntPtr output,uint flags,out IntPtr handle);
 [DllImport("kernel32")]static extern int ResizePseudoConsole(IntPtr handle,COORD size);
 [DllImport("kernel32")]static extern void ClosePseudoConsole(IntPtr handle);
 [DllImport("kernel32",SetLastError=true)]static extern bool InitializeProcThreadAttributeList(IntPtr list,int count,int flags,ref IntPtr size);
 [DllImport("kernel32",SetLastError=true)]static extern bool UpdateProcThreadAttribute(IntPtr list,uint flags,IntPtr attribute,IntPtr value,IntPtr size,IntPtr previous,IntPtr returnSize);
 [DllImport("kernel32")]static extern void DeleteProcThreadAttributeList(IntPtr list);
 [DllImport("kernel32",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CreateProcess(string app,StringBuilder command,IntPtr pa,IntPtr ta,bool inherit,uint flags,IntPtr env,string cwd,ref STARTUPINFOEX si,out PROCESS_INFORMATION pi);
 [DllImport("kernel32",SetLastError=true)]static extern IntPtr CreateJobObject(IntPtr attrs,string name);
 [DllImport("kernel32",SetLastError=true)]static extern bool SetInformationJobObject(IntPtr job,int type,IntPtr info,uint length);
 [DllImport("kernel32",SetLastError=true)]static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
 [DllImport("kernel32")]static extern uint ResumeThread(IntPtr thread);
 [DllImport("kernel32")]static extern uint WaitForSingleObject(IntPtr handle,uint wait);
 [DllImport("kernel32")]static extern bool GetExitCodeProcess(IntPtr process,out uint code);
 [DllImport("kernel32")]static extern bool TerminateProcess(IntPtr process,uint code);
 [DllImport("kernel32")]static extern bool CloseHandle(IntPtr handle);
}
