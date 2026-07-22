using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TaiwuProbeFrontend
{
    /// <summary>
    /// JSON-RPC 2.0 分发。用 Newtonsoft.Json（游戏自带的 Managed DLL）处理解析和序列化。
    ///
    /// 输入：通过 JObject.Parse 深度正确的 JSON 解析，不会将嵌套对象的同名字段误读为顶层字段。
    /// 输出：通过 JObject / JArray 构建 JSON 响应，避免手动字符串拼接导致转义错误。
    /// 游戏不包含 Newtonsoft.Json.dll 时无需额外部署（<Private>false</Private> 引用 Managed 目录）。
    /// </summary>
    internal static class JsonRpc
    {
        #region 公共方法

        /// <summary>
        /// 从 JSON-RPC 请求体中提取 method 字段。
        /// 找不到时返回空字符串。解析异常时也返回空字符串（容错）。
        /// </summary>
        /// <param name="body">JSON-RPC 请求体原文。</param>
        /// <returns>method 字符串，或空字符串。</returns>
        public static string ExtractMethod(string body)
        {
            if (string.IsNullOrEmpty(body)) return "";
            try
            {
                var obj = JObject.Parse(body);
                return obj["method"]?.Value<string>() ?? "";
            }
            catch { return ""; }
        }

        /// <summary>
        /// 提取 id 字段的原样值（数字或字符串引号保留），用于原样回填响应。
        /// 找不到 id 时返回 null（用于通知检测）。
        /// </summary>
        /// <param name="body">JSON-RPC 请求体原文。</param>
        /// <returns>id 的原样文本（字符串带引号，数字原样），或 null。</returns>
        public static string? ExtractIdRaw(string body)
        {
            if (string.IsNullOrEmpty(body)) return null;
            try
            {
                var obj = JObject.Parse(body);
                var idToken = obj["id"];
                if (idToken == null) return null;
                return idToken.Type switch
                {
                    JTokenType.String => "\"" + idToken.Value<string>() + "\"",
                    JTokenType.Integer or JTokenType.Float => idToken.ToString(),
                    _ => null
                };
            }
            catch { return null; }
        }

        /// <summary>
        /// JSON-RPC 分发入口。根据 method 路由到对应处理函数，
        /// 包装 JSON-RPC 2.0 响应格式（含 jsonrpc/id/result 或 jsonrpc/id/error）。
        /// 通知类方法不返回响应体（返回 "{}"）。
        /// </summary>
        /// <param name="body">完整的 JSON-RPC 请求体。</param>
        /// <returns>JSON 格式的响应字符串。</returns>
        public static string Dispatch(string body)
        {
            string? id = ExtractIdRaw(body);
            string method = ExtractMethod(body);

            string result;
            try
            {
                result = method switch
                {
                    "initialize" => HandleInitialize(),
                    "tools/list" => HandleToolsList(),
                    "tools/call" => HandleToolsCall(body),
                    "notifications/initialized" => "{}",
                    _ => MakeError(-32601, $"Method not found: {method}"),
                };
            }
            catch (Exception ex)
            {
                return WrapError(id, -32603, $"Internal error: {ex.Message}");
            }

            // 通知类方法不返回响应（JSON-RPC 通知规范）
            if (method.StartsWith("notifications/", StringComparison.Ordinal))
                return "{}";

            return WrapResult(id, result);
        }

        #endregion

        #region MCP 方法处理

        /// <summary>initialize：返回协议版本、服务信息、能力声明。</summary>
        private static string HandleInitialize()
        {
            var resp = new JObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["serverInfo"] = new JObject
                {
                    ["name"] = "taiwu-probe",
                    ["version"] = "0.5.0"
                },
                ["capabilities"] = new JObject
                {
                    ["tools"] = new JObject()
                }
            };
            return resp.ToString(Formatting.None);
        }

        /// <summary>tools/list：返回前端、UI 自动化与后端工具清单。</summary>
        private static string HandleToolsList()
        {
            var tools = new JArray(
                new JObject
                {
                    ["name"] = "taiwu_eval",
                    ["description"] = "在太吾游戏前端进程里用反射求值或写入 C# 成员。用于调试：读写字段/属性、调方法、访问单例。支持链式访问、带参方法、int↔enum、Vector2/3。带 value: 段时为写入模式（对最后一级 field/property 赋值）。访问 Unity 对象时用 main: 前缀走主线程。",
                    ["inputSchema"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["expression"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "表达式。读：type:全名,member:成员[,member:...]；写：type:全名,member:成员,value:值；带参方法：type:全名,member:方法名(参数)；主线程：main:前缀。例：main:type:UnityEngine.Time,member:timeScale,value:0.5（慢动作），main:type:UnityEngine.GameObject,member:Find(\"Canvas\"),member:name"
                            }
                        },
                        ["required"] = new JArray("expression")
                    }
                },
                new JObject
                {
                    ["name"] = "taiwu_ping",
                    ["description"] = "探针连通性测试，返回 pong 和当前时间。用于确认 MOD 已加载且 HTTP server 正常。",
                    ["inputSchema"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject()
                    }
                },
                new JObject
                {
                    ["name"] = "taiwu_move",
                    ["description"] = "在大地图上按方向移动一步（up/down/left/right）。内部调用游戏原生寻路，走到目标地块为止。移动前建议先用 taiwu_map_info 查当前位置和可去方向。",
                    ["inputSchema"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["direction"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "移动方向：up / down / left / right（大小写不敏感）。对应屏幕坐标的上下左右。",
                                ["enum"] = new JArray("up", "down", "left", "right")
                            }
                        },
                        ["required"] = new JArray("direction")
                    }
                },
                new JObject
                {
                    ["name"] = "taiwu_map_info",
                    ["description"] = "查询当前大地图位置：AreaId、BlockId、移动状态、四个方向是否可达及目标 blockId。用于移动前决策。",
                    ["inputSchema"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject()
                    }
                }
            );

            tools.Add(new JObject
            {
                ["name"] = "taiwu_ui_screenshot",
                ["description"] = "在 Unity 帧末捕获游戏画面并直接返回 PNG image content；支持完整 game_client 或按 selector 裁剪 element，可同时保存文件。",
                ["inputSchema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["target"] = new JObject { ["type"] = "string", ["enum"] = new JArray("game_client", "element"), ["description"] = "默认 game_client" },
                        ["selector"] = SelectorSchema(),
                        ["wait_frames"] = new JObject { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 120, ["description"] = "截图前等待帧数，默认 2" },
                        ["save_path"] = new JObject { ["type"] = "string", ["description"] = "可选 PNG 保存路径" },
                        ["timeout_ms"] = TimeoutProperty(15000)
                    }
                }
            });
            tools.Add(new JObject
            {
                ["name"] = "taiwu_hotload_invoke",
                ["description"] = "从 DLL 字节热加载唯一程序集并在 Unity 主线程调用静态入口；检测 Mono 同名程序集冲突，支持基本参数、等待稳定帧并返回 SHA-256。",
                ["inputSchema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["assembly_path"] = new JObject { ["type"] = "string" },
                        ["type"] = new JObject { ["type"] = "string", ["description"] = "完整类型名" },
                        ["method"] = new JObject { ["type"] = "string", ["description"] = "静态方法名" },
                        ["arguments"] = new JObject { ["type"] = "array", ["description"] = "可选的基本类型参数" },
                        ["allow_existing"] = new JObject { ["type"] = "boolean", ["description"] = "允许调用已加载的同名程序集，默认 false" },
                        ["wait_frames"] = new JObject { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 120 },
                        ["timeout_ms"] = TimeoutProperty(10000)
                    },
                    ["required"] = new JArray("assembly_path", "type", "method")
                }
            });
            tools.Add(new JObject
            {
                ["name"] = "taiwu_frontend_log",
                ["description"] = "为前端 Unity 日志创建 cursor，或只读取 cursor 之后的新日志；可按 Log/Warning/Error/Assert/Exception 与文本过滤。",
                ["inputSchema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["action"] = new JObject { ["type"] = "string", ["enum"] = new JArray("mark", "tail"), ["description"] = "默认 tail" },
                        ["since"] = new JObject { ["type"] = "integer", ["minimum"] = 0 },
                        ["levels"] = new JObject { ["type"] = "array", ["items"] = new JObject { ["type"] = "string", ["enum"] = new JArray("Log", "Warning", "Error", "Assert", "Exception") } },
                        ["contains"] = new JObject { ["type"] = "string" },
                        ["limit"] = new JObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = 1000 }
                    }
                }
            });
            tools.Add(new JObject
            {
                ["name"] = "taiwu_ui_scenario",
                ["description"] = "一次完成可选热加载、多个 UI action/assert、前端异常检查和最终截图。适合无重启视觉回归，将整轮验证压缩为一次 MCP 调用。",
                ["inputSchema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["assembly"] = new JObject { ["type"] = "object", ["description"] = "参数同 taiwu_hotload_invoke" },
                        ["steps"] = new JObject { ["type"] = "array", ["description"] = "每项包含 action 或 assert；assert 使用 selector/property/equals" },
                        ["capture"] = new JObject { ["type"] = "object", ["description"] = "参数同 taiwu_ui_screenshot" },
                        ["fail_on_new_exceptions"] = new JObject { ["type"] = "boolean", ["description"] = "默认 true" },
                        ["continue_on_failure"] = new JObject { ["type"] = "boolean", ["description"] = "默认 false" }
                    }
                }
            });

            tools.Add(new JObject
            {
                ["name"] = "taiwu_ui_snapshot",
                ["description"] = "拍摄当前 UI 快照：场景摘要（场景/顶层窗口/其他层/地图装饰计数）+ 顶层窗口语义树（@eN ref、角色 button/toggle/slider/rangeslider/input/scroll/clickable、文字、状态 on/off/值）。之后用 @eN 调 click/fill/toggle/hover/scroll/describe。默认只展开顶层窗口；scope 可填 all 或层名/窗口名（如 LayerPart、查找地格）。同 scope 的再次调用会在头部附带与上次的变化 diff。",
                ["inputSchema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["scope"] = new JObject { ["type"] = "string", ["description"] = "空=顶层窗口（默认）；all=所有层；或层名/窗口名" },
                        ["max_lines"] = new JObject { ["type"] = "integer", ["minimum"] = 20, ["maximum"] = 1000, ["description"] = "输出硬上限，默认 200" }
                    }
                }
            });
            tools.Add(new JObject
            {
                ["name"] = "taiwu_ui_click",
                ["description"] = "点击 @eN（snapshot 分配）或 selector（逃生舱）。pointer 模式走真实 Raycast/PointerEvent，被遮挡时报出遮挡者；invoke 直接调 Button.onClick/Toggle。返回结果与动作后的变化 diff。ref 失效（stale_ref）时请重新 snapshot。",
                ["inputSchema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["ref"] = new JObject { ["type"] = "string", ["description"] = "snapshot 分配的引用，如 @e2" },
                        ["selector"] = SelectorSchema(),
                        ["mode"] = new JObject { ["type"] = "string", ["enum"] = new JArray("pointer", "invoke"), ["description"] = "默认 pointer" },
                        ["wait_frames"] = new JObject { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 120, ["description"] = "动作后等待帧数再算 diff，默认 3" },
                        ["timeout_ms"] = TimeoutProperty(10000)
                    }
                }
            });
            tools.Add(new JObject
            {
                ["name"] = "taiwu_ui_fill",
                ["description"] = "向 input（TMP_InputField）填入文本并提交（触发 onEndEdit），游戏会真实响应（联动滑条/搜索）。返回结果与变化 diff。",
                ["inputSchema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["ref"] = new JObject { ["type"] = "string", ["description"] = "input 节点的 @eN" },
                        ["selector"] = SelectorSchema(),
                        ["text"] = new JObject { ["type"] = "string", ["description"] = "要填入的文本" },
                        ["wait_frames"] = new JObject { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 120 },
                        ["timeout_ms"] = TimeoutProperty(10000)
                    },
                    ["required"] = new JArray("text")
                }
            });
            tools.Add(new JObject
            {
                ["name"] = "taiwu_ui_toggle",
                ["description"] = "把 toggle 显式设为 on 或 off（幂等，已是目标态则不动作）。比盲目翻转安全。返回结果与变化 diff。",
                ["inputSchema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["ref"] = new JObject { ["type"] = "string" },
                        ["selector"] = SelectorSchema(),
                        ["state"] = new JObject { ["type"] = "string", ["enum"] = new JArray("on", "off") },
                        ["wait_frames"] = new JObject { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 120 },
                        ["timeout_ms"] = TimeoutProperty(10000)
                    },
                    ["required"] = new JArray("state")
                }
            });
            tools.Add(new JObject
            {
                ["name"] = "taiwu_ui_hover",
                ["description"] = "悬停 @eN 触发 tooltip 并返回 tooltip 全文。内部优先用游戏原生 TooltipInvoker.ShowTips()（pointerEnter 模拟会被真实鼠标轮询刷掉）。用于查看资源/物品/功法详情。",
                ["inputSchema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["ref"] = new JObject { ["type"] = "string" },
                        ["selector"] = SelectorSchema(),
                        ["wait_frames"] = new JObject { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 120, ["description"] = "触发后等待帧数再读 tooltip，默认 2" },
                        ["timeout_ms"] = TimeoutProperty(10000)
                    }
                }
            });
            tools.Add(new JObject
            {
                ["name"] = "taiwu_ui_scroll",
                ["description"] = "滚动 scroll（CScrollRect）区域，direction up/down，delta 0~1。返回滚动位置变化与内容 diff。",
                ["inputSchema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["ref"] = new JObject { ["type"] = "string" },
                        ["selector"] = SelectorSchema(),
                        ["direction"] = new JObject { ["type"] = "string", ["enum"] = new JArray("up", "down"), ["description"] = "默认 down" },
                        ["delta"] = new JObject { ["type"] = "number", ["description"] = "滚动量（视口高度比例），默认 0.25" },
                        ["wait_frames"] = new JObject { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 120 },
                        ["timeout_ms"] = TimeoutProperty(10000)
                    }
                }
            });
            tools.Add(new JObject
            {
                ["name"] = "taiwu_ui_wait",
                ["description"] = "等待条件：某文字出现/消失（text + state=appear|disappear）、selector 出现/消失，或纯毫秒（ms）。带超时。用于异步加载、窗口打开/关闭确认。",
                ["inputSchema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["text"] = new JObject { ["type"] = "string", ["description"] = "等待出现/消失的文字（包含匹配）" },
                        ["selector"] = SelectorSchema(),
                        ["state"] = new JObject { ["type"] = "string", ["enum"] = new JArray("appear", "disappear"), ["description"] = "默认 appear" },
                        ["ms"] = new JObject { ["type"] = "integer", ["description"] = "纯等待毫秒数（不与 text/selector 同用）" },
                        ["timeout_ms"] = TimeoutProperty(10000)
                    }
                }
            });
            tools.Add(new JObject
            {
                ["name"] = "taiwu_ui_describe",
                ["description"] = "深挖单个节点：组件全限定类名+所在程序集（反编译定位用）、路径、rect/屏幕边界、文字、交互状态、父链组件摘要、可选子树。配合 taiwu_eval/反编译做代码追踪。",
                ["inputSchema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["ref"] = new JObject { ["type"] = "string" },
                        ["selector"] = SelectorSchema(),
                        ["depth"] = new JObject { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 3, ["description"] = "子树深度，默认 0" }
                    }
                }
            });
            tools.Add(new JObject
            {
                ["name"] = "taiwu_backend_ping",
                ["description"] = "检查 GameData 后端插件、主线程和 Domain 初始化状态。返回后端 PID、游戏版本、线程信息和 Domain 数量。",
                ["inputSchema"] = EmptyObjectSchema()
            });
            tools.Add(new JObject
            {
                ["name"] = "taiwu_backend_eval",
                ["description"] = "在 GameData 后端主线程中进行只读反射求值。可访问 DomainManager 和后端权威状态；支持 string/数值/bool/enum/null/可选参数并自动注入 DataContext。不支持 value: 写入，仅允许 Get/Is/Can/Has/Contains/ToString 查询方法。",
                ["inputSchema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["expression"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "例：type:GameData.Domains.DomainManager,member:Global,member:GetAchievement(1)"
                        }
                    },
                    ["required"] = new JArray("expression")
                }
            });
            tools.Add(new JObject
            {
                ["name"] = "taiwu_backend_type_search",
                ["description"] = "在 GameData 后端所有已加载程序集中按关键词搜索完整类型名。",
                ["inputSchema"] = SearchSchema("query", "类型名关键词")
            });
            tools.Add(new JObject
            {
                ["name"] = "taiwu_backend_members",
                ["description"] = "列出后端类型的字段、属性和完整方法签名，可按名称过滤并选择包含非公开成员。",
                ["inputSchema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["type"] = new JObject { ["type"] = "string", ["description"] = "完整类型名" },
                        ["filter"] = new JObject { ["type"] = "string", ["description"] = "可选的成员名过滤词" },
                        ["include_nonpublic"] = new JObject { ["type"] = "boolean", ["description"] = "是否包含 private/internal 成员，默认 false" },
                        ["limit"] = LimitProperty()
                    },
                    ["required"] = new JArray("type")
                }
            });
            tools.Add(new JObject
            {
                ["name"] = "taiwu_backend_log_tail",
                ["description"] = "读取最新 GameData_*.log 的末尾内容，可按文本过滤。",
                ["inputSchema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["contains"] = new JObject { ["type"] = "string", ["description"] = "可选的大小写不敏感过滤词" },
                        ["limit"] = LimitProperty()
                    }
                }
            });
            tools.Add(new JObject
            {
                ["name"] = "taiwu_backend_csharp",
                ["description"] = "危险：在 GameData 主线程编译并执行任意 C# 方法体。代码通过 return 返回结果，可直接使用 DomainManager 和自动提供的 DataContext context。仅执行可信查询代码；死循环或写操作可能卡死游戏、破坏存档。",
                ["inputSchema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["code"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "C# 方法体，必须 return 结果。例：var id = DomainManager.Taiwu.GetTaiwuCharId(); return id;"
                        },
                        ["usings"] = new JObject
                        {
                            ["type"] = "array",
                            ["items"] = new JObject { ["type"] = "string" },
                            ["description"] = "可选的额外命名空间列表"
                        }
                    },
                    ["required"] = new JArray("code")
                }
            });

            var result = new JObject { ["tools"] = tools };
            return result.ToString(Formatting.None);
        }

        /// <summary>
        /// tools/call：根据 name 分发到具体工具实现。
        ///
        /// 用 Newtonsoft.Json 的 JObject.Parse 解析 params 对象，
        /// 基于 JSON 树结构的属性访问天然支持嵌套，不会把 arguments.name 误读为 tool name。
        /// </summary>
        private static string HandleToolsCall(string body)
        {
            var obj = JObject.Parse(body);
            var paramsObj = obj["params"];
            if (paramsObj == null)
                throw new Exception("Missing params");

            string name = paramsObj["name"]?.Value<string>() ?? "";
            var args = paramsObj["arguments"] as JObject ?? new JObject();

            JObject response = name switch
            {
                "taiwu_ping" => McpToolResults.Text("pong " + DateTime.Now.ToString("HH:mm:ss")),
                "taiwu_eval" => McpToolResults.Text(ProbeTools.Eval(args["expression"]?.Value<string>() ?? "")),
                "taiwu_ui_snapshot" => UiSnapshotTools.SnapshotHandle(args),
                "taiwu_ui_click" => UiSnapshotTools.ClickHandle(args),
                "taiwu_ui_fill" => UiSnapshotTools.FillHandle(args),
                "taiwu_ui_hover" => UiSnapshotTools.HoverHandle(args),
                "taiwu_ui_toggle" => UiSnapshotTools.ToggleHandle(args),
                "taiwu_ui_scroll" => UiSnapshotTools.ScrollHandle(args),
                "taiwu_ui_wait" => UiSnapshotTools.WaitHandle(args),
                "taiwu_ui_describe" => UiSnapshotTools.DescribeHandle(args),
                "taiwu_move" => McpToolResults.Text(EvalOnMainThread(() => UiTools.MoveByDirection(args["direction"]?.Value<string>() ?? ""))),
                "taiwu_map_info" => McpToolResults.Text(EvalOnMainThread(() => UiTools.GetMapInfo())),
                "taiwu_ui_screenshot" => ScreenshotTools.Handle(args),
                "taiwu_hotload_invoke" => HotLoadTools.Handle(args),
                "taiwu_frontend_log" => FrontendLogBuffer.Handle(args),
                "taiwu_ui_scenario" => UiScenarioTools.Handle(args),
                "taiwu_backend_ping" or
                "taiwu_backend_eval" or
                "taiwu_backend_type_search" or
                "taiwu_backend_members" or
                "taiwu_backend_log_tail" or
                "taiwu_backend_csharp" => McpToolResults.Text(BackendBridge.Call(name, args)),
                _ => throw new Exception($"Unknown tool: {name}"),
            };
            return response.ToString(Formatting.None);
        }

        #endregion

        #region 后端工具 Schema

        private static JObject EmptyObjectSchema() => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject()
        };

        private static JObject SearchSchema(string key, string description) => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                [key] = new JObject { ["type"] = "string", ["description"] = description },
                ["limit"] = LimitProperty()
            },
            ["required"] = new JArray(key)
        };

        private static JObject LimitProperty() => new JObject
        {
            ["type"] = "integer",
            ["minimum"] = 1,
            ["maximum"] = 500,
            ["description"] = "最大返回条数，默认 100"
        };

        private static JObject SelectorSchema() => new JObject
        {
            ["type"] = "object",
            ["description"] = "UI selector。可组合 path/name/text/component；path 支持完整路径或唯一后缀。",
            ["properties"] = new JObject
            {
                ["path"] = new JObject { ["type"] = "string" },
                ["name"] = new JObject { ["type"] = "string" },
                ["text"] = new JObject { ["type"] = "string" },
                ["exact_text"] = new JObject { ["type"] = "boolean", ["description"] = "默认 true" },
                ["component"] = new JObject { ["type"] = "string", ["description"] = "组件短名或完整类型名" }
            }
        };

        private static JObject TimeoutProperty(int defaultValue) => new JObject
        {
            ["type"] = "integer",
            ["minimum"] = 1000,
            ["maximum"] = 60000,
            ["description"] = $"超时毫秒数，默认 {defaultValue}"
        };

        #endregion

        #region JSON-RPC 响应构造

        /// <summary>包装成功响应：{"jsonrpc":"2.0","id":<id>,"result":<result>}</summary>
        private static string WrapResult(string? id, string result)
        {
            var resp = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id != null ? JToken.Parse(id) : JValue.CreateNull(),
                ["result"] = JToken.Parse(result)
            };
            return resp.ToString(Formatting.None);
        }

        /// <summary>包装错误响应：{"jsonrpc":"2.0","id":<id>,"error":{"code":<code>,"message":<msg>}}</summary>
        private static string WrapError(string? id, int code, string message)
        {
            var resp = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id != null ? JToken.Parse(id) : JValue.CreateNull(),
                ["error"] = new JObject
                {
                    ["code"] = code,
                    ["message"] = message
                }
            };
            return resp.ToString(Formatting.None);
        }

        /// <summary>生成纯错误对象（不加外层 jsonrpc/id），用于 dispatch switch 中的未知 method。</summary>
        private static string MakeError(int code, string message)
        {
            var err = new JObject
            {
                ["code"] = code,
                ["message"] = message
            };
            return err.ToString(Formatting.None);
        }

        #endregion

        #region 主线程执行

        /// <summary>
        /// 在主线程执行委托，同步等待结果（超时 3 秒）。
        /// 所有 UI 操作（ListElements/GetTree/Click/TriggerButton）都需要在主线程执行，
        /// 通过此方法将 Func 排入 MainThreadRunner 队列并等待主线程消费。
        /// </summary>
        private static string EvalOnMainThread(Func<string> func)
        {
            if (!MainThreadRunner.IsAvailable) return "<Unity 主线程执行器尚未初始化>";
            string? result = null;
            Exception? error = null;
            var done = new System.Threading.ManualResetEventSlim(false);
            MainThreadRunner.RunOnMainThread(() =>
            {
                try { result = func(); }
                catch (Exception ex) { error = ex; }
                finally { done.Set(); }
            });
            if (!done.Wait(3000))
                return "<主线程执行超时（3秒）>";
            done.Dispose();
            if (error != null)
                return "<主线程执行异常: " + error.Message + ">";
            return result ?? "<null>";
        }

        /// <summary>结构化工具共用的主线程 adapter，保持错误也采用标准 MCP tool result。</summary>
        internal static JObject RunStructuredOnMainThread(Func<JObject> func)
        {
            if (!MainThreadRunner.IsAvailable)
                return McpToolResults.Error("main_thread_unavailable", "Unity 主线程执行器尚未初始化。");
            JObject? result = null;
            Exception? error = null;
            var done = new System.Threading.ManualResetEventSlim(false);
            MainThreadRunner.RunOnMainThread(() =>
            {
                try { result = func(); }
                catch (Exception ex) { error = ex; }
                finally { done.Set(); }
            });
            if (!done.Wait(5000))
                return McpToolResults.Error("main_thread_timeout", "Unity 主线程执行超时（5秒）。");
            done.Dispose();
            if (error != null)
                return McpToolResults.Error("main_thread_exception", error.GetType().Name + ": " + error.Message);
            return result ?? McpToolResults.Error("main_thread_no_result", "Unity 主线程未返回结果。");
        }

        #endregion
    }
}
