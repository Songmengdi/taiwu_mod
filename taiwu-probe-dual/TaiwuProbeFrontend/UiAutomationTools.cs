using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TaiwuProbeFrontend
{
    /// <summary>
    /// 所有结构化 UI 工具共享的 selector seam。调用方只需学习
    /// path/name/text/component 四种定位方式；遍历 Canvas、去重、活跃状态过滤均隐藏在此处。
    /// </summary>
    internal static class UiLocator
    {
        internal static List<GameObject> Find(JObject? selector, bool includeInactive, int maxMatches = 50)
        {
            if (selector == null) return new List<GameObject>();
            string? path = Clean(selector["path"]?.Value<string>());
            string? name = Clean(selector["name"]?.Value<string>());
            string? text = Clean(selector["text"]?.Value<string>());
            string? component = Clean(selector["component"]?.Value<string>());
            bool exactText = selector["exact_text"]?.Value<bool>() ?? true;
            if (path == null && name == null && text == null && component == null)
                return new List<GameObject>();

            var result = new List<GameObject>();
            var seen = new HashSet<int>();
            Canvas[] canvases = includeInactive
                ? Resources.FindObjectsOfTypeAll<Canvas>()
                : UnityEngine.Object.FindObjectsOfType<Canvas>();
            foreach (Canvas canvas in canvases)
            {
                if (canvas == null || !canvas.gameObject.scene.IsValid()) continue;
                if (!includeInactive && !canvas.gameObject.activeInHierarchy) continue;
                if (canvas.transform.parent != null && canvas.transform.parent.GetComponentInParent<Canvas>() != null)
                    continue;
                Walk(canvas.transform, includeInactive, go =>
                {
                    if (result.Count >= maxMatches || !seen.Add(go.GetInstanceID())) return;
                    if (Matches(go, path, name, text, component, exactText)) result.Add(go);
                });
                if (result.Count >= maxMatches) break;
            }
            return result;
        }

        internal static string FullPath(GameObject go)
        {
            var parts = new List<string>();
            Transform? current = go.transform;
            while (current != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static void Walk(Transform root, bool includeInactive, Action<GameObject> visit)
        {
            if (includeInactive || root.gameObject.activeInHierarchy) visit(root.gameObject);
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (includeInactive || child.gameObject.activeInHierarchy) Walk(child, includeInactive, visit);
            }
        }

        private static bool Matches(
            GameObject go,
            string? path,
            string? name,
            string? text,
            string? component,
            bool exactText)
        {
            if (path != null)
            {
                string full = FullPath(go);
                string normalized = path.Trim('/');
                if (!string.Equals(full, normalized, StringComparison.Ordinal) &&
                    !full.EndsWith("/" + normalized, StringComparison.Ordinal)) return false;
            }
            if (name != null && !string.Equals(go.name, name, StringComparison.Ordinal)) return false;
            if (text != null)
            {
                TMP_Text? tmp = go.GetComponent<TMP_Text>();
                if (tmp == null) return false;
                if (exactText ? !string.Equals(tmp.text, text, StringComparison.Ordinal) :
                    tmp.text.IndexOf(text, StringComparison.Ordinal) < 0) return false;
            }
            if (component != null && !go.GetComponents<Component>().Any(c =>
                    c != null && (string.Equals(c.GetType().Name, component, StringComparison.Ordinal) ||
                                  string.Equals(c.GetType().FullName, component, StringComparison.Ordinal)))) return false;
            return true;
        }

        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    internal static class UiInspectTools
    {
        internal static JObject Handle(JObject args)
        {
            return JsonRpc.RunStructuredOnMainThread(() => InspectOnMainThread(args));
        }

        internal static JObject InspectOnMainThread(JObject args)
        {
            JObject? selector = args["selector"] as JObject;
            bool includeInactive = args["include_inactive"]?.Value<bool>() ?? false;
            bool requireUnique = args["require_unique"]?.Value<bool>() ?? false;
            int depth = Math.Max(0, Math.Min(5, args["depth"]?.Value<int>() ?? 0));
            int maxMatches = Math.Max(1, Math.Min(200, args["max_matches"]?.Value<int>() ?? 50));
            List<GameObject> matches = UiLocator.Find(selector, includeInactive, maxMatches);
            if (matches.Count == 0)
                return McpToolResults.Error("ui_not_found", "没有找到符合 selector 的 UI 节点。",
                    new JObject { ["selector"] = selector?.DeepClone() });
            if (requireUnique && matches.Count != 1)
                return McpToolResults.Error("ui_ambiguous", $"selector 匹配到 {matches.Count} 个节点，需要唯一结果。",
                    new JObject
                    {
                        ["matchCount"] = matches.Count,
                        ["paths"] = new JArray(matches.Select(UiLocator.FullPath))
                    });

            var data = new JObject
            {
                ["selector"] = selector?.DeepClone(),
                ["matchCount"] = matches.Count,
                ["matches"] = new JArray(matches.Select(go => InspectObject(go, depth)))
            };
            return McpToolResults.Success($"找到 {matches.Count} 个 UI 节点。", data);
        }

        internal static JObject InspectObject(GameObject go, int depth = 0)
        {
            var result = new JObject
            {
                ["name"] = go.name,
                ["path"] = UiLocator.FullPath(go),
                ["activeSelf"] = go.activeSelf,
                ["activeInHierarchy"] = go.activeInHierarchy,
                ["layer"] = go.layer,
                ["siblingIndex"] = go.transform.GetSiblingIndex(),
                ["childCount"] = go.transform.childCount,
                ["components"] = new JArray(go.GetComponents<Component>()
                    .Where(c => c != null).Select(c => c.GetType().FullName ?? c.GetType().Name))
            };

            if (go.transform is RectTransform rect) result["rect"] = RectInfo(rect);
            Image? image = go.GetComponent<Image>();
            if (image != null)
            {
                result["image"] = new JObject
                {
                    ["sprite"] = image.sprite != null ? image.sprite.name : null,
                    ["type"] = image.type.ToString(),
                    ["preserveAspect"] = image.preserveAspect,
                    ["raycastTarget"] = image.raycastTarget,
                    ["color"] = ColorInfo(image.color)
                };
            }
            TMP_Text? text = go.GetComponent<TMP_Text>();
            if (text != null)
            {
                result["text"] = new JObject
                {
                    ["value"] = text.text,
                    ["fontSize"] = text.fontSize,
                    ["font"] = text.font != null ? text.font.name : null,
                    ["alignment"] = text.alignment.ToString(),
                    ["raycastTarget"] = text.raycastTarget,
                    ["color"] = ColorInfo(text.color)
                };
            }
            Selectable? selectable = go.GetComponent<Selectable>();
            if (selectable != null)
            {
                result["interaction"] = new JObject
                {
                    ["type"] = selectable.GetType().FullName,
                    ["interactable"] = selectable.interactable,
                    ["transition"] = selectable.transition.ToString()
                };
            }
            Canvas? canvas = go.GetComponent<Canvas>();
            if (canvas != null)
            {
                result["canvas"] = new JObject
                {
                    ["renderMode"] = canvas.renderMode.ToString(),
                    ["sortingOrder"] = canvas.sortingOrder,
                    ["scaleFactor"] = canvas.scaleFactor
                };
            }
            if (depth > 0)
            {
                var children = new JArray();
                for (int i = 0; i < go.transform.childCount; i++)
                    children.Add(InspectObject(go.transform.GetChild(i).gameObject, depth - 1));
                result["children"] = children;
            }
            return result;
        }

        private static JObject RectInfo(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Canvas? canvas = rect.GetComponentInParent<Canvas>();
            Camera? camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera ?? UIManager.Instance?.UiCamera
                : null;
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return new JObject
            {
                ["width"] = rect.rect.width,
                ["height"] = rect.rect.height,
                ["anchoredPosition"] = VectorInfo(rect.anchoredPosition),
                ["sizeDelta"] = VectorInfo(rect.sizeDelta),
                ["anchorMin"] = VectorInfo(rect.anchorMin),
                ["anchorMax"] = VectorInfo(rect.anchorMax),
                ["pivot"] = VectorInfo(rect.pivot),
                ["screenBounds"] = new JObject
                {
                    ["xMin"] = Math.Min(min.x, max.x),
                    ["yMin"] = Math.Min(min.y, max.y),
                    ["xMax"] = Math.Max(min.x, max.x),
                    ["yMax"] = Math.Max(min.y, max.y)
                }
            };
        }

        private static JObject VectorInfo(Vector2 value) => new JObject { ["x"] = value.x, ["y"] = value.y };
        private static JObject ColorInfo(Color value) => new JObject
        {
            ["r"] = value.r, ["g"] = value.g, ["b"] = value.b, ["a"] = value.a
        };
    }

    internal static class UiActionTools
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
                return McpToolResults.Error("ui_action_timeout", $"UI 动作在 {timeout}ms 内未完成。");
            done.Dispose();
            return response ?? McpToolResults.Error("ui_action_no_result", "UI 动作没有返回结果。");
        }

        private static IEnumerator Run(JObject args, Action<JObject> complete)
        {
            JObject response;
            try { response = ExecuteOnMainThread(args); }
            catch (Exception ex) { response = McpToolResults.Error("ui_action_failed", ex.GetType().Name + ": " + ex.Message); }
            int waitFrames = Math.Max(0, Math.Min(120, args["wait_frames"]?.Value<int>() ?? 1));
            for (int i = 0; i < waitFrames; i++) yield return null;

            if (!McpToolResults.IsError(response))
            {
                List<GameObject> afterMatches = UiLocator.Find(args["selector"] as JObject, false, 2);
                JObject? data = response["structuredContent"] as JObject;
                if (data != null)
                {
                    data["waitFrames"] = waitFrames;
                    data["after"] = afterMatches.Count == 1
                        ? UiInspectTools.InspectObject(afterMatches[0])
                        : JValue.CreateNull();
                    data["afterMatchCount"] = afterMatches.Count;
                }
            }
            complete(response);
        }

        internal static JObject ExecuteOnMainThread(JObject args)
        {
            JObject? selector = args["selector"] as JObject;
            List<GameObject> matches = UiLocator.Find(selector, false, 20);
            if (matches.Count == 0)
                return McpToolResults.Error("ui_not_found", "动作 selector 没有匹配到节点。",
                    new JObject { ["selector"] = selector?.DeepClone() });
            if (matches.Count != 1)
                return McpToolResults.Error("ui_ambiguous", $"动作 selector 匹配到 {matches.Count} 个节点。",
                    new JObject { ["paths"] = new JArray(matches.Select(UiLocator.FullPath)) });

            GameObject target = matches[0];
            string targetPath = UiLocator.FullPath(target);
            string mode = args["mode"]?.Value<string>()?.Trim().ToLowerInvariant() ?? "pointer";
            string actionResult;
            try
            {
                actionResult = mode switch
                {
                    "pointer" => PointerClick(target),
                    "invoke" => Invoke(target),
                    _ => throw new ArgumentException("mode 必须是 pointer 或 invoke")
                };
            }
            catch (Exception ex)
            {
                return McpToolResults.Error("ui_action_failed", ex.Message,
                    new JObject { ["path"] = targetPath, ["mode"] = mode });
            }

            var data = new JObject
            {
                ["path"] = targetPath,
                ["mode"] = mode,
                ["result"] = actionResult
            };
            return McpToolResults.Success(actionResult, data);
        }

        private static string PointerClick(GameObject target)
        {
            if (EventSystem.current == null) throw new InvalidOperationException("当前没有 EventSystem");
            RectTransform? rect = target.GetComponent<RectTransform>();
            if (rect == null) throw new InvalidOperationException("目标没有 RectTransform");
            Canvas? canvas = target.GetComponentInParent<Canvas>();
            Camera? camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera ?? UIManager.Instance?.UiCamera
                : null;
            Vector2 position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            var eventData = new PointerEventData(EventSystem.current) { position = position };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, hits);
            RaycastResult chosen = hits.FirstOrDefault(hit =>
                hit.gameObject == target ||
                hit.gameObject.transform.IsChildOf(target.transform) ||
                target.transform.IsChildOf(hit.gameObject.transform));
            if (chosen.gameObject == null)
                throw new InvalidOperationException($"目标中心点 ({position.x:F1}, {position.y:F1}) 没有命中目标射线");
            GameObject hitObject = chosen.gameObject;
            eventData.pointerPressRaycast = chosen;
            ExecuteEvents.ExecuteHierarchy(hitObject, eventData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hitObject, eventData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hitObject, eventData, ExecuteEvents.pointerClickHandler);
            return $"pointer 点击成功：{UiLocator.FullPath(target)} -> {UiLocator.FullPath(hitObject)}";
        }

        private static string Invoke(GameObject target)
        {
            Button? button = target.GetComponent<Button>() ?? target.GetComponentInParent<Button>();
            if (button != null)
            {
                button.onClick.Invoke();
                return "已调用 Button.onClick：" + UiLocator.FullPath(button.gameObject);
            }
            Toggle? toggle = target.GetComponent<Toggle>() ?? target.GetComponentInParent<Toggle>();
            if (toggle != null)
            {
                toggle.isOn = !toggle.isOn;
                return "已翻转 Toggle：" + UiLocator.FullPath(toggle.gameObject);
            }
            throw new InvalidOperationException("目标及父级没有 Button 或 Toggle；请改用 pointer 模式");
        }
    }
}
