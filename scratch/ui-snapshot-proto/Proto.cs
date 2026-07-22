using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UiSnapshotProto
{
    /// <summary>
    /// 热加载原型：Agent 向 UI snapshot。验证窗口识别、角色推断、ref 分配与文本格式。
    /// 结果写文件（hotload 的 returnValue 在 MCP 客户端不可见）。
    /// </summary>
    public static class Proto
    {
        private static readonly Dictionary<int, GameObject> Refs = new Dictionary<int, GameObject>();
        private static readonly Dictionary<int, string> RefPaths = new Dictionary<int, string>();
        private static readonly HashSet<int> ConsumedLabels = new HashSet<int>();
        private static List<string> LastSignature = new List<string>();
        private static string LastScope = null;
        private static List<string> CurrentSig = new List<string>();

        // ---------- 动作入口（hotload 同步调用；diff 通过下一次 Snapshot 自动附带） ----------

        public static string Click(int refId, string savePath)
            => WriteResult(savePath, () => PointerClick(ResolveRef(refId)));

        public static string Fill(int refId, string text, string savePath)
            => WriteResult(savePath, () =>
            {
                GameObject go = ResolveRef(refId);
                TMP_InputField input = go.GetComponent<TMP_InputField>() ?? go.GetComponentInParent<TMP_InputField>();
                if (input == null) return "FAIL: 目标不是 input";
                input.text = text;
                input.onEndEdit?.Invoke(text);
                return "filled \"" + text + "\" -> " + RefPaths[refId];
            });

        public static string SetToggle(int refId, bool on, string savePath)
            => WriteResult(savePath, () =>
            {
                GameObject go = ResolveRef(refId);
                Toggle toggle = go.GetComponent<Toggle>() ?? go.GetComponentInParent<Toggle>();
                if (toggle == null) return "FAIL: 目标不是 toggle";
                if (toggle.isOn == on) return "已是 " + (on ? "on" : "off") + "，未改动";
                toggle.isOn = on;
                return "toggle -> " + (toggle.isOn ? "on" : "off") + "  " + RefPaths[refId];
            });

        public static string Hover(int refId, string savePath)
            => WriteResult(savePath, () =>
            {
                GameObject go = ResolveRef(refId);
                // 优先走游戏原生 TooltipInvoker.ShowTips()（指针模拟会被每帧的真实鼠标轮询刷掉）
                Component invoker = FindComponentByName(go, "TooltipInvoker");
                if (invoker != null)
                {
                    var show = invoker.GetType().GetMethod("ShowTips");
                    if (show != null)
                    {
                        object ret = show.Invoke(invoker, null);
                        return $"hover(ShowTips={ret}) -> {RefPaths[refId]}";
                    }
                }
                if (EventSystem.current == null) return "FAIL: 无 EventSystem 且无 TooltipInvoker";
                var data = new PointerEventData(EventSystem.current);
                ExecuteEvents.ExecuteHierarchy(go, data, ExecuteEvents.pointerEnterHandler);
                return "hover(pointerEnter) -> " + RefPaths[refId];
            });

        private static Component FindComponentByName(GameObject go, string typeName)
        {
            Component c = go.GetComponent(typeName);
            if (c != null) return c;
            c = go.GetComponentInChildren(System.Type.GetType(typeName) ?? FindType(typeName), false);
            if (c != null) return c;
            Transform p = go.transform.parent;
            while (p != null)
            {
                c = p.GetComponent(typeName);
                if (c != null) return c;
                p = p.parent;
            }
            return null;
        }

        private static System.Type FindType(string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetTypes().FirstOrDefault(x => x.Name == typeName);
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>读取 LayerTips 下当前 tooltip 的全部文本。</summary>
        public static string ReadTips(string savePath)
            => WriteResult(savePath, () =>
            {
                GameObject tips = GameObject.Find("Camera_UIRoot/Canvas/LayerTips");
                if (tips == null) return "(无 LayerTips)";
                var texts = tips.GetComponentsInChildren<TMP_Text>(false)
                    .Select(t => StripRichText(t.text.Trim()))
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
                return texts.Count > 0 ? string.Join("\n", texts) : "(tooltip 为空)";
            });

        public static string Scroll(int refId, float delta, string savePath)
            => WriteResult(savePath, () =>
            {
                GameObject go = ResolveRef(refId);
                ScrollRect scroll = go.GetComponent<ScrollRect>() ?? go.GetComponentInParent<ScrollRect>();
                if (scroll == null) return "FAIL: 目标及父级没有 ScrollRect";
                float before = scroll.verticalNormalizedPosition;
                scroll.verticalNormalizedPosition = Mathf.Clamp01(before + delta);
                return $"scroll {before:0.##} -> {scroll.verticalNormalizedPosition:0.##}  " + RefPaths[refId];
            });

        private static string WriteResult(string savePath, Func<string> action)
        {
            string result;
            try { result = action(); }
            catch (Exception ex) { result = "EXCEPTION: " + ex.GetType().Name + ": " + ex.Message; }
            File.WriteAllText(savePath, result);
            return result.Length > 100 ? result.Substring(0, 100) : result;
        }

        private static GameObject ResolveRef(int refId)
        {
            if (!Refs.TryGetValue(refId, out GameObject go) || go == null)
                throw new InvalidOperationException($"@e{refId} 已失效（对象不存在），请重新 snapshot");
            string current = FullPath(go);
            if (current != RefPaths[refId])
                throw new InvalidOperationException($"@e{refId} 已失效（路径变化: {RefPaths[refId]} -> {current}），请重新 snapshot");
            if (!go.activeInHierarchy)
                throw new InvalidOperationException($"@e{refId} 目标已隐藏，请重新 snapshot");
            return go;
        }

        private static string PointerClick(GameObject target)
        {
            if (EventSystem.current == null) return "FAIL: 无 EventSystem";
            RectTransform rect = target.GetComponent<RectTransform>();
            if (rect == null) return "FAIL: 目标没有 RectTransform";
            Canvas canvas = target.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
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
                string cover = hits.Count > 0 ? hits[0].gameObject.name : "(无命中)";
                return $"FAIL: 目标被遮挡（最上层命中: {cover}）";
            }
            eventData.pointerPressRaycast = chosen;
            ExecuteEvents.ExecuteHierarchy(chosen.gameObject, eventData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(chosen.gameObject, eventData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(chosen.gameObject, eventData, ExecuteEvents.pointerClickHandler);
            return "clicked " + FullPath(target);
        }

        private static readonly HashSet<string> IgnoredLayers = new HashSet<string>
        {
            "LayerTips", "LayerGlow", "LayerSpecial", "LayerCursor",
            "MaskBack", "MaskTop", "MaskBottom", "MaskLeft", "MaskRight", "SharedUIMask"
        };

        public static string Snapshot(string savePath, string scope = "", int maxLines = 200)
        {
            var sb = new StringBuilder();
            try { Build(sb, scope ?? "", maxLines <= 0 ? 200 : maxLines); }
            catch (Exception ex) { sb.AppendLine("SNAPSHOT ERROR: " + ex); }
            File.WriteAllText(savePath, sb.ToString());
            int lines = sb.ToString().Split('\n').Length;
            return "written " + lines + " lines to " + savePath;
        }

        private static void Build(StringBuilder sb, string scope, int maxLines)
        {
            Refs.Clear();
            RefPaths.Clear();
            ConsumedLabels.Clear();
            int nextRef = 1;

            GameObject uiRoot = GameObject.Find("Camera_UIRoot/Canvas");
            if (uiRoot == null) { sb.AppendLine("未找到 Camera_UIRoot/Canvas"); return; }

            // 收集层与窗口
            var layers = new List<(string name, int order, List<GameObject> windows)>();
            foreach (Transform layer in uiRoot.transform)
            {
                if (!layer.gameObject.activeInHierarchy) continue;
                var windows = new List<GameObject>();
                foreach (Transform child in layer)
                    if (child.gameObject.activeInHierarchy) windows.Add(child.gameObject);
                int order = 0;
                Canvas c = layer.GetComponent<Canvas>();
                if (c != null) order = c.sortingOrder;
                layers.Add((layer.name, order, windows));
            }
            layers.Sort((a, b) => b.order.CompareTo(a.order));

            // 场景（LayerBack 内容）
            var back = layers.FirstOrDefault(l => l.name == "LayerBack");
            string scene = back.windows.Count > 0 ? string.Join(",", back.windows.Select(w => w.name)) : "(空)";
            sb.AppendLine("场景: " + scene);

            // 候选交互层（排除 tips/cursor/mask）；窗口须有交互或文本内容才算数
            var interactiveLayers = layers
                .Where(l => !IgnoredLayers.Contains(l.name) && l.name != "LayerBack")
                .Select(l => (l.name, l.order, windows: l.windows.Where(IsSignificant).ToList()))
                .Where(l => l.windows.Count > 0)
                .ToList();

            // 顶层窗口
            var top = interactiveLayers.FirstOrDefault();
            if (top.windows != null && top.windows.Count > 0)
                sb.AppendLine($"顶层: [{top.name}] " + string.Join(", ", top.windows.Select(Title)));
            else
                sb.AppendLine("顶层: (无窗口)");

            // 其他层摘要
            foreach (var l in interactiveLayers.Skip(1))
                sb.AppendLine($"其他: [{l.name}] " + string.Join(", ", l.windows.Select(Title)));

            // 地图装饰：LayerBack 子树内的嵌套 Canvas（世界地图上的商户/蛐蛐等图标）
            if (back.windows != null && back.windows.Count > 0)
            {
                int decoCanvases = 0, decoNodes = 0;
                foreach (var w in back.windows)
                {
                    Canvas self = w.GetComponent<Canvas>();
                    decoCanvases += w.GetComponentsInChildren<Canvas>(false).Length - (self != null ? 1 : 0);
                    decoNodes += CountNodes(w.transform) - 1;
                }
                if (decoCanvases > 0)
                    sb.AppendLine($"地图装饰: {decoCanvases} 个子 Canvas / {decoNodes} 节点（不展开）");
            }
            sb.AppendLine("");

            // 树：按 scope 决定要展开的层
            int headerLen = sb.Length;
            CurrentSig = new List<string>();
            List<(string name, int order, List<GameObject> windows)> expand;
            if (scope == "all") expand = interactiveLayers;
            else if (!string.IsNullOrEmpty(scope))
                expand = interactiveLayers.Where(l => l.name == scope || l.windows.Any(w => w.name == scope || Title(w) == scope)).ToList();
            else if (top.windows != null) expand = new List<(string, int, List<GameObject>)> { top };
            else expand = new List<(string, int, List<GameObject>)>();

            int lineCount = sb.ToString().Split('\n').Length;
            bool truncated = false;
            foreach (var l in expand)
            {
                foreach (var w in l.windows)
                {
                    EmitWindow(sb, w, ref nextRef, maxLines, ref lineCount, ref truncated);
                    if (truncated) break;
                }
                if (truncated) break;
            }
            if (truncated) sb.AppendLine($"...(超出 {maxLines} 行已截断，用 scope 参数缩小范围)");

            // 与上一次 snapshot 的多重集 diff，插到头部之后（scope 变了则不对比）
            if (LastSignature.Count > 0 && LastScope == scope)
            {
                var added = MultisetDiff(CurrentSig, LastSignature);
                var removed = MultisetDiff(LastSignature, CurrentSig);
                if (added.Count > 0 || removed.Count > 0)
                {
                    var diff = new StringBuilder();
                    diff.AppendLine($"--- 变化: +{added.Count} -{removed.Count} ---");
                    foreach (string s in added.Take(10)) diff.AppendLine("+ " + s);
                    if (added.Count > 10) diff.AppendLine($"+ ...({added.Count - 10} 更多)");
                    foreach (string s in removed.Take(10)) diff.AppendLine("- " + s);
                    if (removed.Count > 10) diff.AppendLine($"- ...({removed.Count - 10} 更多)");
                    diff.AppendLine("");
                    sb.Insert(headerLen, diff.ToString());
                }
            }
            LastSignature = new List<string>(CurrentSig);
            LastScope = scope;
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
            string title = Title(window);
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
                string role, state, text;
                bool interactive;
                Infer(go, out role, out state, out text, out interactive);
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
                // slider/input 的内部结构（handle、placeholder）是噪音，不展开
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

        /// <summary>角色推断：按组件类型名与基类判断，不依赖游戏程序集。</summary>
        private static void Infer(GameObject go, out string role, out string state, out string text, out bool interactive)
        {
            role = null; state = null; text = null; interactive = false;
            Component[] comps = go.GetComponents<Component>();

            TMP_InputField input = comps.OfType<TMP_InputField>().FirstOrDefault();
            if (input != null)
            {
                role = "input"; interactive = true;
                text = input.text;
                if (!input.interactable) state = "disabled";
                foreach (TMP_Text t in go.GetComponentsInChildren<TMP_Text>(false))
                    ConsumedLabels.Add(t.GetInstanceID());
                return;
            }
            Toggle toggle = comps.OfType<Toggle>().FirstOrDefault();
            if (toggle != null)
            {
                role = "toggle"; interactive = true;
                state = toggle.isOn ? "on" : "off";
                if (!toggle.interactable) state += ",disabled";
                text = LabelOf(go);
                return;
            }
            Slider slider = comps.OfType<Slider>().FirstOrDefault();
            if (slider != null)
            {
                role = "slider"; interactive = true;
                state = $"{slider.value:0.##}/{slider.maxValue:0.##}";
                text = LabelOf(go);
                return;
            }
            Button button = comps.OfType<Button>().FirstOrDefault();
            if (button != null)
            {
                role = "button"; interactive = true;
                if (!button.interactable) state = "disabled";
                text = LabelOf(go);
                return;
            }
            ScrollRect scroll = comps.OfType<ScrollRect>().FirstOrDefault();
            if (scroll != null)
            {
                role = "scroll"; interactive = true;
                return;
            }
            Component rangeSlider = comps.FirstOrDefault(c => c != null && c.GetType().Name == "RangeSlider");
            if (rangeSlider != null)
            {
                role = "rangeslider"; interactive = true;
                state = ReadNumberProps(rangeSlider);
                return;
            }
            // 游戏自定义交互组件（未挂 Unity 标准 Selectable 的）
            bool hasCustom = comps.Any(c => c != null && (
                c.GetType().Name == "UIInteractionBehaviour" ||
                c.GetType().Name == "PointerTrigger"));
            if (hasCustom)
            {
                role = "clickable"; interactive = true;
                text = LabelOf(go);
                return;
            }
            TMP_Text tmp = comps.OfType<TMP_Text>().FirstOrDefault();
            if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text) && !ConsumedLabels.Contains(tmp.GetInstanceID()))
            {
                text = StripRichText(tmp.text.Trim());
                return;
            }
        }

        private static string LabelOf(GameObject go)
        {
            TMP_Text t = go.GetComponentInChildren<TMP_Text>();
            if (t == null) return null;
            ConsumedLabels.Add(t.GetInstanceID());
            return StripRichText(t.text.Trim());
        }

        private static string Title(GameObject window)
        {
            Transform holder = FindDeep(window.transform, n => n.Contains("Title"));
            TMP_Text t = holder != null ? holder.GetComponentInChildren<TMP_Text>() : null;
            string title = t != null ? StripRichText(t.text.Trim()) : "";
            return string.IsNullOrWhiteSpace(title) ? window.name : title;
        }

        private static string StripRichText(string s)
            => System.Text.RegularExpressions.Regex.Replace(s, "<[^>]+>", "");

        /// <summary>窗口是否值得展示：含有交互组件或文本的后代节点。</summary>
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

        private static Transform FindDeep(Transform root, Func<string, bool> nameMatch)
        {
            foreach (Transform child in root)
            {
                if (nameMatch(child.name)) return child;
                Transform found = FindDeep(child, nameMatch);
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

        private static string FullPath(GameObject go)
        {
            var parts = new List<string>();
            Transform cur = go.transform;
            while (cur != null) { parts.Add(cur.name); cur = cur.parent; }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static string ReadNumberProps(Component c)
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

        /// <summary>滚动实验：对 CScrollRect 调 ScrollTo(Vector2, 0)。</summary>
        public static string ScrollCS(string savePath, string goName, float dy)
            => WriteResult(savePath, () =>
            {
                GameObject go = GameObject.Find(goName);
                if (go == null) return "FAIL: 未找到 " + goName;
                Type type = FindType("CScrollRect");
                Component comp = go.GetComponent(type);
                if (comp == null) return "FAIL: 无 CScrollRect";
                var viewport = (RectTransform)type.GetProperty("Viewport").GetValue(comp, null);
                var content = (RectTransform)type.GetProperty("Content").GetValue(comp, null);
                Vector2 cur = content.anchoredPosition;
                var m = type.GetMethod("ScrollTo", new[] { typeof(Vector2), typeof(float) });
                m.Invoke(comp, new object[] { cur + new Vector2(0, dy), 0f });
                return $"anchored {cur} -> {content.anchoredPosition}, viewportH={viewport.rect.height}, contentH={content.rect.height}";
            });

        /// <summary>调试用：转储指定类型名的 public 成员签名。</summary>
        public static string DumpType(string savePath, string typeName)
        {
            Type found = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                found = asm.GetTypes().FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);
                if (found != null) break;
            }
            var sb = new StringBuilder();
            if (found == null) sb.AppendLine("type not found: " + typeName);
            else
            {
                sb.AppendLine(found.FullName + " : " + (found.BaseType != null ? found.BaseType.FullName : ""));
                foreach (var p in found.GetProperties()) sb.AppendLine("prop " + p.PropertyType.Name + " " + p.Name);
                foreach (var f in found.GetFields()) sb.AppendLine("field " + f.FieldType.Name + " " + f.Name);
                foreach (var m in found.GetMethods().Where(m => !m.IsSpecialName))
                    sb.AppendLine("method " + m.ReturnType.Name + " " + m.Name + "(" +
                        string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")");
            }
            File.WriteAllText(savePath, sb.ToString());
            return "written";
        }

        private static string Truncate(string s, int max)
            => s.Length <= max ? s : s.Substring(0, max) + "…";
    }
}
