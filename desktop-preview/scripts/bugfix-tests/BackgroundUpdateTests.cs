using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
[assembly:AssemblyInformationalVersion("0.3.0-preview.22")]
public static class Storage {public static string PathName=Path.Combine(Path.GetTempPath(),"asterhub-background-update-tests");}
internal static class BackgroundUpdateTests {
 static int failures;static void Assert(bool value,string label){Console.WriteLine((value?"PASS ":"FAIL ")+label);if(!value)failures++;}
 static ReleaseInfo Release(){return new ReleaseInfo{version="0.3.0-preview.99",notes="fixture"};}
 static bool Wait(Func<bool> condition){return SpinWait.SpinUntil(condition,3000);}
 static void Main(){
  int starts=0;CancellationToken token=default(CancellationToken);var completion=new TaskCompletionSource<string>();var entered=new ManualResetEventSlim();IProgress<double> reporter=null;
  var task=new BackgroundUpdateTask("0.3.0-preview.22",cancel=>Task.FromResult(Release()),(r,p,c)=>{Interlocked.Increment(ref starts);token=c;reporter=p;entered.Set();return completion.Task;},null);
  task.Check().GetAwaiter().GetResult();var start=task.Start();entered.Wait(3000);reporter.Report(41);
  Assert(start.state=="downloading"&&task.Status().progress==41,"download starts immediately and exposes a native progress snapshot");
  task.Start();Assert(starts==1,"repeated panel requests share one native task");
  var remounted=task.Status();Assert(remounted.state=="downloading"&&remounted.latest=="0.3.0-preview.99"&&remounted.progress==41,"a reopened panel recovers release and progress");
  task.Cancel();Assert(token.IsCancellationRequested&&task.Status().state=="cancelling","cancel signals actual downloader token and waits for settlement");
  task.Start();Assert(starts==1,"cannot restart while old download is cancelling");
  completion.SetResult("verified.exe");Assert(Wait(()=>task.Status().state=="cancelled")&&!task.Status().ready,"cancel racing completion cannot become ready");
  bool installed=false;try{task.Install((path,r)=>installed=true);}catch(InvalidOperationException){}Assert(!installed,"cancelled task cannot install");
  var complete=new BackgroundUpdateTask("0.3.0-preview.22",cancel=>Task.FromResult(Release()),(r,p,c)=>Task.FromResult("verified.exe"),null);
  complete.Check().GetAwaiter().GetResult();complete.Start();Wait(()=>complete.Status().ready);
  Assert(!installed&&complete.Status().progress==100,"download completion waits for separate installation request");
  complete.Install((path,r)=>installed=true);Assert(installed&&complete.Status().state=="installing","only explicit install starts installation");
  var invalid=new BackgroundUpdateTask("0.3.0-preview.22",cancel=>Task.FromResult(Release()),(r,p,c)=>{throw new InvalidDataException("安装包校验失败，请重新下载。");},null);invalid.Check().GetAwaiter().GetResult();invalid.Start();Wait(()=>invalid.Status().state=="error");Assert(!invalid.Status().ready&&invalid.Status().error=="安装包校验失败，请重新下载。","hash validation failure remains explicit and cannot become installable");
  int attempts=0;var retry=new BackgroundUpdateTask("0.3.0-preview.22",cancel=>Task.FromResult(Release()),(r,p,c)=>{attempts++;if(attempts==1)throw new IOException("interrupted");return Task.FromResult("verified.exe");},null);
  retry.Check().GetAwaiter().GetResult();retry.Start();Wait(()=>retry.Status().state=="error");Assert(!retry.Status().ready&&retry.Status().available,"background error remains in status with retryable release");retry.Start();Wait(()=>retry.Status().ready);Assert(attempts==2&&retry.Status().error==null,"retry clears prior error and finishes same verified release");
  int stopped=0;var stop=new BackgroundUpdateTask("0.3.0-preview.22",cancel=>Task.FromResult(Release()),async(r,p,c)=>{try{await Task.Delay(5000,c);return "fixture";}finally{Interlocked.Increment(ref stopped);}},null);stop.Check().GetAwaiter().GetResult();stop.Start();stop.Stop().GetAwaiter().GetResult();Assert(stopped==1&&stop.Status().state=="cancelled","real application exit waits for downloader cleanup");
  try{Assert(DownloadCancellationScenario.Run(),"real local range transfer cancels, keeps completed chunks and resumes with final hash verification");}catch(Exception ex){Console.WriteLine(ex);Assert(false,"real local range transfer cancellation and resume");}
  Console.WriteLine("FAILURES="+failures);Environment.ExitCode=failures==0?0:1;
 }
}
