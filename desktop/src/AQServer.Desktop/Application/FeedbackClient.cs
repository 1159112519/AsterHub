using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

public sealed class FeedbackPayload {
    public int schemaVersion=1;
    public string appVersion=AppUpdates.DisplayVersion,platform="Windows",category,title,description,contact,environment;
}
public sealed class DeploymentInfo {
    public string appId,feedbackEndpoint,website,expiresUtc;
}
public static class FeedbackClient {
    public const string Discovery="https://raw.githubusercontent.com/1159112519/aq-server/main/website/public/releases/services.json";
    public static DeploymentInfo VerifyDiscovery(byte[] bytes,string key) {
        if(bytes==null||bytes.Length>16384)throw new InvalidDataException("服务信息格式不正确。");
        var json=new JavaScriptSerializer();
        try {
            var envelope=json.Deserialize<ReleaseEnvelope>(Encoding.UTF8.GetString(bytes));
            byte[] payload=Convert.FromBase64String(envelope.payload),signature=Convert.FromBase64String(envelope.signature);
            using(var rsa=new RSACryptoServiceProvider()){rsa.PersistKeyInCsp=false;rsa.FromXmlString(key);if(!rsa.VerifyData(payload,CryptoConfig.MapNameToOID("SHA256"),signature))throw new InvalidDataException("反馈服务签名校验失败。");}
            var info=json.Deserialize<DeploymentInfo>(Encoding.UTF8.GetString(payload));Uri uri,site;DateTime expiry;
            if(info==null||info.appId!="AQServerServices"||!Uri.TryCreate(info.feedbackEndpoint,UriKind.Absolute,out uri)||uri.Scheme!="https"||!string.IsNullOrEmpty(uri.UserInfo)||uri.AbsolutePath!="/aqserver/api/feedback"||!string.IsNullOrEmpty(uri.Query)||!string.IsNullOrEmpty(uri.Fragment)||!Uri.TryCreate(info.website,UriKind.Absolute,out site)||site.Scheme!="https"||site.Host!=uri.Host||!string.IsNullOrEmpty(site.UserInfo)||!DateTime.TryParse(info.expiresUtc,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind,out expiry)||expiry.ToUniversalTime()<=DateTime.UtcNow)throw new InvalidDataException("反馈服务信息已过期或无效，请稍后重试。");
            return info;
        }catch(InvalidDataException){throw;}catch{throw new InvalidDataException("反馈服务信息格式不正确。");}
    }
    static async Task<byte[]> Request(string address,byte[] body,CancellationToken token) {
        ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
        var request=(HttpWebRequest)WebRequest.Create(address);request.Timeout=10000;request.ReadWriteTimeout=10000;request.UserAgent="AQServer/"+AppUpdates.DisplayVersion;
        request.CachePolicy=new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
        request.AllowAutoRedirect=body==null;
        using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)) {
            deadline.CancelAfter(15000);
            using(deadline.Token.Register(()=>request.Abort())) {
                try {
                    if(body!=null){request.Method="POST";request.ContentType="application/json; charset=utf-8";request.Headers["X-AQServer-Feedback"]="1";request.ContentLength=body.Length;using(var stream=await request.GetRequestStreamAsync().ConfigureAwait(false))await stream.WriteAsync(body,0,body.Length,deadline.Token).ConfigureAwait(false);}
                    using(var response=(HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))using(var stream=response.GetResponseStream())using(var data=new MemoryStream()){
                        if(body!=null&&response.StatusCode!=HttpStatusCode.Created)throw new IOException("服务尚未确认保存，请重试。");
                        var buffer=new byte[4096];int n;while((n=await stream.ReadAsync(buffer,0,buffer.Length,deadline.Token).ConfigureAwait(false))>0){if(data.Length+n>16384)throw new InvalidDataException("服务响应过大。");data.Write(buffer,0,n);}return data.ToArray();
                    }
                }catch(Exception){if(token.IsCancellationRequested)throw new OperationCanceledException(token);if(deadline.IsCancellationRequested)throw new TimeoutException("提交超时，内容已保留，请稍后重试。");throw;}
            }
        }
    }
    public static async Task<DeploymentInfo> Services(CancellationToken token){return VerifyDiscovery(await Request(Discovery,null,token).ConfigureAwait(false),UpdatePublicKey.Xml);}
    public static async Task<string> Submit(FeedbackPayload payload,CancellationToken token) {
        Validate(payload);var info=await Services(token).ConfigureAwait(false);return await SubmitAt(info.feedbackEndpoint,payload,token).ConfigureAwait(false);
    }
    internal static async Task<string> SubmitAt(string endpoint,FeedbackPayload payload,CancellationToken token) {
        Validate(payload);var json=new JavaScriptSerializer();byte[] bytes=Encoding.UTF8.GetBytes(json.Serialize(payload));
        var receipt=json.Deserialize<Dictionary<string,string>>(Encoding.UTF8.GetString(await Request(endpoint,bytes,token).ConfigureAwait(false)));
        if(receipt==null||!receipt.ContainsKey("status")||receipt["status"]!="received"||!receipt.ContainsKey("id")||!System.Text.RegularExpressions.Regex.IsMatch(receipt["id"],@"^FB-\d{8}-[a-f0-9]{16}$"))throw new InvalidDataException("服务未返回有效回执，内容已保留。");
        return receipt["id"];
    }
    public static void Validate(FeedbackPayload p){if(p==null||string.IsNullOrWhiteSpace(p.title)||string.IsNullOrWhiteSpace(p.description))throw new ArgumentException("请填写问题标题和详情。");if(p.title.Length>100||p.description.Length>8000||(p.contact??"").Length>200||(p.environment??"").Length>300)throw new ArgumentException("反馈内容超过长度限制。");if(Array.IndexOf(new[]{"bug","ui","suggestion","other"},p.category)<0)throw new ArgumentException("请选择反馈分类。");}
    public static string FriendlyError(Exception error) {
        if(error is OperationCanceledException)return "已取消，填写的内容已保留。";
        if(error is ArgumentException||error is InvalidDataException||error is TimeoutException)return error.Message;
        var web=error as WebException;var response=web==null?null:web.Response as HttpWebResponse;
        if(response!=null&&(int)response.StatusCode==429)return "提交过于频繁，请稍后重试。内容已保留。";
        return "暂时无法提交，请检查网络后重试。填写的内容已保留。";
    }
}
