using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using Renci.SshNet;

public interface ITerminalSession : IDisposable {
    Func<string,Task> Output { get; set; }
    event Action<int> Exited;
    void Start(); void Write(string text); void Resize(int columns,int rows);
}

// Managed SSH authentication shares the exact host-key/password policy of files and metrics.
public sealed class SshTerminalSession : ITerminalSession {
    readonly Server server; readonly string command; SshClient client; ShellStream shell;
    readonly object gate=new object(); int columns,rows,disposed,started,channelClosed;Exception terminalError;
    public Func<string,Task> Output { get; set; } public event Action<int> Exited;
    public SshTerminalSession(Server s,string remoteCommand,int cols,int lines) { server=s.Copy();command=remoteCommand;columns=cols;rows=lines; }
    public void Start() {
        if(Interlocked.Exchange(ref started,1)!=0)return;
        Task.Run(async()=>{
            int exit=0;
            try {
                var connection=SshAuthentication.Client(server);
                lock(gate) { if(disposed!=0){connection.Dispose();return;} client=connection; }
                connection.Connect();
                lock(gate) {
                    if(disposed!=0)return;
                    shell=connection.CreateShellStream("xterm-256color",(uint)columns,(uint)rows,0,0,65536);
                    shell.Closed+=(o,e)=>Interlocked.Exchange(ref channelClosed,1);
                    shell.ErrorOccurred+=(o,e)=>{terminalError=e.Exception;Interlocked.Exchange(ref channelClosed,1);};
                    Resize(columns,rows);
                    shell.WriteLine("stty -echo; printf '\\033]1337;AQREADY\\007'; "+command);
                }
                var decoder=Encoding.UTF8.GetDecoder();var bytes=new byte[16384];var chars=new char[16384];
                string initial="";bool ready=false;
                while(disposed==0) {
                    if(!shell.DataAvailable){if(Volatile.Read(ref channelClosed)!=0||!connection.IsConnected)break;await Task.Delay(15).ConfigureAwait(false);continue;}
                    int n=shell.Read(bytes,0,bytes.Length); if(n<=0)continue;
                    string text=new string(chars,0,decoder.GetChars(bytes,0,n,chars,0));
                    if(!ready) { initial+=text;int marker=initial.IndexOf("\x1b]1337;AQREADY\x07",StringComparison.Ordinal);if(marker<0){if(initial.Length>65536)throw new IOException("终端初始化响应过大。");continue;} text=initial.Substring(marker+"\x1b]1337;AQREADY\x07".Length); initial=""; ready=true; }
                    var output=Output;if(output!=null&&!string.IsNullOrEmpty(text))await output(text).ConfigureAwait(false);
                }
                if(terminalError!=null&&disposed==0)throw terminalError;
            }catch(Exception error){exit=1;var output=Output;if(disposed==0&&output!=null)output("\r\n连接失败："+error.Message+"\r\n").GetAwaiter().GetResult();}
            finally { Dispose();var handler=Exited;if(handler!=null)handler(exit); }
        });
    }
    public void Write(string text) { lock(gate) { if(disposed!=0)return;if(shell==null)throw new InvalidOperationException("终端正在连接，请稍候。");if((text??"").Length>1024*1024)throw new InvalidOperationException("终端输入超过限制。");shell.Write(text);shell.Flush(); } }
    public void Resize(int cols,int lines) {
        lock(gate) {
            columns=Math.Max(2,Math.Min(500,cols));rows=Math.Max(2,Math.Min(300,lines));if(shell==null||disposed!=0)return;
            // Pinned SSH.NET exposes window-change only on its internal channel. Keep compatibility in this adapter.
            var field=typeof(ShellStream).GetField("_channel",BindingFlags.NonPublic|BindingFlags.Instance);
            object channel=field==null?null:field.GetValue(shell);
            var method=channel==null?null:channel.GetType().GetMethod("SendWindowChangeRequest",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
            if(method==null)throw new NotSupportedException("当前 SSH 组件不支持终端尺寸调整。");
            method.Invoke(channel,new object[]{(uint)columns,(uint)rows,(uint)0,(uint)0});
        }
    }
    public void Dispose() { if(Interlocked.Exchange(ref disposed,1)!=0)return;lock(gate){if(shell!=null)shell.Dispose();if(client!=null)client.Dispose();} }
}
