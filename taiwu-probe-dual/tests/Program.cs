using System.Reflection;
using Newtonsoft.Json.Linq;

static JObject Dispatch(MethodInfo dispatch, string body)
{
    string json = (string)dispatch.Invoke(null, new object[] { body })!;
    return JObject.Parse(json);
}

Assembly assembly = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "TaiwuProbeFrontend.dll"));
Type jsonRpc = assembly.GetType("TaiwuProbeFrontend.JsonRpc", throwOnError: true)!;
MethodInfo dispatch = jsonRpc.GetMethod("Dispatch", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;

JObject initialize = Dispatch(dispatch,
    "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{}}");
if (initialize["result"]?["serverInfo"]?["version"]?.Value<string>() != "0.5.0")
    throw new Exception("initialize 没有返回 0.5.0");

JObject listed = Dispatch(dispatch,
    "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\",\"params\":{}}");
JArray tools = (JArray?)listed["result"]?["tools"] ?? throw new Exception("tools/list 缺少 tools");
string[] required =
{
    "taiwu_ping",
    "taiwu_eval",
    "taiwu_ui_snapshot",
    "taiwu_ui_click",
    "taiwu_ui_fill",
    "taiwu_ui_hover",
    "taiwu_ui_toggle",
    "taiwu_ui_scroll",
    "taiwu_ui_wait",
    "taiwu_ui_describe",
    "taiwu_ui_screenshot",
    "taiwu_hotload_invoke",
    "taiwu_frontend_log",
    "taiwu_ui_scenario",
    "taiwu_backend_csharp"
};
foreach (string name in required)
{
    JObject tool = tools.OfType<JObject>().SingleOrDefault(x => x["name"]?.Value<string>() == name)
        ?? throw new Exception("缺少工具：" + name);
    if (tool["description"]?.Type != JTokenType.String || tool["inputSchema"]?["type"]?.Value<string>() != "object")
        throw new Exception("工具 schema 无效：" + name);
}

JObject ping = Dispatch(dispatch,
    "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{\"name\":\"taiwu_ping\",\"arguments\":{}}}");
if (!ping["result"]?["content"]?.Any(x => x?["text"]?.Value<string>()?.StartsWith("pong ") == true) == true)
    throw new Exception("旧 taiwu_ping 返回不兼容");

JObject describeWithoutGameLoop = Dispatch(dispatch,
    "{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"tools/call\",\"params\":{\"name\":\"taiwu_ui_describe\",\"arguments\":{\"selector\":{\"name\":\"Missing\"}}}}");
if (describeWithoutGameLoop["result"]?["isError"]?.Value<bool>() != true ||
    describeWithoutGameLoop["result"]?["structuredContent"]?["errorCode"]?.Value<string>() != "main_thread_unavailable")
    throw new Exception("结构化工具错误契约无效: " + describeWithoutGameLoop.ToString());

JObject logMark = Dispatch(dispatch,
    "{\"jsonrpc\":\"2.0\",\"id\":5,\"method\":\"tools/call\",\"params\":{\"name\":\"taiwu_frontend_log\",\"arguments\":{\"action\":\"mark\"}}}");
if (logMark["result"]?["structuredContent"]?["success"]?.Value<bool>() != true ||
    logMark["result"]?["structuredContent"]?["cursor"] == null)
    throw new Exception("前端日志 cursor 契约无效");

Console.WriteLine($"TaiwuProbe protocol contracts passed ({tools.Count} tools, server 0.5.0).");
