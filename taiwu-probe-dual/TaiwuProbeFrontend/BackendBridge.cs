using System;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TaiwuProbeFrontend
{
    /// <summary>
    /// 前端 MCP 到 GameData 后端内部 HTTP 桥。后端再把请求排入自身主线程执行。
    /// 此桥不依赖 AsyncMethodDispatcher，因此在主菜单和离开世界状态也可用。
    /// </summary>
    internal static class BackendBridge
    {
        private const string Endpoint = "http://localhost:13132/probe";
        private const int TimeoutMilliseconds = 12000;

        public static string Call(string tool, JObject arguments)
        {
            try
            {
                var requestBody = new JObject
                {
                    ["tool"] = tool,
                    ["arguments"] = arguments
                };
                byte[] bytes = Encoding.UTF8.GetBytes(requestBody.ToString(Formatting.None));
                var request = (HttpWebRequest)WebRequest.Create(Endpoint);
                request.Method = "POST";
                request.ContentType = "application/json; charset=utf-8";
                request.ContentLength = bytes.Length;
                request.Timeout = TimeoutMilliseconds;
                request.ReadWriteTimeout = TimeoutMilliseconds;
                using (Stream stream = request.GetRequestStream())
                    stream.Write(bytes, 0, bytes.Length);
                using var response = (HttpWebResponse)request.GetResponse();
                using var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
                JObject result = JObject.Parse(reader.ReadToEnd());
                return result["text"]?.Value<string>() ?? "<后端内部桥响应缺少 text>";
            }
            catch (WebException ex)
            {
                return "<无法连接后端内部桥 localhost:13132: " + ex.Message + ">";
            }
            catch (Exception ex)
            {
                return "<后端内部桥异常: " + ex.Message + ">";
            }
        }
    }
}
