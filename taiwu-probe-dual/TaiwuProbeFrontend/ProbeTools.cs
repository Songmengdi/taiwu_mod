using System;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace TaiwuProbeFrontend
{
    /// <summary>
    /// 探针工具实现。通过反射链式求值，覆盖静态字段/属性/无参方法的查询。
    ///
    /// 表达式语法：
    ///   - "type:全名,member:成员名"  → 查指定类型的静态成员
    ///     例：type:UnityEngine.Application,member:loadedLevelName
    ///   - "type:全名"               → 列出该类型的所有静态字段/属性（概览）
    ///   - "main:type:全名,member:..." → 在主线程执行（查 Unity 对象用这个，绕过主线程限制）
    ///   - 其它                      → 原样返回（提示用法）
    /// </summary>
    internal static class ProbeTools
    {
        /// <summary>
        /// 反射求值入口。解析表达式，定位类型，链式访问成员。
        /// </summary>
        /// <param name="expression">表达式字符串，详见类注释语法说明。</param>
        /// <returns>求值结果文本（或错误信息）。</returns>
        public static string Eval(string expression)
        {
            expression = (expression ?? "").Trim();
            if (expression.Length == 0)
                return "用法：\n" +
                       "  type:全名,member:成员名  查静态成员\n" +
                       "  type:全名  列出静态成员\n" +
                       "  main:type:全名,member:成员名  在主线程执行（查 Unity 对象用这个）\n" +
                       "  带参方法：type:全名,member:方法名(参数1,参数2,...)  支持多参数/数值/枚举\n" +
                       "  泛型方法：type:全名,member:方法名<T>()  如 getInstance<WorldMapModel>()";

            // 主线程模式：排到主线程执行，同步等待结果
            // Unity 的大量 API（Application.isPlaying 等）只能在主线程读取
            if (expression.StartsWith("main:", StringComparison.Ordinal))
            {
                return EvalOnMainThread(expression.Substring(5));
            }

            return EvalCore(expression);
        }

        /// <summary>
        /// 在主线程执行求值。通过 MainThreadRunner 排到主线程的 Update 回调中执行，
        /// 用 ManualResetEventSlim 同步等待结果。超时 3 秒防止后台线程永久阻塞。
        /// </summary>
        private static string EvalOnMainThread(string innerExpr)
        {
            if (!MainThreadRunner.IsAvailable) return "<Unity 主线程执行器尚未初始化>";
            string? result = null;
            Exception? error = null;
            var done = new System.Threading.ManualResetEventSlim(false);
            TaiwuProbeFrontend.MainThreadRunner.RunOnMainThread(() =>
            {
                try { result = EvalCore(innerExpr); }
                catch (Exception ex) { error = ex; }
                finally { done.Set(); }
            });
            // 3 秒超时：防止 Unity 主线程卡死时后台线程无限等待
            if (!done.Wait(3000))
                return "<主线程执行超时（3秒）>";
            done.Dispose();
            if (error != null)
                return "<主线程执行异常: " + error.Message + ">";
            return result ?? "<null>";
        }

        /// <summary>
        /// 核心求值。解析 type: 和 member: 段，逐级链式访问成员。
        /// 支持字段 → 属性 → 无参方法的自动查找（按优先级）。
        ///
        /// 特殊 member 语法：
        ///   "成员名:N"     → 访问列表/数组的第 N 个元素（N 从 0 开始）
        ///   例：member:InventoryItems:0  → 取 InventoryItems 列表的第一个元素
        /// </summary>
        private static string EvalCore(string expression)
        {
            // 解析 type、所有 member（支持链式访问）和可选的 value（写入语义）
            // value: 段一旦出现，其内容取到表达式结尾（不按逗号分割），支持 "x,y" 这类含逗号的值
            string? typeName = null;
            string? setValue = null;   // value: 段存在时表示写入模式
            var members = new System.Collections.Generic.List<string>();
            // 先用正则定位 "value:" 段，把表达式切成 主表达式 和 value 两部分
            int valueIdx = expression.IndexOf("value:", StringComparison.Ordinal);
            string mainExpr = expression;
            if (valueIdx >= 0)
            {
                // value: 前必须以逗号分隔（避免 member 名含 value 子串误判）
                // 简化处理：找到 ",value:" 或行首 "value:"
                int commaIdx = expression.LastIndexOf(',', valueIdx, valueIdx);
                if (commaIdx >= 0 && expression.Substring(commaIdx + 1, valueIdx - commaIdx - 1).Trim() == "value")
                {
                    // 形如 ...,value:XXX → value 取 valueIdx+6 到结尾
                    mainExpr = expression.Substring(0, commaIdx);
                    setValue = expression.Substring(valueIdx + 6); // 跳过 "value:"
                }
                else if (expression.StartsWith("value:", StringComparison.Ordinal))
                {
                    mainExpr = "";
                    setValue = expression.Substring(6);
                }
                else
                {
                    setValue = null; // 非合法 value 段
                }
            }

            foreach (var part in mainExpr.Split(','))
            {
                var kv = part.Split(new[] { ':' }, 2);
                if (kv.Length == 2)
                {
                    string key = kv[0].Trim();
                    string val = kv[1].Trim();
                    if (key == "type") typeName = val;
                    else if (key == "member") members.Add(val);
                }
            }

            // 也支持直接传类型全名（不含 type: 前缀），简化快捷查询
            if (typeName == null && expression.IndexOf(':') < 0)
                typeName = expression;

            if (typeName == null)
                return "无法解析 type，用法：\n" +
                       "  读：type:全名,member:成员名[,member:成员名...]\n" +
                       "  写：type:全名,member:成员名,value:值   （对最后一个 member 赋值）\n" +
                       "  带参方法：type:全名,member:方法名(参数1,参数2,...)  支持数值/枚举/多参数\n" +
                       "  泛型方法：type:全名,member:方法名<T>()  如 getInstance<WorldMapModel>()\n" +
                       "例：type:UnityEngine.GameObject,member:Find(\"Canvas\"),member:name\n" +
                       "    type:UnityEngine.Time,member:timeScale,value:0.5\n" +
                       "    type:SingletonObject,member:getInstance<WorldMapModel>(),member:CurrentBlockId";

            Type? t = FindType(typeName);
            if (t == null)
                return $"未找到类型：{typeName}（已搜所有已加载程序集）\n" +
                       $"提示：需要完整类型名（含命名空间），如 UnityEngine.GameObject 而非 GameObject";

            // 无 member：列出所有静态成员（快速概览）
            if (members.Count == 0)
                return ListStaticMembers(t);

            bool isWrite = setValue != null;

            // 链式成员访问：逐级求值，每级结果作为下一级的实例。
            // 写入模式：前 N-1 级正常 GetValue 定位目标，最后一级 SetValue。
            object? current = null;
            Type currentType = t;

            for (int i = 0; i < members.Count; i++)
            {
                string memberName = members[i];
                bool isLast = i == members.Count - 1;
                bool shouldSet = isWrite && isLast;

                var result = ResolveMember(currentType, memberName, current, shouldSet ? setValue : null);
                // 如果声明类型没找到成员但实例有运行时类型，用运行时类型再试一次
                if (result.Error != null && current != null && current.GetType() != currentType)
                {
                    result = ResolveMember(current.GetType(), memberName, current, shouldSet ? setValue : null);
                }
                if (result.Error != null)
                    return result.Error;

                current = result.Value;
                currentType = result.Type ?? current?.GetType() ?? typeof(object);

                // 值为 null 时提前结束链式访问，避免 NullReferenceException
                if (current == null && i < members.Count - 1)
                    return $"成员 \"{memberName}\" 返回 null，无法继续访问后续成员";
            }

            return FormatResult(current, currentType);
        }

        /// <summary>成员解析结果。携带值、完整类型、或错误信息。</summary>
        private struct MemberResult
        {
            /// <summary>成员的值，可为 null。</summary>
            public object? Value;

            /// <summary>成员的声明类型（字段类型 / 属性类型 / 方法返回类型）。</summary>
            public Type? Type;

            /// <summary>解析失败时的错误信息。</summary>
            public string? Error;
        }

        /// <summary>
        /// 在指定类型上解析一个成员名。查找顺序：字段 → 属性 → 无参方法 → 带一个字符串参数的方法。
        /// 成员可公开也可非公开（BindingFlags.NonPublic），因为调试需要访问游戏内部的私有状态。
        ///
        /// 索引语法：memberName 格式 "名称:N" 时，先按名称解析，再取结果的第 N 个元素。
        /// 如果 instance 本身是 IList / 数组且 memberName 是纯数字字符串，直接按索引取值。
        ///
        /// 带参方法语法：memberName 格式 "方法名(参数字符串)" 时，调用带一个字符串参数的方法。
        /// 例：Find("ItemName") 或 GetComponent("TextMeshProUGUI, Assembly-CSharp")
        ///
        /// 写入语义：setValue 非空时，对最后一级 field/property 执行 SetValue（赋值）而非 GetValue。
        /// </summary>
        private static MemberResult ResolveMember(Type type, string memberName, object? instance, string? setValue = null)
        {
            // 写入模式不支持带参方法 / 索引（只对 field/property 有意义）
            if (setValue == null)
            {
                // 带参方法语法 "方法名(参数)"，支持多参数、类型自动转换、泛型方法。
                //   单 string 参数（向后兼容）：Find("Canvas")
                //   数值/多参数：MoveToBlock(123)、GetNeighbor(block, Up, true)
                //   泛型：getInstance<WorldMapModel>()
                int parenOpen = memberName.IndexOf('(');
                if (parenOpen > 0 && memberName.EndsWith(")"))
                {
                    string methodName = memberName.Substring(0, parenOpen);
                    string argsText = memberName.Substring(parenOpen + 1, memberName.Length - parenOpen - 2);

                    // 泛型方法语法 "名<T>()"：methodName 末尾有 <T>，用 MakeGenericMethod 调用
                    // 例：getInstance<WorldMapModel>() —— 取单例
                    int genericOpen = methodName.IndexOf('<');
                    Type[]? genericArgs = null;
                    if (genericOpen > 0 && methodName.EndsWith(">"))
                    {
                        string genericBody = methodName.Substring(genericOpen + 1, methodName.Length - genericOpen - 2);
                        methodName = methodName.Substring(0, genericOpen);
                        // 支持逗号分隔的多个泛型参数（暂只解析单个，够覆盖 getInstance<T>）
                        var gType = FindType(genericBody.Trim());
                        if (gType == null)
                            return new MemberResult { Error = $"泛型参数类型未找到：{genericBody}" };
                        genericArgs = new[] { gType };
                    }

                    // 拆分参数列表：识别引号包裹的字符串（内部逗号不算分隔符）
                    var argStrings = SplitArguments(argsText);
                    foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                    {
                        if (m.Name != methodName) continue;
                        if (m.IsGenericMethod && genericArgs != null)
                        {
                            // 泛型方法：用指定的类型实参具现化后再匹配参数
                            MethodInfo concrete;
                            try { concrete = m.MakeGenericMethod(genericArgs); }
                            catch { continue; }
                            var pars = concrete.GetParameters();
                            var converted = TryMatchArgs(argStrings, pars);
                            if (converted != null)
                            {
                                try
                                {
                                    object? val = concrete.Invoke(instance, converted);
                                    return new MemberResult { Value = val, Type = concrete.ReturnType };
                                }
                                catch (Exception ex)
                                {
                                    return new MemberResult { Error = $"调用 {type.Name}.{methodName}<{string.Join(",", genericArgs.Select(a => a.Name))}>(...) 异常: {ex.Message}" };
                                }
                            }
                        }
                        else if (!m.IsGenericMethod && genericArgs == null)
                        {
                            // 普通方法：参数数量与类型匹配即可
                            var pars = m.GetParameters();
                            var converted = TryMatchArgs(argStrings, pars);
                            if (converted != null)
                            {
                                try
                                {
                                    object? val = m.Invoke(instance, converted);
                                    return new MemberResult { Value = val, Type = m.ReturnType };
                                }
                                catch (Exception ex)
                                {
                                    return new MemberResult { Error = $"调用 {type.Name}.{methodName}({argsText}) 异常: {ex.Message}" };
                                }
                            }
                        }
                    }
                    return new MemberResult { Error = $"未找到匹配的重载：{type.Name}.{memberName}（参数数量或类型不匹配）" };
                }

                // 先尝试索引语法 "名称:N"：先从父对象取成员，再按索引取子元素
                int colonIdx = memberName.LastIndexOf(':');
                if (colonIdx > 0 && int.TryParse(memberName.Substring(colonIdx + 1), out int listIndex))
                {
                    string baseMember = memberName.Substring(0, colonIdx);
                    var baseResult = ResolveMemberCore(type, baseMember, instance);
                    if (baseResult.Error != null)
                        return baseResult;
                    return IndexResult(baseResult.Value, listIndex);
                }

                // 纯数字：如果 instance 是 IList/数组，直接按索引取值
                if (int.TryParse(memberName, out int directIndex))
                {
                    return IndexResult(instance, directIndex);
                }
            }

            return ResolveMemberCore(type, memberName, instance, setValue);
        }

        /// <summary>
        /// 取 IList / 数组的第 index 个元素（index 从 0 开始）。
        /// </summary>
        private static MemberResult IndexResult(object? value, int index)
        {
            if (value == null)
                return new MemberResult { Error = "无法索引 null 对象" };

            if (value is System.Collections.IList list)
            {
                if (index < 0 || index >= list.Count)
                    return new MemberResult { Error = $"索引 {index} 越界，范围 0..{list.Count - 1}" };
                var item = list[index];
                return new MemberResult { Value = item, Type = item?.GetType() ?? typeof(object) };
            }

            // 数组也实现了 IList，上面的分支已处理；再加一个安全的数组专门处理
            if (value is System.Array arr)
            {
                if (index < 0 || index >= arr.Length)
                    return new MemberResult { Error = $"索引 {index} 越界，范围 0..{arr.Length - 1}" };
                var item = arr.GetValue(index);
                return new MemberResult { Value = item, Type = item?.GetType() ?? typeof(object) };
            }

            return new MemberResult { Error = $"无法对类型 {value.GetType().Name} 按索引访问，它不是一个列表或数组" };
        }

        /// <summary>
        /// 核心成员解析（不含索引语法）。查找顺序：字段 → 属性 → 无参方法。
        /// 成员可公开也可非公开（BindingFlags.NonPublic），因为调试需要访问游戏内部的私有状态。
        ///
        /// 写入语义：setValue 非空时，对 field/property 执行 SetValue。
        /// 值类型转换：先取当前值推断目标类型，再 ConvertFromstring 转换（支持基本类型/enum/Vector2/3/Color）。
        /// </summary>
        private static MemberResult ResolveMemberCore(Type type, string memberName, object? instance, string? setValue = null)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.Instance | BindingFlags.Static;

            // 先尝试字段（最常见的求值目标）
            var field = type.GetField(memberName, flags);
            if (field != null)
            {
                if (setValue != null)
                {
                    try
                    {
                        object? converted = ConvertValue(setValue, field.FieldType);
                        field.SetValue(instance, converted);
                        return new MemberResult { Value = converted, Type = field.FieldType };
                    }
                    catch (Exception ex)
                    {
                        return new MemberResult { Error = $"写字段 {type.Name}.{memberName} 异常: {ex.Message}" };
                    }
                }
                try
                {
                    object? val = field.GetValue(instance);
                    return new MemberResult { Value = val, Type = field.FieldType };
                }
                catch (Exception ex)
                {
                    return new MemberResult { Error = $"读取字段 {type.Name}.{memberName} 异常: {ex.Message}" };
                }
            }

            // 再尝试属性（排除带索引器的属性）
            var prop = type.GetProperty(memberName, flags);
            if (prop != null && prop.GetIndexParameters().Length == 0)
            {
                if (setValue != null)
                {
                    try
                    {
                        object? converted = ConvertValue(setValue, prop.PropertyType);
                        prop.SetValue(instance, converted);
                        return new MemberResult { Value = converted, Type = prop.PropertyType };
                    }
                    catch (Exception ex)
                    {
                        return new MemberResult { Error = $"写属性 {type.Name}.{memberName} 异常: {ex.Message}" };
                    }
                }
                try
                {
                    object? val = prop.GetValue(instance);
                    return new MemberResult { Value = val, Type = prop.PropertyType };
                }
                catch (Exception ex)
                {
                    return new MemberResult { Error = $"读取属性 {type.Name}.{memberName} 异常: {ex.Message}" };
                }
            }

            // setValue 模式下，成员不是 field/property 就无法写入
            if (setValue != null)
                return new MemberResult { Error = $"{type.Name}.{memberName} 不是字段/属性，无法赋值" };

            // 尝试无参方法（只在链式访问的最后一个 member 时调用有意义）
            var method = type.GetMethod(memberName, flags, null, Type.EmptyTypes, null);
            if (method != null)
            {
                try
                {
                    object? val = method.Invoke(instance, null);
                    return new MemberResult { Value = val, Type = method.ReturnType };
                }
                catch (Exception ex)
                {
                    return new MemberResult { Error = $"调用方法 {type.Name}.{memberName} 异常: {ex.Message}" };
                }
            }

            // 如果有实例，列出可用成员帮助调试（快速定位拼写错误）
            string hint = instance != null
                ? $"。可用成员: {string.Join(", ", GetMemberNames(type, instance != null))}"
                : "";

            return new MemberResult { Error = $"在 {type.Name} 中未找到成员 \"{memberName}\"{hint}" };
        }

        /// <summary>
        /// 把字符串值转换为目标类型。支持基本类型、enum、Vector2/3、Color、bool。
        /// 用于 eval 的 value: 写入语义。
        /// </summary>
        private static object? ConvertValue(string text, Type targetType)
        {
            if (targetType == typeof(string)) return text;
            if (targetType == typeof(bool)) return text == "1" || string.Equals(text, "true", StringComparison.OrdinalIgnoreCase);
            if (targetType.IsEnum) return Enum.Parse(targetType, text, ignoreCase: true);

            // Vector2/3：用 TypeDescriptor 或手动解析 "x,y" / "x,y,z"
            if (targetType == typeof(UnityEngine.Vector2))
            {
                var p = text.Split(',');
                if (p.Length == 2) return new UnityEngine.Vector2(float.Parse(p[0].Trim()), float.Parse(p[1].Trim()));
            }
            if (targetType == typeof(UnityEngine.Vector3))
            {
                var p = text.Split(',');
                if (p.Length == 3) return new UnityEngine.Vector3(float.Parse(p[0].Trim()), float.Parse(p[1].Trim()), float.Parse(p[2].Trim()));
            }
            if (targetType == typeof(UnityEngine.Color))
            {
                var p = text.Split(',');
                if (p.Length == 4) return new UnityEngine.Color(float.Parse(p[0].Trim()), float.Parse(p[1].Trim()), float.Parse(p[2].Trim()), float.Parse(p[3].Trim()));
            }

            // 基本数值类型
            return System.Convert.ChangeType(text, targetType);
        }

        /// <summary>
        /// 获取指定类型上可访问的成员名列表（字段/属性/无参方法）。
        /// 最多显示 15 个，超出用 "..." 截断。
        /// </summary>
        private static string GetMemberNames(Type type, bool includeInstance)
        {
            var names = new System.Collections.Generic.List<string>();
            var flags = BindingFlags.Public | BindingFlags.Static;
            if (includeInstance) flags |= BindingFlags.Instance;

            foreach (var f in type.GetFields(flags))
                names.Add($"[字段] {f.Name}");
            foreach (var p in type.GetProperties(flags))
                if (p.GetIndexParameters().Length == 0) names.Add($"[属性] {p.Name}");
            foreach (var m in type.GetMethods(flags))
                if (!m.IsSpecialName && m.GetParameters().Length == 0) names.Add($"[方法] {m.Name}()");

            if (names.Count > 15)
            {
                names.RemoveRange(15, names.Count - 15);
                names.Add("...");
            }
            return string.Join(", ", names);
        }

        /// <summary>
        /// 格式化求值结果。集合类型显示数量，非集合类型显示简短值。
        /// List / Dictionary / IEnumerable 分别处理避免输出过长。
        /// </summary>
        private static string FormatResult(object? value, Type type)
        {
            if (value == null) return "null";

            // 集合：显示类型 + 元素数量，而非序列化全部元素
            if (value is System.Collections.IList list)
            {
                string itemType = type.GenericTypeArguments.Length > 0
                    ? type.GenericTypeArguments[0].Name : "?";
                return $"[List<{itemType}> Count={list.Count}]";
            }

            if (value is System.Collections.IDictionary dict)
                return $"[Dictionary Count={dict.Count}]";

            if (value is System.Collections.IEnumerable enumerable && value is not string)
            {
                int count = 0;
                foreach (var _ in enumerable) { count++; if (count > 100) break; }
                return $"[IEnumerable Count>={count}]";
            }

            return FormatValue(value);
        }

        /// <summary>
        /// 在全部已加载程序集中查找类型。两阶段搜索：
        ///   1. 直接 Type.GetType(fullName) —— 处理 mscorlib / System 等框架类型
        ///   2. 遍历 AppDomain 所有程序集逐次查找 —— 处理 Unity / 游戏自定义类型
        /// </summary>
        private static Type? FindType(string fullName)
        {
            var t = Type.GetType(fullName);
            if (t != null) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>
        /// 列出指定类型的所有公开静态字段和属性及其当前值。
        /// 用于 type:全名（不带 member）时的快速概览。
        /// </summary>
        private static string ListStaticMembers(Type type)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"类型：{type.FullName}");
            sb.AppendLine("静态字段/属性：");

            foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                string v = SafeValueShort(() => FormatValue(f.GetValue(null)));
                sb.AppendLine($"  [字段] {f.FieldType.Name} {f.Name} = {v}");
            }
            foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                string v = SafeValueShort(() => FormatValue(p.GetValue(null)));
                sb.AppendLine($"  [属性] {p.PropertyType.Name} {p.Name} = {v}");
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// 格式化单一值。字符串加引号，原始类型直接 ToString，
        /// 复杂类型显示 [类型名] 加 ToString 截断。
        /// </summary>
        private static string FormatValue(object? value)
        {
            if (value == null) return "null";
            if (value is string s) return $"\"{s}\"";
            var t = value.GetType();
            if (t.IsPrimitive || t.IsEnum) return value.ToString() ?? "";
            string str = value.ToString() ?? t.Name;
            if (str.Length > 200) str = str.Substring(0, 200) + "...";
            return $"[{t.Name}] {str}";
        }

        /// <summary>安全执行取值函数，异常时返回错误文本。</summary>
        private static string SafeValue(Func<string> getter)
        {
            try { return getter(); }
            catch (Exception ex) { return "<读取异常: " + ex.Message + ">"; }
        }

        /// <summary>
        /// 安全执行取值函数并截断结果到 80 字符。
        /// 用于 ListStaticMembers 中每行输出，避免单个过长的值撑满输出。
        /// </summary>
        private static string SafeValueShort(Func<string> getter)
        {
            try
            {
                string v = getter();
                if (v.Length > 80) v = v.Substring(0, 80) + "...";
                return v;
            }
            catch (Exception ex) { return "<异常: " + ex.Message + ">"; }
        }

        /// <summary>
        /// 拆分方法参数字符串。识别引号包裹的字符串字面量（内部逗号不算分隔符），
        /// 其余按逗号分隔。空字符串返回空列表（表示无参数）。
        /// 例：Find("Canvas") → ["\"Canvas\""]；MoveToBlock(123) → ["123"]；
        ///     GetNeighbor(block, Up, true) → ["block","Up","true"]
        /// </summary>
        private static System.Collections.Generic.List<string> SplitArguments(string argsText)
        {
            var result = new System.Collections.Generic.List<string>();
            if (string.IsNullOrWhiteSpace(argsText)) return result;
            int depth = 0;
            bool inString = false;
            char stringQuote = '\0';
            int start = 0;
            for (int i = 0; i < argsText.Length; i++)
            {
                char c = argsText[i];
                if (inString)
                {
                    if (c == stringQuote) inString = false;
                }
                else if (c == '"' || c == '\'')
                {
                    inString = true;
                    stringQuote = c;
                }
                else if (c == '(' || c == '[' || c == '<') depth++;
                else if (c == ')' || c == ']' || c == '>') depth--;
                else if (c == ',' && depth == 0)
                {
                    result.Add(argsText.Substring(start, i - start).Trim());
                    start = i + 1;
                }
            }
            if (start < argsText.Length)
                result.Add(argsText.Substring(start).Trim());
            return result;
        }

        /// <summary>
        /// 尝试把字符串参数列表转换成形参数组要求的类型。
        /// 参数数量必须完全一致，每个参数用 ConvertSingleArg 转换；任一失败返回 null。
        /// 返回 null 表示此重载不匹配，调用方会继续尝试下一个重载。
        /// </summary>
        private static object?[]? TryMatchArgs(System.Collections.Generic.IList<string> argStrings, ParameterInfo[] pars)
        {
            // 可选参数：传入参数可以少于形参总数（缺省用默认值）
            if (argStrings.Count > pars.Length) return null;
            var result = new object?[pars.Length];
            for (int i = 0; i < pars.Length; i++)
            {
                if (i < argStrings.Count)
                {
                    var converted = ConvertSingleArg(argStrings[i], pars[i].ParameterType);
                    if (converted == null) return null;  // 类型不匹配，此重载放弃
                    result[i] = converted;
                }
                else if (pars[i].HasDefaultValue)
                {
                    result[i] = pars[i].DefaultValue;
                }
                else
                {
                    return null;  // 必填参数缺失
                }
            }
            return result;
        }

        /// <summary>
        /// 把单个字符串参数转换为目标类型。返回 null 表示无法转换（用于重载匹配回退）。
        /// 支持的类型：
        ///   string —— 去掉两侧引号（"x" 或 'x'），无引号原样返回
        ///   bool   —— "true"/"false"（不区分大小写）或 "1"/"0"
        ///   整数   —— sbyte/byte/short/ushort/int/uint/long/ulong，解析失败返回 null
        ///   enum   —— 按名（Up/Down/Left/Right）或数字解析
        ///   其它   —— 不支持，返回 null（保证只处理常见调试场景）
        /// </summary>
        private static object? ConvertSingleArg(string text, Type targetType)
        {
            if (targetType == typeof(string))
            {
                // 去掉字符串字面量的引号；无引号时原样返回（兼容旧用法 Find(Canvas)）
                if (text.Length >= 2 && ((text[0] == '"' && text[^1] == '"') || (text[0] == '\'' && text[^1] == '\'')))
                    return text.Substring(1, text.Length - 2);
                return text;
            }
            if (targetType == typeof(bool))
            {
                string t = text.Trim();
                if (string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)) return false;
                if (t == "1") return true;
                if (t == "0") return false;
                return null;
            }
            if (targetType.IsEnum)
            {
                // 先按枚举名解析，失败再按数字
                try { return Enum.Parse(targetType, text.Trim(), ignoreCase: true); } catch { }
                if (long.TryParse(text.Trim(), out long enumVal))
                {
                    try { return Enum.ToObject(targetType, enumVal); } catch { }
                }
                return null;
            }
            // 整数类型族：逐个尝试（避免 Convert.ChangeType 对 sbyte/short 的溢出误判）
            string num = text.Trim();
            if (targetType == typeof(sbyte)) { if (sbyte.TryParse(num, out var v)) return v; return null; }
            if (targetType == typeof(byte)) { if (byte.TryParse(num, out var v)) return v; return null; }
            if (targetType == typeof(short)) { if (short.TryParse(num, out var v)) return v; return null; }
            if (targetType == typeof(ushort)) { if (ushort.TryParse(num, out var v)) return v; return null; }
            if (targetType == typeof(int)) { if (int.TryParse(num, out var v)) return v; return null; }
            if (targetType == typeof(uint)) { if (uint.TryParse(num, out var v)) return v; return null; }
            if (targetType == typeof(long)) { if (long.TryParse(num, out var v)) return v; return null; }
            if (targetType == typeof(ulong)) { if (ulong.TryParse(num, out var v)) return v; return null; }
            return null;  // 不支持的类型（float/对象引用等），让重载匹配继续找下一个
        }
    }
}
