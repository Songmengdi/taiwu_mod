using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace TaiwuProbeFrontend
{
    internal sealed class FrontendLogEntry
    {
        internal long Sequence;
        internal DateTime Timestamp;
        internal string Message = "";
        internal string StackTrace = "";
        internal LogType Type;
    }

    /// <summary>
    /// 前端日志环形缓冲。cursor 是稳定 seam：验证开始时 mark，结束时只查询新增异常，
    /// 不再依赖 Player.log 文件位置或旧异常文本过滤。
    /// </summary>
    internal static class FrontendLogBuffer
    {
        private const int Capacity = 2000;
        private static readonly object Gate = new object();
        private static readonly List<FrontendLogEntry> Entries = new List<FrontendLogEntry>();
        private static long _sequence;
        private static bool _started;

        internal static long CurrentCursor => Interlocked.Read(ref _sequence);

        internal static void Start()
        {
            lock (Gate)
            {
                if (_started) return;
                Application.logMessageReceivedThreaded += Record;
                _started = true;
            }
        }

        internal static void Stop()
        {
            lock (Gate)
            {
                if (!_started) return;
                Application.logMessageReceivedThreaded -= Record;
                _started = false;
            }
        }

        internal static JObject Handle(JObject args)
        {
            string action = args["action"]?.Value<string>()?.Trim().ToLowerInvariant() ?? "tail";
            if (action == "mark")
                return McpToolResults.Success("已创建前端日志游标。",
                    new JObject { ["cursor"] = CurrentCursor });
            if (action != "tail")
                return McpToolResults.Error("invalid_log_action", "action 必须是 mark 或 tail。");

            long since = args["since"]?.Value<long>() ?? Math.Max(0, CurrentCursor - 100);
            int limit = Math.Max(1, Math.Min(1000, args["limit"]?.Value<int>() ?? 100));
            string? contains = args["contains"]?.Value<string>();
            HashSet<string>? levels = (args["levels"] as JArray)?
                .Values<string>().Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            List<FrontendLogEntry> snapshot;
            lock (Gate)
            {
                snapshot = Entries.Where(e => e.Sequence > since)
                    .Where(e => levels == null || levels.Count == 0 || levels.Contains(e.Type.ToString()))
                    .Where(e => string.IsNullOrEmpty(contains) ||
                        e.Message.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        e.StackTrace.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0)
                    .TakeLast(limit).ToList();
            }
            var data = new JObject
            {
                ["since"] = since,
                ["cursor"] = CurrentCursor,
                ["count"] = snapshot.Count,
                ["entries"] = new JArray(snapshot.Select(ToJson))
            };
            return McpToolResults.Success($"读取到 {snapshot.Count} 条前端日志。", data);
        }

        internal static List<FrontendLogEntry> Since(long cursor, params LogType[] types)
        {
            var allowed = new HashSet<LogType>(types);
            lock (Gate) return Entries.Where(e => e.Sequence > cursor && allowed.Contains(e.Type)).ToList();
        }

        private static void Record(string condition, string stackTrace, LogType type)
        {
            var entry = new FrontendLogEntry
            {
                Sequence = Interlocked.Increment(ref _sequence),
                Timestamp = DateTime.UtcNow,
                Message = condition ?? "",
                StackTrace = stackTrace ?? "",
                Type = type
            };
            lock (Gate)
            {
                Entries.Add(entry);
                if (Entries.Count > Capacity) Entries.RemoveRange(0, Entries.Count - Capacity);
            }
        }

        private static JObject ToJson(FrontendLogEntry entry) => new JObject
        {
            ["sequence"] = entry.Sequence,
            ["timestampUtc"] = entry.Timestamp.ToString("O"),
            ["level"] = entry.Type.ToString(),
            ["message"] = entry.Message,
            ["stackTrace"] = entry.StackTrace
        };
    }

    internal static class HotLoadTools
    {
        internal static JObject Handle(JObject args)
        {
            JObject? response = null;
            var done = new ManualResetEventSlim(false);
            if (!MainThreadRunner.RunCoroutine(Run(args, value =>
                {
                    response = value;
                    done.Set();
                })))
                return McpToolResults.Error("main_thread_unavailable", "Unity 主线程执行器尚未初始化。");
            int timeout = Math.Max(1000, Math.Min(60000, args["timeout_ms"]?.Value<int>() ?? 10000));
            if (!done.Wait(timeout))
                return McpToolResults.Error("hotload_timeout", $"热加载调用在 {timeout}ms 内未完成。");
            done.Dispose();
            return response ?? McpToolResults.Error("hotload_no_result", "热加载没有返回结果。");
        }

        private static IEnumerator Run(JObject args, Action<JObject> complete)
        {
            JObject response;
            try { response = InvokeOnMainThread(args); }
            catch (Exception ex) { response = McpToolResults.Error("hotload_failed", FormatException(ex)); }
            int waitFrames = Math.Max(0, Math.Min(120, args["wait_frames"]?.Value<int>() ?? 0));
            for (int i = 0; i < waitFrames; i++) yield return null;
            complete(response);
        }

        private static JObject InvokeOnMainThread(JObject args)
        {
            string path = args["assembly_path"]?.Value<string>() ?? "";
            string typeName = args["type"]?.Value<string>() ?? "";
            string methodName = args["method"]?.Value<string>() ?? "";
            bool allowExisting = args["allow_existing"]?.Value<bool>() ?? false;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return McpToolResults.Error("assembly_not_found", "assembly_path 不存在：" + path);
            if (string.IsNullOrWhiteSpace(typeName) || string.IsNullOrWhiteSpace(methodName))
                return McpToolResults.Error("invalid_entrypoint", "type 和 method 不能为空。");

            byte[] bytes = File.ReadAllBytes(path);
            string hash;
            using (SHA256 sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
            AssemblyName requestedName = AssemblyName.GetAssemblyName(path);
            Assembly? existing = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a =>
                AssemblyName.ReferenceMatchesDefinition(a.GetName(), requestedName));
            Assembly assembly;
            bool reused;
            if (existing != null)
            {
                if (!allowExisting)
                    return McpToolResults.Error("assembly_name_already_loaded",
                        $"程序集 {requestedName.Name} 已加载；热迭代请使用新 AssemblyName，或显式 allow_existing=true。",
                        new JObject { ["assemblyName"] = requestedName.FullName, ["sha256"] = hash });
                assembly = existing;
                reused = true;
            }
            else
            {
                assembly = Assembly.Load(bytes);
                reused = false;
            }

            Type? type = assembly.GetType(typeName, false);
            if (type == null)
                return McpToolResults.Error("type_not_found", $"程序集内未找到类型 {typeName}。");
            JArray supplied = args["arguments"] as JArray ?? new JArray();
            MethodInfo? method = null;
            object?[]? converted = null;
            foreach (MethodInfo candidate in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                         .Where(m => m.Name == methodName))
            {
                if (TryConvertArguments(supplied, candidate.GetParameters(), out object?[] values))
                {
                    method = candidate;
                    converted = values;
                    break;
                }
            }
            if (method == null)
                return McpToolResults.Error("method_not_found",
                    $"未找到可接受 {supplied.Count} 个参数的静态方法 {typeName}.{methodName}。");

            object? invocationResult;
            try { invocationResult = method.Invoke(null, converted); }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                return McpToolResults.Error("entrypoint_exception", FormatException(ex.InnerException));
            }
            var data = new JObject
            {
                ["assemblyName"] = assembly.FullName,
                ["assemblyPath"] = Path.GetFullPath(path),
                ["sha256"] = hash,
                ["reusedExisting"] = reused,
                ["type"] = typeName,
                ["method"] = methodName,
                ["returnValue"] = SafeToken(invocationResult)
            };
            return McpToolResults.Success($"已加载并调用 {typeName}.{methodName}。", data);
        }

        private static bool TryConvertArguments(JArray supplied, ParameterInfo[] parameters, out object?[] values)
        {
            values = new object?[parameters.Length];
            if (supplied.Count > parameters.Length) return false;
            if (supplied.Count < parameters.Count(p => !p.IsOptional)) return false;
            try
            {
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (i >= supplied.Count) values[i] = parameters[i].DefaultValue;
                    else values[i] = ConvertToken(supplied[i], parameters[i].ParameterType);
                }
                return true;
            }
            catch { return false; }
        }

        private static object? ConvertToken(JToken token, Type type)
        {
            if (token.Type == JTokenType.Null) return null;
            Type target = Nullable.GetUnderlyingType(type) ?? type;
            if (target.IsEnum)
            {
                if (token.Type == JTokenType.String) return Enum.Parse(target, token.Value<string>()!, true);
                return Enum.ToObject(target, token.Value<int>());
            }
            if (target == typeof(string)) return token.Value<string>();
            if (target == typeof(Guid)) return Guid.Parse(token.Value<string>()!);
            return token.ToObject(target);
        }

        private static JToken SafeToken(object? value)
        {
            if (value == null) return JValue.CreateNull();
            if (value is UnityEngine.Object unityObject)
                return new JObject { ["type"] = value.GetType().FullName, ["name"] = unityObject.name };
            try { return JToken.FromObject(value); }
            catch { return value.ToString() ?? value.GetType().FullName ?? "<value>"; }
        }

        private static string FormatException(Exception ex) => ex.GetType().Name + ": " + ex.Message;
    }

    internal static class ScreenshotTools
    {
        internal static JObject Handle(JObject args)
        {
            JObject? response = null;
            byte[]? png = null;
            var done = new ManualResetEventSlim(false);
            if (!MainThreadRunner.RunCoroutine(Capture(args, (result, bytes) =>
                {
                    response = result;
                    png = bytes;
                    done.Set();
                })))
                return McpToolResults.Error("main_thread_unavailable", "Unity 主线程执行器尚未初始化。");
            int timeout = Math.Max(1000, Math.Min(60000, args["timeout_ms"]?.Value<int>() ?? 15000));
            if (!done.Wait(timeout))
                return McpToolResults.Error("screenshot_timeout", $"截图在 {timeout}ms 内未完成。");
            done.Dispose();
            if (response == null || png == null) return response ?? McpToolResults.Error("screenshot_no_result", "截图没有返回结果。");

            string? savePath = args["save_path"]?.Value<string>();
            if (!string.IsNullOrWhiteSpace(savePath))
            {
                try
                {
                    string fullPath = Path.GetFullPath(savePath);
                    string? directory = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                    File.WriteAllBytes(fullPath, png);
                    response["savePath"] = fullPath;
                }
                catch (Exception ex)
                {
                    return McpToolResults.Error("screenshot_save_failed", ex.Message, response);
                }
            }
            return McpToolResults.Success("游戏截图已捕获。", response, png);
        }

        private static IEnumerator Capture(JObject args, Action<JObject, byte[]?> complete)
        {
            int waitFrames = Math.Max(0, Math.Min(120, args["wait_frames"]?.Value<int>() ?? 2));
            for (int i = 0; i < waitFrames; i++) yield return null;
            yield return new WaitForEndOfFrame();
            Texture2D? full = null;
            Texture2D? output = null;
            try
            {
                full = ScreenCapture.CaptureScreenshotAsTexture();
                if (full == null)
                {
                    complete(McpToolResults.Error("screenshot_capture_failed", "ScreenCapture 返回 null。"), null);
                    yield break;
                }
                string target = args["target"]?.Value<string>()?.Trim().ToLowerInvariant() ?? "game_client";
                JObject crop = new JObject { ["x"] = 0, ["y"] = 0, ["width"] = full.width, ["height"] = full.height };
                output = full;
                if (target == "element")
                {
                    List<GameObject> matches = UiLocator.Find(args["selector"] as JObject, false, 10);
                    if (matches.Count != 1 || !(matches[0].transform is RectTransform rect))
                    {
                        complete(McpToolResults.Error("screenshot_element_not_unique",
                            $"element 截图要求 selector 唯一匹配 RectTransform，当前匹配 {matches.Count} 个。"), null);
                        yield break;
                    }
                    if (!TryScreenRect(rect, full.width, full.height, out int x, out int y, out int width, out int height))
                    {
                        complete(McpToolResults.Error("screenshot_element_outside", "目标元素不在屏幕可见范围内。"), null);
                        yield break;
                    }
                    output = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    output.SetPixels(full.GetPixels(x, y, width, height));
                    output.Apply(false, false);
                    crop = new JObject { ["x"] = x, ["y"] = y, ["width"] = width, ["height"] = height };
                }
                else if (target != "game_client")
                {
                    complete(McpToolResults.Error("invalid_screenshot_target", "target 必须是 game_client 或 element。"), null);
                    yield break;
                }
                byte[] bytes = output.EncodeToPNG();
                var data = new JObject
                {
                    ["target"] = target,
                    ["screenWidth"] = Screen.width,
                    ["screenHeight"] = Screen.height,
                    ["imageWidth"] = output.width,
                    ["imageHeight"] = output.height,
                    ["crop"] = crop,
                    ["waitFrames"] = waitFrames,
                    ["byteLength"] = bytes.Length
                };
                complete(data, bytes);
            }
            catch (Exception ex)
            {
                complete(McpToolResults.Error("screenshot_exception", ex.GetType().Name + ": " + ex.Message), null);
            }
            finally
            {
                if (output != null && output != full) UnityEngine.Object.Destroy(output);
                if (full != null) UnityEngine.Object.Destroy(full);
            }
        }

        private static bool TryScreenRect(RectTransform rect, int textureWidth, int textureHeight,
            out int x, out int y, out int width, out int height)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Canvas? canvas = rect.GetComponentInParent<Canvas>();
            Camera? camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera ?? UIManager.Instance?.UiCamera
                : null;
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            int xMin = Math.Max(0, Math.Min(textureWidth, Mathf.FloorToInt(Math.Min(bottomLeft.x, topRight.x))));
            int yMin = Math.Max(0, Math.Min(textureHeight, Mathf.FloorToInt(Math.Min(bottomLeft.y, topRight.y))));
            int xMax = Math.Max(0, Math.Min(textureWidth, Mathf.CeilToInt(Math.Max(bottomLeft.x, topRight.x))));
            int yMax = Math.Max(0, Math.Min(textureHeight, Mathf.CeilToInt(Math.Max(bottomLeft.y, topRight.y))));
            x = xMin;
            y = yMin;
            width = xMax - xMin;
            height = yMax - yMin;
            return width > 0 && height > 0;
        }
    }

    /// <summary>
    /// 高频热验证的深模块：外部只有一个 scenario interface，内部编排热加载、动作、
    /// 结构化断言、日志游标和截图。基础工具仍可独立使用，场景工具不复制其实现。
    /// </summary>
    internal static class UiScenarioTools
    {
        internal static JObject Handle(JObject args)
        {
            long logCursor = FrontendLogBuffer.CurrentCursor;
            bool success = true;
            var results = new JArray();
            var content = new JArray();

            if (args["assembly"] is JObject assemblyArgs)
            {
                JObject assemblyResult = HotLoadTools.Handle(assemblyArgs);
                results.Add(new JObject { ["kind"] = "assembly", ["result"] = assemblyResult["structuredContent"]?.DeepClone() });
                if (McpToolResults.IsError(assemblyResult)) success = false;
            }

            if (success && args["steps"] is JArray steps)
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    if (!(steps[i] is JObject step)) continue;
                    JObject stepResult;
                    string kind;
                    if (step["action"] is JObject action)
                    {
                        kind = "action";
                        stepResult = UiActionTools.Handle(action);
                    }
                    else if (step["assert"] is JObject assertion)
                    {
                        kind = "assert";
                        stepResult = Assert(assertion);
                    }
                    else
                    {
                        kind = "invalid";
                        stepResult = McpToolResults.Error("invalid_scenario_step", $"第 {i} 步缺少 action 或 assert。");
                    }
                    results.Add(new JObject
                    {
                        ["index"] = i,
                        ["kind"] = kind,
                        ["result"] = stepResult["structuredContent"]?.DeepClone()
                    });
                    if (McpToolResults.IsError(stepResult))
                    {
                        success = false;
                        if (args["continue_on_failure"]?.Value<bool>() != true) break;
                    }
                }
            }

            JObject? captureStructured = null;
            if (success && args["capture"] is JObject captureArgs)
            {
                JObject captureResult = ScreenshotTools.Handle(captureArgs);
                captureStructured = captureResult["structuredContent"] as JObject;
                if (captureResult["content"] is JArray captureContent)
                {
                    foreach (JToken item in captureContent)
                        if (item["type"]?.Value<string>() == "image") content.Add(item.DeepClone());
                }
                if (McpToolResults.IsError(captureResult)) success = false;
            }

            List<FrontendLogEntry> errors = FrontendLogBuffer.Since(
                logCursor, LogType.Error, LogType.Exception, LogType.Assert);
            if ((args["fail_on_new_exceptions"]?.Value<bool>() ?? true) && errors.Count > 0) success = false;

            string summary = success
                ? $"UI 场景验证通过，共执行 {results.Count} 个阶段。"
                : $"UI 场景验证失败，共执行 {results.Count} 个阶段，新增异常 {errors.Count} 条。";
            content.Insert(0, new JObject { ["type"] = "text", ["text"] = summary });
            var structured = new JObject
            {
                ["success"] = success,
                ["results"] = results,
                ["capture"] = captureStructured?.DeepClone(),
                ["logCursorStart"] = logCursor,
                ["logCursorEnd"] = FrontendLogBuffer.CurrentCursor,
                ["newExceptionCount"] = errors.Count,
                ["newExceptions"] = new JArray(errors.Select(e => new JObject
                {
                    ["sequence"] = e.Sequence,
                    ["level"] = e.Type.ToString(),
                    ["message"] = e.Message,
                    ["stackTrace"] = e.StackTrace
                }))
            };
            var response = new JObject { ["content"] = content, ["structuredContent"] = structured };
            if (!success) response["isError"] = true;
            return response;
        }

        private static JObject Assert(JObject assertion)
        {
            var inspectArgs = new JObject
            {
                ["selector"] = assertion["selector"]?.DeepClone(),
                ["require_unique"] = true,
                ["depth"] = 0
            };
            JObject inspected = UiInspectTools.Handle(inspectArgs);
            if (McpToolResults.IsError(inspected)) return inspected;
            JObject? match = inspected["structuredContent"]?["matches"]?.First as JObject;
            string property = assertion["property"]?.Value<string>() ?? "";
            JToken? actual = match?.SelectToken(property, false);
            JToken? expected = assertion["equals"];
            bool equal = TokensEqual(actual, expected);
            var data = new JObject
            {
                ["selector"] = assertion["selector"]?.DeepClone(),
                ["property"] = property,
                ["actual"] = actual?.DeepClone(),
                ["expected"] = expected?.DeepClone(),
                ["passed"] = equal
            };
            return equal
                ? McpToolResults.Success("断言通过：" + property, data)
                : McpToolResults.Error("assertion_failed", "断言失败：" + property, data);
        }

        private static bool TokensEqual(JToken? left, JToken? right)
        {
            if (left == null || right == null) return left == null && right == null;
            if ((left.Type == JTokenType.Integer || left.Type == JTokenType.Float) &&
                (right.Type == JTokenType.Integer || right.Type == JTokenType.Float))
                return Math.Abs(left.Value<double>() - right.Value<double>()) < 0.0001;
            return JToken.DeepEquals(left, right);
        }
    }
}
