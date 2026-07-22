using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TaiwuProbeFrontend
{
    /// <summary>
    /// Agent 向 UI 工具集：snapshot → @eN ref → action。
    /// 核心信息一律放在文本 content（MCP 客户端唯一可见通道），structuredContent 仅作冗余。
    /// 设计依据：scratch/ui-snapshot-proto/NOTES.md（热加载原型实测结论）。
    /// </summary>
    internal static class UiSnapshotTools
    {
        #region 状态

        private static readonly Dictionary<int, GameObject> Refs = new Dictionary<int, GameObject>();
        private static readonly Dictionary<int, string> RefPaths = new Dictionary<int, string>();
        private static readonly HashSet<int> ConsumedLabels = new HashSet<int>();
        private static List<string> LastSignature = new List<string>();
        private static string? LastScope;
        private static List<string> CurrentSig = new List<string>();
        private static readonly StringBuilder TreeBuffer = new StringBuilder();

        private static readonly HashSet<string> IgnoredLayers = new HashSet<string>
        {
            "LayerTips", "LayerGlow", "LayerSpecial", "LayerCursor",
            "MaskBack", "MaskTop", "MaskBottom", "MaskLeft", "MaskRight", "SharedUIMask"
        };

        #endregion

        #region 工具入口

        internal static JObject SnapshotHandle(JObject args)
        {
            return JsonRpc.RunStructuredOnMainThread(() =>
            {
                string scope = args["scope"]?.Value<string>() ?? "";
                int maxLines = Math.Max(20, Math.Min(1000, args["max_lines"]?.Value<int>() ?? 200));
                string text = BuildSnapshot(scope, maxLines);
                var data = new JObject
                {
                    ["text"] = text,
                    ["refCount"] = Refs.Count,
                    ["refs"] = new JObject(RefPaths.Select(kv => new JProperty("e" + kv.Key, kv.Value)))
                };
                return McpToolResults.Success(text, data);
            });
        }

        internal static JObject DescribeHandle(JObject args)
        {
            return JsonRpc.RunStructuredOnMainThread(() =>
            {
                GameObject? go;
                JObject? err = TryResolveTarget(args, out go);
                if (err != null) return err;
                int depth = Math.Max(0, Math.Min(3, args["depth"]?.Value<int>() ?? 0));
                JObject info = UiInspectTools.InspectObject(go!, depth);
                // 组件附程序集名（反编译定位用）
                info["components"] = new JArray(go!.GetComponents<Component>()
                    .Where(c => c != null)
                    .Select(c => c.GetType().FullName + ", " + c.GetType().Assembly.GetName().Name));
                // 父链
                var parents = new JArray();
                Transform? p = go.transform.parent;
                while (p != null)
                {
                    parents.Add(new JObject
                    {
                        ["name"] = p.name,
                        ["components"] = new JArray(p.GetComponents<Component>()
                            .Where(c => c != null && !(c is Transform))
                            .Select(c => c.GetType().Name))
                    });
                    p = p.parent;
                }
                info["parents"] = parents;
                string text = info.ToString(Newtonsoft.Json.Formatting.Indented);
                return McpToolResults.Success(text, new JObject { ["target"] = info });
            });
        }

        internal static JObject ClickHandle(JObject args) => RunAction(args, go => PointerClick(go, args));

        internal static JObject FillHandle(JObject args) => RunAction(args, go =>
        {
            string text = args["text"]?.Value<string>() ?? "";
            TMP_InputField? input = go.GetComponent<TMP_InputField>() ?? go.GetComponentInParent<TMP_InputField>();
            if (input == null) return "FAIL: 目标不是 input";
            input.text = text;
            input.onEndEdit?.Invoke(text);
            return $"filled \"{text}\"";
        });

        internal static JObject ToggleHandle(JObject args) => RunAction(args, go =>
        {
            string want = (args["state"]?.Value<string>() ?? "").Trim().ToLowerInvariant();
            if (want != "on" && want != "off") return "FAIL: state 必须是 on 或 off";
            Toggle? toggle = go.GetComponent<Toggle>() ?? go.GetComponentInParent<Toggle>();
            if (toggle == null) return "FAIL: 目标不是 toggle";
            bool on = want == "on";
            if (toggle.isOn == on) return "已是 " + want + "，未改动";
            toggle.isOn = on;
            return "toggle -> " + (toggle.isOn ? "on" : "off");
        });

        internal static JObject ScrollHandle(JObject args) => RunAction(args, go =>
        {
            float delta = args["delta"]?.Value<float>() ?? 0.25f;
            string dir = (args["direction"]?.Value<string>() ?? "down").Trim().ToLowerInvariant();
            float sign = dir == "up" ? 1f : -1f;
            ScrollRect? scroll = go.GetComponent<ScrollRect>() ?? go.GetComponentInParent<ScrollRect>();
            if (scroll != null)
            {
                float before = scroll.verticalNormalizedPosition;
                scroll.verticalNormalizedPosition = Mathf.Clamp01(before + sign * delta);
                return $"scroll {before:0.##} -> {scroll.verticalNormalizedPosition:0.##}";
            }
            // CScrollRect 等自定义滚动：ScrollTo(Vector2 targetAnchorPosition, float duration)，
            // 向下滚 = content.anchoredPosition.y 增加，内部自动 clamp。（实测见 NOTES.md）
            Component? custom = FindScrollLike(go);
            if (custom == null) return "FAIL: 目标及父级没有 ScrollRect";
            Type t = custom.GetType();
            RectTransform? viewport = t.GetProperty("Viewport")?.GetValue(custom, null) as RectTransform;
            RectTransform? content = t.GetProperty("Content")?.GetValue(custom, null) as RectTransform;
            var scrollTo = t.GetMethod("ScrollTo", new[] { typeof(Vector2), typeof(float) });
            if (viewport == null || content == null || scrollTo == null)
                return "FAIL: " + t.Name + " 缺少 Viewport/Content/ScrollTo(Vector2,float)";
            Vector2 from = content.anchoredPosition;
            float page = viewport.rect.height * delta;
            // 注意方向：ScrollRect.normalizedPosition 是 up=+，anchoredPosition 是 down=+
            Vector2 targetPos = from + new Vector2(0, -sign * page);
            scrollTo.Invoke(custom, new object[] { targetPos, 0f });
            return $"scroll 请求 y {from.y:0} -> {targetPos.y:0}（阻尼滚动，超出范围会被游戏 clamp，实际位置以 settle 后为准）";
        });

        private static Component? FindScrollLike(GameObject go)
        {
            Transform? t = go.transform;
            while (t != null)
            {
                Component? c = t.GetComponents<Component>().FirstOrDefault(x =>
                    x != null && x.GetType().Name.EndsWith("ScrollRect", StringComparison.Ordinal));
                if (c != null) return c;
                t = t.parent;
            }
            return null;
        }

        internal static JObject HoverHandle(JObject args)
        {
            JObject? response = null;
            var done = new ManualResetEventSlim(false);
            if (!MainThreadRunner.RunCoroutine(RunHover(args, value => { response = value; done.Set(); })))
                return McpToolResults.Error("main_thread_unavailable", "Unity 主线程执行器尚未初始化。");
            int timeout = Math.Max(1000, Math.Min(60000, args["timeout_ms"]?.Value<int>() ?? 10000));
            if (!done.Wait(timeout))
                return McpToolResults.Error("ui_action_timeout", $"hover 在 {timeout}ms 内未完成。");
            done.Dispose();
            return response ?? McpToolResults.Error("ui_action_no_result", "hover 没有返回结果。");
        }

        private static IEnumerator RunHover(JObject args, Action<JObject> complete)
        {
            JObject? err = TryResolveTarget(args, out GameObject? go);
            string result;
            if (err != null) { complete(err); yield break; }
            result = Hover(go!);
            int waitFrames = Math.Max(0, Math.Min(120, args["wait_frames"]?.Value<int>() ?? 2));
            for (int i = 0; i < waitFrames; i++) yield return null;
            string tips = ReadTips();
            string text = result + "\n--- tooltip ---\n" + tips;
            complete(McpToolResults.Success(text, new JObject { ["result"] = result, ["tips"] = tips }));
        }

        internal static JObject WaitHandle(JObject args)
        {
            JObject? response = null;
            var done = new ManualResetEventSlim(false);
            if (!MainThreadRunner.RunCoroutine(RunWait(args, value => { response = value; done.Set(); })))
                return McpToolResults.Error("main_thread_unavailable", "Unity 主线程执行器尚未初始化。");
            int timeout = Math.Max(1000, Math.Min(60000, args["timeout_ms"]?.Value<int>() ?? 10000));
            // 协程内部按 timeout 退出，这里多等 1 秒余量
            if (!done.Wait(timeout + 1000))
                return McpToolResults.Error("ui_wait_timeout", $"wait 在 {timeout}ms 内未完成。");
            done.Dispose();
            return response ?? McpToolResults.Error("ui_wait_no_result", "wait 没有返回结果。");
        }

        private static IEnumerator RunWait(JObject args, Action<JObject> complete)
        {
            string text = args["text"]?.Value<string>() ?? "";
            bool disappear = (args["state"]?.Value<string>() ?? "appear") == "disappear";
            int ms = args["ms"]?.Value<int>() ?? 0;
            int timeoutMs = Math.Max(200, Math.Min(60000, args["timeout_ms"]?.Value<int>() ?? 10000));
            JObject? selector = args["selector"] as JObject;

            if (ms > 0 && string.IsNullOrEmpty(text) && selector == null)
            {
                float waitUntil = Time.unscaledTime + ms / 1000f;
                while (Time.unscaledTime < waitUntil) yield return null;
                complete(McpToolResults.Success($"已等待 {ms}ms。", new JObject { ["ms"] = ms }));
                yield break;
            }

            float deadline = Time.unscaledTime + timeoutMs / 1000f;
            int frames = 0;
            while (Time.unscaledTime < deadline)
            {
                bool found;
                if (!string.IsNullOrEmpty(text))
                    found = FindTextAnywhere(text);
                else
                    found = UiLocator.Find(selector, false, 1).Count > 0;
                if (found != disappear)
                {
                    string what = !string.IsNullOrEmpty(text) ? $"文字 \"{text}\"" : "selector";
                    complete(McpToolResults.Success(
                        $"{what} 已{(disappear ? "消失" : "出现")}（{frames} 帧）。",
                        new JObject { ["frames"] = frames, ["state"] = disappear ? "disappear" : "appear" }));
                    yield break;
                }
                frames++;
                yield return null;
            }
            complete(McpToolResults.Error("ui_wait_timeout",
                $"等待超时（{timeoutMs}ms）：{(string.IsNullOrEmpty(text) ? "selector" : "文字 \"" + text + "\"")} 未{(disappear ? "消失" : "出现")}。"));
        }

        #endregion

        #region 动作执行框架（含 after-diff）

        /// <summary>协程执行动作：解析目标 → 执行 → 等帧 → 附同 scope diff。</summary>
        private static JObject RunAction(JObject args, Func<GameObject, string> action)
        {
            JObject? response = null;
            var done = new ManualResetEventSlim(false);
            if (!MainThreadRunner.RunCoroutine(Run(args, value => { response = value; done.Set(); })))
                return McpToolResults.Error("main_thread_unavailable", "Unity 主线程执行器尚未初始化。");
            int timeout = Math.Max(1000, Math.Min(60000, args["timeout_ms"]?.Value<int>() ?? 10000));
            if (!done.Wait(timeout))
                return McpToolResults.Error("ui_action_timeout", $"UI 动作在 {timeout}ms 内未完成。");
            done.Dispose();
            return response ?? McpToolResults.Error("ui_action_no_result", "UI 动作没有返回结果。");

            IEnumerator Run(JObject a, Action<JObject> complete)
            {
                JObject? err = TryResolveTarget(a, out GameObject? go);
                if (err != null) { complete(err); yield break; }
                string path = FullPath(go!);
                string result;
                try { result = action(go!); }
                catch (Exception ex)
                {
                    complete(McpToolResults.Error("ui_action_failed", ex.GetType().Name + ": " + ex.Message,
                        new JObject { ["path"] = path }));
                    yield break;
                }
                int waitFrames = Math.Max(0, Math.Min(120, a["wait_frames"]?.Value<int>() ?? 3));
                for (int i = 0; i < waitFrames; i++) yield return null;
                string diff = ComputeDiff();
                string text = result + "\n" + path + (diff.Length > 0 ? "\n" + diff : "\n(无可见变化)");
                complete(McpToolResults.Success(text,
                    new JObject { ["result"] = result, ["path"] = path, ["diff"] = diff }));
            }
        }

        /// <summary>目标解析：ref 优先，selector 兜底（逃生舱）。</summary>
        private static JObject? TryResolveTarget(JObject args, out GameObject? go)
        {
            go = null;
            JToken? refToken = args["ref"];
            if (refToken != null)
            {
                int refId = ParseRef(refToken.Value<string>() ?? refToken.ToString());
                if (refId < 0)
                    return McpToolResults.Error("invalid_ref", "ref 格式应为 \"@eN\" 或整数。",
                        new JObject { ["ref"] = refToken.ToString() });
                return ResolveRef(refId, out go);
            }
            if (args["selector"] is JObject selector)
            {
                List<GameObject> matches = UiLocator.Find(selector, false, 20);
                if (matches.Count == 0)
                    return McpToolResults.Error("ui_not_found", "selector 没有匹配到节点。",
                        new JObject { ["selector"] = selector.DeepClone() });
                if (matches.Count != 1)
                    return McpToolResults.Error("ui_ambiguous", $"selector 匹配到 {matches.Count} 个节点。",
                        new JObject { ["paths"] = new JArray(matches.Select(FullPath)) });
                go = matches[0];
                return null;
            }
            return McpToolResults.Error("missing_target", "必须提供 ref（@eN）或 selector。");
        }

        private static int ParseRef(string s)
        {
            s = s.Trim();
            if (s.StartsWith("@e", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
            else if (s.StartsWith("e", StringComparison.OrdinalIgnoreCase)) s = s.Substring(1);
            return int.TryParse(s, out int id) ? id : -1;
        }

        private static JObject? ResolveRef(int refId, out GameObject? go)
        {
            go = null;
            if (!Refs.TryGetValue(refId, out GameObject? cached) || cached == null)
                return McpToolResults.Error("stale_ref",
                    $"@e{refId} 已失效（对象不存在或未经过 snapshot），请重新调用 taiwu_ui_snapshot。");
            string current = FullPath(cached);
            if (current != RefPaths[refId])
                return McpToolResults.Error("stale_ref",
                    $"@e{refId} 已失效（路径变化: {RefPaths[refId]} -> {current}），请重新调用 taiwu_ui_snapshot。");
            if (!cached.activeInHierarchy)
                return McpToolResults.Error("stale_ref",
                    $"@e{refId} 目标已隐藏（{current}），请重新调用 taiwu_ui_snapshot。");
            go = cached;
            return null;
        }

        #endregion

        #region snapshot 引擎

        internal static string BuildSnapshot(string scope, int maxLines)
        {
            Refs.Clear();
            RefPaths.Clear();
            ConsumedLabels.Clear();
            int nextRef = 1;
            var sb = new StringBuilder();

            GameObject? uiRoot = GameObject.Find("Camera_UIRoot/Canvas");
            if (uiRoot == null) return "未找到 Camera_UIRoot/Canvas（不在游戏主界面？）";

            List<(string name, int order, List<GameObject> windows)> interactiveLayers =
                CollectInteractiveLayers(uiRoot, out List<GameObject> backWindows);

            string scene = backWindows.Count > 0
                ? string.Join(",", backWindows.Select(w => w.name)) : "(空)";
            sb.AppendLine("场景: " + scene);

            var top = interactiveLayers.FirstOrDefault();
            sb.AppendLine(top.windows != null && top.windows.Count > 0
                ? $"顶层: [{top.name}] " + string.Join(", ", top.windows.Select(TitleOf))
                : "顶层: (无窗口)");
            foreach (var l in interactiveLayers.Skip(1))
                sb.AppendLine($"其他: [{l.name}] " + string.Join(", ", l.windows.Select(TitleOf)));

            if (backWindows.Count > 0)
            {
                int decoCanvases = 0, decoNodes = 0;
                foreach (GameObject w in backWindows)
                {
                    Canvas? self = w.GetComponent<Canvas>();
                    decoCanvases += w.GetComponentsInChildren<Canvas>(false).Length - (self != null ? 1 : 0);
                    decoNodes += CountNodes(w.transform) - 1;
                }
                if (decoCanvases > 0)
                    sb.AppendLine($"地图装饰: {decoCanvases} 个子 Canvas / {decoNodes} 节点（不展开）");
            }
            sb.AppendLine("");

            int headerLen = sb.Length;
            CurrentSig = new List<string>();
            AddHeaderSignature(interactiveLayers);

            List<(string name, int order, List<GameObject> windows)> expand = ResolveScope(interactiveLayers, scope);

            int lineCount = sb.ToString().Split('\n').Length;
            bool truncated = false;
            foreach (var l in expand)
            {
                foreach (GameObject w in l.windows)
                {
                    EmitWindow(sb, w, ref nextRef, maxLines, ref lineCount, ref truncated);
                    if (truncated) break;
                }
                if (truncated) break;
            }
            if (truncated) sb.AppendLine($"...(超出 {maxLines} 行已截断，用 scope 参数缩小范围)");

            if (LastSignature.Count > 0 && LastScope == scope)
            {
                List<string> added = MultisetDiff(CurrentSig, LastSignature);
                List<string> removed = MultisetDiff(LastSignature, CurrentSig);
                if (added.Count > 0 || removed.Count > 0)
                    sb.Insert(headerLen, FormatDiff(added, removed));
            }
            LastSignature = new List<string>(CurrentSig);
            LastScope = scope;
            return sb.ToString();
        }

        /// <summary>收集排序后的交互层（sortingOrder 降序、过滤无意义窗口），供 snapshot 与 diff 共用。</summary>
        private static List<(string name, int order, List<GameObject> windows)> CollectInteractiveLayers(
            GameObject uiRoot, out List<GameObject> backWindows)
        {
            var layers = new List<(string name, int order, List<GameObject> windows)>();
            foreach (Transform layer in uiRoot.transform)
            {
                if (!layer.gameObject.activeInHierarchy) continue;
                var windows = new List<GameObject>();
                foreach (Transform child in layer)
                    if (child.gameObject.activeInHierarchy) windows.Add(child.gameObject);
                Canvas? c = layer.GetComponent<Canvas>();
                layers.Add((layer.name, c != null ? c.sortingOrder : 0, windows));
            }
            layers.Sort((a, b) => b.order.CompareTo(a.order));
            backWindows = layers.FirstOrDefault(l => l.name == "LayerBack").windows ?? new List<GameObject>();
            return layers
                .Where(l => !IgnoredLayers.Contains(l.name) && l.name != "LayerBack")
                .Select(l => (l.name, l.order, windows: l.windows.Where(IsSignificant).ToList()))
                .Where(l => l.windows.Count > 0)
                .ToList();
        }

        private static List<(string name, int order, List<GameObject> windows)> ResolveScope(
            List<(string name, int order, List<GameObject> windows)> interactiveLayers, string scope)
        {
            if (scope == "all") return interactiveLayers;
            if (!string.IsNullOrEmpty(scope))
                return interactiveLayers
                    .Where(l => l.name == scope || l.windows.Any(w => w.name == scope || TitleOf(w) == scope))
                    .ToList();
            return interactiveLayers.Take(1).ToList();
        }

        /// <summary>层/窗口行进入签名：任何 scope 下窗口开关都能反映在 diff 里。</summary>
        private static void AddHeaderSignature(List<(string name, int order, List<GameObject> windows)> interactiveLayers)
        {
            foreach (var l in interactiveLayers)
                CurrentSig.Add("layer|" + l.name + "|" + string.Join(",", l.windows.Select(TitleOf)));
        }

        /// <summary>动作后调用：对当前 scope 重建签名并输出 diff（同时更新基线）。</summary>
        private static string ComputeDiff()
        {
            if (LastScope == null || LastSignature.Count == 0) return "";
            GameObject? uiRoot = GameObject.Find("Camera_UIRoot/Canvas");
            if (uiRoot == null) return "";
            var interactiveLayers = CollectInteractiveLayers(uiRoot, out _);
            CurrentSig = new List<string>();
            AddHeaderSignature(interactiveLayers);
            foreach (var l in ResolveScope(interactiveLayers, LastScope))
                foreach (GameObject w in l.windows)
                    CollectSignature(w);
            List<string> added = MultisetDiff(CurrentSig, LastSignature);
            List<string> removed = MultisetDiff(LastSignature, CurrentSig);
            LastSignature = new List<string>(CurrentSig);
            return FormatDiff(added, removed);
        }

        private static void CollectSignature(GameObject window)
        {
            CurrentSig.Add("window|" + window.name + "|" + TitleOf(window));
            CollectWalk(window.transform);
        }

        private static void CollectWalk(Transform t)
        {
            foreach (Transform child in t)
            {
                GameObject go = child.gameObject;
                if (!go.activeInHierarchy) continue;
                Infer(go, out string? role, out string? state, out string? text, out _);
                if (role != null || text != null)
                    CurrentSig.Add((role ?? "text") + "|" + (text ?? "") + "|" + (state ?? ""));
                if (role == "slider" || role == "rangeslider" || role == "input") continue;
                CollectWalk(child);
            }
        }

        private static string FormatDiff(List<string> added, List<string> removed)
        {
            if (added.Count == 0 && removed.Count == 0) return "";
            var diff = new StringBuilder();
            diff.AppendLine($"--- 变化: +{added.Count} -{removed.Count} ---");
            foreach (string s in added.Take(10)) diff.AppendLine("+ " + s);
            if (added.Count > 10) diff.AppendLine($"+ ...({added.Count - 10} 更多)");
            foreach (string s in removed.Take(10)) diff.AppendLine("- " + s);
            if (removed.Count > 10) diff.AppendLine($"- ...({removed.Count - 10} 更多)");
            diff.AppendLine("");
            return diff.ToString();
        }

        private static List<string> MultisetDiff(List<string> a, List<string> b)
        {
            var remaining = new List<string>(b);
            var result = new List<string>();
            foreach (string s in a)
                if (!remaining.Remove(s)) result.Add(s);
            return result;
        }

        private static void EmitWindow(StringBuilder sb, GameObject window, ref int nextRef, int maxLines, ref int lineCount, ref bool truncated)
        {
            string title = TitleOf(window);
            CurrentSig.Add("window|" + window.name + "|" + title);
            Emit(sb, 0, $"{window.name}  「{title}」", maxLines, ref lineCount, ref truncated);
            Walk(sb, window.transform, 1, ref nextRef, maxLines, ref lineCount, ref truncated);
        }

        private static void Walk(StringBuilder sb, Transform t, int depth, ref int nextRef, int maxLines, ref int lineCount, ref bool truncated)
        {
            if (truncated) return;
            foreach (Transform child in t)
            {
                GameObject go = child.gameObject;
                if (!go.activeInHierarchy) continue;
                Infer(go, out string? role, out string? state, out string? text, out bool interactive);
                int childDepth = depth;
                if (role != null || text != null)
                {
                    string refTag = "";
                    if (interactive)
                    {
                        refTag = $"@e{nextRef} ";
                        Refs[nextRef] = go;
                        RefPaths[nextRef] = FullPath(go);
                        nextRef++;
                    }
                    string line = refTag + (role ?? "text");
                    if (text != null) line += $" \"{Truncate(text, 30)}\"";
                    if (state != null) line += $" [{state}]";
                    if (!interactive && role != null) line += " (不可交互)";
                    CurrentSig.Add((role ?? "text") + "|" + (text ?? "") + "|" + (state ?? ""));
                    Emit(sb, depth, line + "  <" + go.name + ">", maxLines, ref lineCount, ref truncated);
                    childDepth = depth + 1;
                }
                if (role == "slider" || role == "rangeslider" || role == "input") continue;
                Walk(sb, child, childDepth, ref nextRef, maxLines, ref lineCount, ref truncated);
            }
        }

        private static void Emit(StringBuilder sb, int depth, string content, int maxLines, ref int lineCount, ref bool truncated)
        {
            if (truncated) return;
            if (lineCount >= maxLines) { truncated = true; return; }
            sb.AppendLine(new string(' ', depth * 2) + content);
            lineCount++;
        }

        /// <summary>角色推断：按组件类型名与基类判断，不依赖游戏程序集类型。</summary>
        private static void Infer(GameObject go, out string? role, out string? state, out string? text, out bool interactive)
        {
            role = null; state = null; text = null; interactive = false;
            Component[] comps = go.GetComponents<Component>();

            TMP_InputField? input = comps.OfType<TMP_InputField>().FirstOrDefault();
            if (input != null)
            {
                role = "input"; interactive = true;
                text = input.text;
                if (!input.interactable) state = "disabled";
                foreach (TMP_Text t in go.GetComponentsInChildren<TMP_Text>(false))
                    ConsumedLabels.Add(t.GetInstanceID());
                return;
            }
            Toggle? toggle = comps.OfType<Toggle>().FirstOrDefault();
            if (toggle != null)
            {
                role = "toggle"; interactive = true;
                state = toggle.isOn ? "on" : "off";
                if (!toggle.interactable) state += ",disabled";
                text = LabelOf(go);
                return;
            }
            Slider? slider = comps.OfType<Slider>().FirstOrDefault();
            if (slider != null)
            {
                role = "slider"; interactive = true;
                state = $"{slider.value:0.##}/{slider.maxValue:0.##}";
                text = LabelOf(go);
                return;
            }
            Button? button = comps.OfType<Button>().FirstOrDefault();
            if (button != null)
            {
                role = "button"; interactive = true;
                if (!button.interactable) state = "disabled";
                text = LabelOf(go);
                return;
            }
            ScrollRect? scroll = comps.OfType<ScrollRect>().FirstOrDefault();
            if (scroll != null)
            {
                role = "scroll"; interactive = true;
                return;
            }
            // CScrollRect 等游戏自定义滚动组件（不继承 Unity ScrollRect）
            if (comps.Any(c => c != null && c.GetType().Name.EndsWith("ScrollRect", StringComparison.Ordinal)))
            {
                role = "scroll"; interactive = true;
                return;
            }
            Component? rangeSlider = comps.FirstOrDefault(c => c != null && c.GetType().Name == "RangeSlider");
            if (rangeSlider != null)
            {
                role = "rangeslider"; interactive = true;
                state = ReadNumberProps(rangeSlider);
                return;
            }
            bool hasCustom = comps.Any(c => c != null && (
                c.GetType().Name == "UIInteractionBehaviour" ||
                c.GetType().Name == "PointerTrigger"));
            if (hasCustom)
            {
                role = "clickable"; interactive = true;
                text = LabelOf(go);
                return;
            }
            TMP_Text? tmp = comps.OfType<TMP_Text>().FirstOrDefault();
            if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text) && !ConsumedLabels.Contains(tmp.GetInstanceID()))
                text = StripRichText(tmp.text.Trim());
        }

        private static string? LabelOf(GameObject go)
        {
            TMP_Text? t = go.GetComponentInChildren<TMP_Text>();
            if (t == null) return null;
            ConsumedLabels.Add(t.GetInstanceID());
            return StripRichText(t.text.Trim());
        }

        private static string TitleOf(GameObject window)
        {
            Transform? holder = FindDeep(window.transform, n => n.Contains("Title"));
            TMP_Text? t = holder != null ? holder.GetComponentInChildren<TMP_Text>() : null;
            string title = t != null ? StripRichText(t.text.Trim()) : "";
            return string.IsNullOrWhiteSpace(title) ? window.name : title;
        }

        private static string StripRichText(string s)
            => System.Text.RegularExpressions.Regex.Replace(s, "<[^>]+>", "");

        private static bool IsSignificant(GameObject window)
        {
            foreach (Component c in window.GetComponentsInChildren<Component>(false))
            {
                if (c == null) continue;
                if (c is TMP_Text tmp) { if (!string.IsNullOrWhiteSpace(tmp.text)) return true; continue; }
                if (c is Selectable || c is ScrollRect) return true;
                string n = c.GetType().Name;
                if (n == "UIInteractionBehaviour" || n == "PointerTrigger") return true;
            }
            return false;
        }

        private static string? ReadNumberProps(Component c)
        {
            var parts = new List<string>();
            foreach (var p in c.GetType().GetProperties())
            {
                if (!p.CanRead) continue;
                if (p.PropertyType != typeof(float) && p.PropertyType != typeof(int)) continue;
                if (p.Name.IndexOf("value", StringComparison.OrdinalIgnoreCase) < 0 &&
                    p.Name.IndexOf("min", StringComparison.OrdinalIgnoreCase) < 0 &&
                    p.Name.IndexOf("max", StringComparison.OrdinalIgnoreCase) < 0) continue;
                try { parts.Add($"{p.Name}={p.GetValue(c, null)}"); } catch { }
            }
            return parts.Count > 0 ? string.Join(",", parts) : null;
        }

        private static Transform? FindDeep(Transform root, Func<string, bool> nameMatch)
        {
            foreach (Transform child in root)
            {
                if (nameMatch(child.name)) return child;
                Transform? found = FindDeep(child, nameMatch);
                if (found != null) return found;
            }
            return null;
        }

        private static int CountNodes(Transform t)
        {
            int n = 1;
            foreach (Transform c in t) n += CountNodes(c);
            return n;
        }

        internal static string FullPath(GameObject go)
        {
            var parts = new List<string>();
            Transform? cur = go.transform;
            while (cur != null) { parts.Add(cur.name); cur = cur.parent; }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static string Truncate(string s, int max)
            => s.Length <= max ? s : s.Substring(0, max) + "…";

        #endregion

        #region 动作原语

        private static string PointerClick(GameObject target, JObject args)
        {
            string mode = args["mode"]?.Value<string>()?.Trim().ToLowerInvariant() ?? "pointer";
            if (mode == "invoke")
            {
                Button? button = target.GetComponent<Button>() ?? target.GetComponentInParent<Button>();
                if (button != null) { button.onClick.Invoke(); return "已调用 Button.onClick"; }
                Toggle? toggle = target.GetComponent<Toggle>() ?? target.GetComponentInParent<Toggle>();
                if (toggle != null) { toggle.isOn = !toggle.isOn; return "已翻转 Toggle -> " + (toggle.isOn ? "on" : "off"); }
                return "FAIL: 目标及父级没有 Button 或 Toggle；请用 pointer 模式";
            }
            if (EventSystem.current == null) return "FAIL: 无 EventSystem";
            RectTransform? rect = target.GetComponent<RectTransform>();
            if (rect == null) return "FAIL: 目标没有 RectTransform";
            Canvas? canvas = target.GetComponentInParent<Canvas>();
            Camera? camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            Vector2 position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            var eventData = new PointerEventData(EventSystem.current) { position = position };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, hits);
            RaycastResult chosen = hits.FirstOrDefault(hit =>
                hit.gameObject == target ||
                hit.gameObject.transform.IsChildOf(target.transform) ||
                target.transform.IsChildOf(hit.gameObject.transform));
            if (chosen.gameObject == null)
            {
                string cover = hits.Count > 0 ? FullPath(hits[0].gameObject) : "(无命中)";
                return $"FAIL: 目标被遮挡（最上层命中: {cover}）";
            }
            eventData.pointerPressRaycast = chosen;
            ExecuteEvents.ExecuteHierarchy(chosen.gameObject, eventData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(chosen.gameObject, eventData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(chosen.gameObject, eventData, ExecuteEvents.pointerClickHandler);
            return "clicked";
        }

        private static string Hover(GameObject go)
        {
            Component? invoker = FindComponentByName(go, "TooltipInvoker");
            if (invoker != null)
            {
                var show = invoker.GetType().GetMethod("ShowTips");
                if (show != null)
                {
                    object? ret = show.Invoke(invoker, null);
                    return $"hover(ShowTips={ret})";
                }
            }
            if (EventSystem.current == null) return "FAIL: 无 EventSystem 且无 TooltipInvoker";
            var data = new PointerEventData(EventSystem.current);
            ExecuteEvents.ExecuteHierarchy(go, data, ExecuteEvents.pointerEnterHandler);
            return "hover(pointerEnter)";
        }

        private static string ReadTips()
        {
            GameObject? tips = GameObject.Find("Camera_UIRoot/Canvas/LayerTips");
            if (tips == null) return "(无 LayerTips)";
            List<string> texts = tips.GetComponentsInChildren<TMP_Text>(false)
                .Select(t => StripRichText(t.text.Trim()))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
            return texts.Count > 0 ? string.Join("\n", texts) : "(tooltip 为空)";
        }

        private static Component? FindComponentByName(GameObject go, string typeName)
        {
            Type? type = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { type = asm.GetTypes().FirstOrDefault(x => x.Name == typeName); } catch { }
                if (type != null) break;
            }
            if (type == null) return null;
            Component? c = go.GetComponent(type);
            if (c != null) return c;
            c = go.GetComponentInChildren(type, false);
            if (c != null) return c;
            Transform? p = go.transform.parent;
            while (p != null)
            {
                c = p.GetComponent(type);
                if (c != null) return c;
                p = p.parent;
            }
            return null;
        }

        private static bool FindTextAnywhere(string text)
        {
            foreach (TMP_Text t in UnityEngine.Object.FindObjectsOfType<TMP_Text>())
                if (t.gameObject.activeInHierarchy && t.text != null &&
                    StripRichText(t.text).IndexOf(text, StringComparison.Ordinal) >= 0)
                    return true;
            return false;
        }

        #endregion
    }
}
