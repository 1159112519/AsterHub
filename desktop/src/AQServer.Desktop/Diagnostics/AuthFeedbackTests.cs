using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Reflection;

public static class AuthFeedbackTests {
    static readonly List<string> Log=new List<string>();
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);Log.Add("PASS "+label);}
    static T Field<T>(object owner,string name){return (T)owner.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(owner);}
    public static int Run(string folder) {
        Directory.CreateDirectory(folder);Log.Clear();
        try {
            string secret=Guid.NewGuid().ToString("N");var server=new Server{Name="test",Host="example.test",User="test",AuthMode="password",Password=secret};
            server.ProtectedPassword=SshAuthentication.Protect(secret);server.Password=null;
            Check(SshAuthentication.Password(server.Copy())==secret,"DPAPI password roundtrip and Server.Copy");
            var xml=new StringWriter();new XmlSerializer(typeof(Server)).Serialize(xml,server);
            Check(!xml.ToString().Contains(secret)&&xml.ToString().Contains("ProtectedPassword"),"saved XML excludes plaintext password");
            server.Password=secret;xml=new StringWriter();new XmlSerializer(typeof(Server)).Serialize(xml,server);Check(!xml.ToString().Contains(secret),"session-only password excluded from XML");
            server.Password=null;server.ProtectedPassword=Convert.ToBase64String(new byte[32]);bool bad=false;try{SshAuthentication.Password(server);}catch(InvalidOperationException){bad=true;}Check(bad,"invalid encrypted password requires reentry");
            Check(SshAuthentication.Password(new Server{AuthMode="password"})=="","empty password supported");
            var payload=new FeedbackPayload{category="bug",title="QA title",description="QA details"};FeedbackClient.Validate(payload);
            string body=new JavaScriptSerializer().Serialize(payload);Check(!body.Contains("KeyPath")&&!body.Contains("Password")&&!body.Contains("Host")&&!body.Contains("Processes"),"feedback payload excludes credentials, devices and process history");
            payload.title=" ";bad=false;try{FeedbackClient.Validate(payload);}catch(ArgumentException){bad=true;}Check(bad,"blank feedback rejected before network");payload.title="QA title";
            using(var rsa=new RSACryptoServiceProvider(2048)) {
                rsa.PersistKeyInCsp=false;var info=new DeploymentInfo{appId="AQServerServices",feedbackEndpoint="https://example.test/aqserver/api/feedback",website="https://example.test/aqserver/",expiresUtc=DateTime.UtcNow.AddDays(1).ToString("o")};
                var json=new JavaScriptSerializer();byte[] bytes=Encoding.UTF8.GetBytes(json.Serialize(info));
                var envelope=new ReleaseEnvelope{payload=Convert.ToBase64String(bytes),signature=Convert.ToBase64String(rsa.SignData(bytes,CryptoConfig.MapNameToOID("SHA256")))};
                Check(FeedbackClient.VerifyDiscovery(Encoding.UTF8.GetBytes(json.Serialize(envelope)),rsa.ToXmlString(false)).feedbackEndpoint==info.feedbackEndpoint,"signed feedback endpoint discovery");
                bytes[20]^=1;envelope.payload=Convert.ToBase64String(bytes);bad=false;try{FeedbackClient.VerifyDiscovery(Encoding.UTF8.GetBytes(json.Serialize(envelope)),rsa.ToXmlString(false));}catch(InvalidDataException){bad=true;}Check(bad,"tampered feedback discovery rejected");
                info.expiresUtc=DateTime.UtcNow.AddDays(-1).ToString("o");bytes=Encoding.UTF8.GetBytes(json.Serialize(info));envelope.payload=Convert.ToBase64String(bytes);envelope.signature=Convert.ToBase64String(rsa.SignData(bytes,CryptoConfig.MapNameToOID("SHA256")));bad=false;try{FeedbackClient.VerifyDiscovery(Encoding.UTF8.GetBytes(json.Serialize(envelope)),rsa.ToXmlString(false));}catch(InvalidDataException){bad=true;}Check(bad,"expired signed feedback service rejected");
            }
            using(var endpoint=new FeedbackEndpoint(false))Check(FeedbackClient.SubmitAt(endpoint.Url,payload,CancellationToken.None).GetAwaiter().GetResult()==FeedbackEndpoint.Receipt,"HTTP201 receipt accepted only after actual submission");
            using(var endpoint=new FeedbackEndpoint(true)){bad=false;try{FeedbackClient.SubmitAt(endpoint.Url,payload,CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidDataException){bad=true;}Check(bad,"unconfirmed feedback response rejected");}
            foreach(string theme in Theme.Keys) {
                Theme.Apply(theme);var settings=new Settings();settings.Servers.Add(new Server{Name="demo",Host="example.test",User="demo",AuthMode="password"});
                var editor=new SettingsWindow(settings,true);var auth=Field<ComboBox>(editor,"authentication");var password=Field<PasswordBox>(editor,"password");
                Check(auth.SelectedIndex==0&&Field<StackPanel>(editor,"passwordFields").Visibility==Visibility.Visible,"new password fields visible in "+theme);
                password.Password=Guid.NewGuid().ToString("N");Field<CheckBox>(editor,"rememberPassword").IsChecked=false;
                editor.RenderImage(Path.Combine(folder,"settings-password-"+theme+".png"),860,760,120);UiReview.Record(editor.Root,Path.Combine(folder,"settings-password-"+theme+".csv"),"settings-password");
                auth.SelectedIndex=1;Check(Field<StackPanel>(editor,"keyFields").Visibility==Visibility.Visible&&Field<StackPanel>(editor,"passwordFields").Visibility==Visibility.Collapsed,"key form switches without overlapping password in "+theme);
                auth.SelectedIndex=3;Check(Field<StackPanel>(editor,"keyFields").Visibility==Visibility.Collapsed&&Field<StackPanel>(editor,"passwordFields").Visibility==Visibility.Collapsed,"none mode requires no private key in "+theme);
                var feedback=new FeedbackWindow(null);Snapshot.Save((Grid)feedback.Content,Path.Combine(folder,"feedback-"+theme+".png"),600,690,120,null);UiReview.Record((Grid)feedback.Content,Path.Combine(folder,"feedback-"+theme+".csv"),"feedback");
                new AboutWindow(null,null).Render(Path.Combine(folder,"about-"+theme+".png"));
            }
            string fixture=Environment.GetEnvironmentVariable("AQSERVER_AUTH_FIXTURE");if(!string.IsNullOrWhiteSpace(fixture))Live(fixture);
            if(Environment.GetEnvironmentVariable("AQSERVER_FEEDBACK_LIVE")=="1"){
                string receipt=FeedbackClient.Submit(new FeedbackPayload{category="other",title="AQ Server 安装版本提交验证",description="验证此版本从 Windows 客户端提交、签名服务发现、HTTPS 入库及回执。此条为自建验证记录，可识别归档。"},CancellationToken.None).GetAwaiter().GetResult();
                Check(receipt.StartsWith("FB-"),"live public feedback stored and receipt returned");File.WriteAllText(Path.Combine(folder,"feedback-live-receipt.txt"),receipt,Encoding.UTF8);
            }
            File.WriteAllLines(Path.Combine(folder,"auth-feedback-results.txt"),Log,Encoding.UTF8);return 0;
        }catch(Exception e){File.WriteAllText(Path.Combine(folder,"auth-feedback-error.txt"),e.ToString(),Encoding.UTF8);throw;}
    }
    static void Live(string file) {
        var devices=new JavaScriptSerializer().Deserialize<Server[]>(File.ReadAllText(file,Encoding.UTF8));
        foreach(var server in devices) {
            Check(ConnectionProbe.Run(server).Success,"actual "+server.Name+" authenticated probe");
            Check(SshAuthentication.Execute(server,"printf '__AQ_AUTH_OK__'",8)=="__AQ_AUTH_OK__","actual "+server.Name+" command");
            var metrics=Remote.Collect(server);Check(string.IsNullOrEmpty(metrics.Error)&&metrics.CpuCores.Count>0,"actual "+server.Name+" metrics and multicore collection");
            using(var files=new FileConnection(server)){files.Connect(CancellationToken.None).GetAwaiter().GetResult();Check(files.List(files.Home,CancellationToken.None)!=null,"actual "+server.Name+" SFTP listing");}
            var output=new StringBuilder();var ready=new ManualResetEventSlim();var ended=new ManualResetEventSlim();
            using(var terminal=new SshTerminalSession(server,"stty echo; printf '__AQ_TERM_READY__\\n'; exec bash --noprofile --norc -i",88,28)) {
                terminal.Exited+=code=>ended.Set();
                terminal.Output=text=>{lock(output){output.Append(text);if(output.ToString().Contains("__AQ_RESIZE__28 88"))ready.Set();}return Task.FromResult(true);};
                terminal.Start();var until=DateTime.UtcNow.AddSeconds(18);while(DateTime.UtcNow<until){bool ok;lock(output)ok=output.ToString().Contains("__AQ_TERM_READY__");if(ok)break;Thread.Sleep(30);}
                terminal.Resize(88,28);terminal.Write("printf '__AQ_RESIZE__'; stty size\r");Check(ready.Wait(8000),"actual "+server.Name+" terminal input, output and resize");terminal.Write("exit 0\r");Check(ended.Wait(6000),"actual "+server.Name+" remote exit closes terminal session");
            }
        }
        var wrong=devices[0].Copy();wrong.Password=Guid.NewGuid().ToString("N");wrong.ProtectedPassword="";Check(!ConnectionProbe.Run(wrong).Success,"wrong fixture password rejected");
    }
    sealed class FeedbackEndpoint : IDisposable {
        public const string Receipt="FB-20261028-1234567890abcdef";readonly System.Net.HttpListener listener=new System.Net.HttpListener();public string Url;
        public FeedbackEndpoint(bool invalid){var socket=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);socket.Start();int port=((System.Net.IPEndPoint)socket.LocalEndpoint).Port;socket.Stop();Url="http://127.0.0.1:"+port+"/";listener.Prefixes.Add(Url);listener.Start();Task.Run(()=>{try{var c=listener.GetContext();using(var r=new StreamReader(c.Request.InputStream))r.ReadToEnd();byte[] data=Encoding.UTF8.GetBytes(invalid?"{\"status\":\"received\"}":"{\"status\":\"received\",\"id\":\""+Receipt+"\"}");c.Response.StatusCode=201;c.Response.ContentLength64=data.Length;c.Response.OutputStream.Write(data,0,data.Length);c.Response.Close();}catch(ObjectDisposedException){}catch(System.Net.HttpListenerException){}});}
        public void Dispose(){listener.Close();}
    }
}
