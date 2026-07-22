using System;
using Newtonsoft.Json.Linq;

namespace TaiwuProbeFrontend
{
    /// <summary>
    /// 新工具统一的 MCP 返回格式。文本供人阅读，structuredContent 供代理稳定消费，
    /// 图片作为标准 MCP image content 返回，不再把 JSON 或 PNG 塞进说明文字。
    /// </summary>
    internal static class McpToolResults
    {
        internal static JObject Text(string text) => new JObject
        {
            ["content"] = new JArray(ContentText(text))
        };

        internal static JObject Success(string summary, JObject data, byte[]? png = null)
        {
            data["success"] = true;
            var content = new JArray(ContentText(summary));
            if (png != null)
            {
                content.Add(new JObject
                {
                    ["type"] = "image",
                    ["data"] = Convert.ToBase64String(png),
                    ["mimeType"] = "image/png"
                });
            }
            return new JObject
            {
                ["content"] = content,
                ["structuredContent"] = data
            };
        }

        internal static JObject Error(string code, string message, JObject? details = null)
        {
            var data = details ?? new JObject();
            data["success"] = false;
            data["errorCode"] = code;
            data["message"] = message;
            return new JObject
            {
                ["content"] = new JArray(ContentText(message)),
                ["structuredContent"] = data,
                ["isError"] = true
            };
        }

        internal static bool IsError(JObject response) => response["isError"]?.Value<bool>() == true;

        private static JObject ContentText(string text) => new JObject
        {
            ["type"] = "text",
            ["text"] = text
        };
    }
}
