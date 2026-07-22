using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using GameData.Common;
using GameData.Domains;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TaiwuProbeBackend;

internal static class BackendTools
{
    private const int DefaultLimit = 100;
    private const int MaxLimit = 500;

    internal static string Execute(DataContext context, string tool, string argumentsJson)
    {
        JObject args = string.IsNullOrWhiteSpace(argumentsJson) ? new JObject() : JObject.Parse(argumentsJson);
        return tool switch
        {
            "taiwu_backend_ping" => Ping(),
            "taiwu_backend_eval" => Eval(context, args["expression"]?.Value<string>() ?? string.Empty),
            "taiwu_backend_type_search" => SearchTypes(
                args["query"]?.Value<string>() ?? string.Empty,
                GetLimit(args)),
            "taiwu_backend_members" => ListMembers(
                args["type"]?.Value<string>() ?? string.Empty,
                args["filter"]?.Value<string>(),
                args["include_nonpublic"]?.Value<bool>() ?? false,
                GetLimit(args)),
            "taiwu_backend_log_tail" => TailLog(
                args["contains"]?.Value<string>(),
                GetLimit(args)),
            "taiwu_backend_csharp" => ExecuteCSharp(context, args),
            _ => throw new ArgumentException("未知后端工具: " + tool)
        };
    }

    private static string ExecuteCSharp(DataContext context, JObject args)
    {
        if (!BackendPlugin.UnsafeCSharpEnabled)
            return "<任意 C# 执行已在 Mod 设置中禁用>";
        string code = args["code"]?.Value<string>() ?? string.Empty;
        IEnumerable<string> namespaces = (args["usings"] as JArray)?.Values<string>()
            .Where(value => value != null)
            .Select(value => value!) ?? Array.Empty<string>();
        return CSharpQueryRunner.Execute(context, code, namespaces);
    }

    internal static string FormatExternalResult(object? value)
    {
        if (value == null) return "null";
        Type type = value.GetType();
        if (type.Assembly.GetName().Name?.StartsWith("TaiwuProbe.Dynamic.", StringComparison.Ordinal) == true ||
            value is IDictionary)
        {
            try
            {
                return JsonConvert.SerializeObject(value, Formatting.Indented, new JsonSerializerSettings
                {
                    MaxDepth = 8,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    Error = (_, args) => args.ErrorContext.Handled = true
                });
            }
            catch (Exception ex)
            {
                return $"[{type.FullName}] <JSON 格式化失败: {ex.Message}> {value}";
            }
        }
        return FormatValue(value, type, 0);
    }

    private static int GetLimit(JObject args)
    {
        int limit = args["limit"]?.Value<int>() ?? DefaultLimit;
        return Math.Clamp(limit, 1, MaxLimit);
    }

    private static string Ping()
    {
        bool initialized = GameData.GameDataBridge.GameDataBridge.IsGameDataModuleInitialized();
        int threadId = Thread.CurrentThread.ManagedThreadId;
        bool isMainThread = DataContextManager.IsMainThread(threadId);
        string version;
        try { version = DomainManager.Global.GetGameVersion(); }
        catch (Exception ex) { version = "<读取失败: " + ex.Message + ">"; }

        return string.Join("\n", new[]
        {
            "pong backend " + DateTime.Now.ToString("HH:mm:ss"),
            "ProcessId: " + Process.GetCurrentProcess().Id,
            "ProcessName: " + Process.GetCurrentProcess().ProcessName,
            "GameVersion: " + version,
            "GameDataInitialized: " + initialized,
            "ThreadId: " + threadId,
            "IsGameDataMainThread: " + isMainThread,
            "DomainCount: " + DomainManager.Domains.Length
        });
    }

    private static string Eval(DataContext context, string expression)
    {
        expression = (expression ?? string.Empty).Trim();
        if (expression.Length == 0)
            return "用法：type:完整类型名[,member:字段/属性/安全查询方法()]...";
        if (expression.IndexOf("value:", StringComparison.OrdinalIgnoreCase) >= 0)
            return "<后端 eval 当前为只读模式，不支持 value: 写入>";

        List<string> parts = SplitExpression(expression);
        string? typeName = parts.FirstOrDefault(p => p.StartsWith("type:", StringComparison.OrdinalIgnoreCase))?[5..].Trim();
        if (string.IsNullOrWhiteSpace(typeName))
            return "<缺少 type:完整类型名>";

        Type? type = FindType(typeName);
        if (type == null)
            return "<未找到后端类型: " + typeName + ">";

        List<string> members = parts
            .Where(p => p.StartsWith("member:", StringComparison.OrdinalIgnoreCase))
            .Select(p => p[7..].Trim())
            .Where(p => p.Length > 0)
            .ToList();

        if (members.Count == 0)
            return DescribeType(type, includeNonPublic: false, limit: 80);

        object? current = null;
        Type currentType = type;
        foreach (string member in members)
        {
            (current, currentType) = ReadMember(context, current, currentType, member);
            if (current == null)
                return "null";
        }
        return FormatValue(current!, currentType, 0);
    }

    private static (object? Value, Type Type) ReadMember(DataContext context, object? target, Type type, string token)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic |
                                   BindingFlags.Static | BindingFlags.Instance |
                                   BindingFlags.FlattenHierarchy;

        int colon = token.LastIndexOf(':');
        if (colon > 0 && int.TryParse(token[(colon + 1)..], out int index))
        {
            string parent = token[..colon];
            (object? collection, Type collectionType) = ReadMember(context, target, type, parent);
            if (collection is IList list)
            {
                if (index < 0 || index >= list.Count) throw new IndexOutOfRangeException($"索引 {index} 越界，Count={list.Count}");
                object? item = list[index];
                return (item, item?.GetType() ?? GetElementType(collectionType));
            }
            if (collection is Array array)
            {
                if (index < 0 || index >= array.Length) throw new IndexOutOfRangeException($"索引 {index} 越界，Length={array.Length}");
                object? item = array.GetValue(index);
                return (item, item?.GetType() ?? collectionType.GetElementType() ?? typeof(object));
            }
            throw new InvalidOperationException(parent + " 不是可按索引访问的列表/数组");
        }

        int openParen = token.IndexOf('(');
        bool isMethod = openParen > 0 && token.EndsWith(")", StringComparison.Ordinal);
        string name = isMethod ? token[..openParen].Trim() : token;
        if (isMethod)
        {
            if (!IsSafeQueryMethod(name))
                throw new InvalidOperationException("只读模式拒绝调用非查询方法: " + name);
            List<string> argumentTexts = SplitArguments(token[(openParen + 1)..^1]);
            var matches = new List<(MethodInfo Method, object?[] Arguments, int Score)>();
            var candidates = type.GetMethods(Flags)
                .Where(m => m.Name == name && !m.IsGenericMethodDefinition && !m.GetParameters().Any(p => p.ParameterType.IsByRef))
                .ToList();
            foreach (MethodInfo candidate in candidates)
            {
                if (TryBuildArguments(context, candidate.GetParameters(), argumentTexts, out object?[] invokeArguments, out int score))
                    matches.Add((candidate, invokeArguments, score));
            }
            if (matches.Count == 0)
            {
                string signatures = candidates.Count == 0
                    ? "<无同名方法>"
                    : string.Join("; ", candidates.Take(8).Select(FormatSignature));
                throw new MissingMethodException($"{type.FullName}.{token} 没有可匹配的只读重载。候选: {signatures}");
            }
            int bestScore = matches.Min(m => m.Score);
            List<(MethodInfo Method, object?[] Arguments, int Score)> best = matches.Where(m => m.Score == bestScore).ToList();
            if (best.Count > 1)
                throw new AmbiguousMatchException("参数可匹配多个重载: " + string.Join("; ", best.Select(m => FormatSignature(m.Method))));
            MethodInfo method = best[0].Method;
            object?[] invokeArgs = best[0].Arguments;
            object? value = method.Invoke(method.IsStatic ? null : target, invokeArgs);
            return (value, value?.GetType() ?? method.ReturnType);
        }

        FieldInfo? field = type.GetField(name, Flags);
        if (field != null)
        {
            object? value = field.GetValue(field.IsStatic ? null : target);
            return (value, value?.GetType() ?? field.FieldType);
        }

        PropertyInfo? property = type.GetProperty(name, Flags);
        if (property != null && property.GetIndexParameters().Length == 0 && property.GetMethod != null)
        {
            object? value = property.GetValue(property.GetMethod.IsStatic ? null : target);
            return (value, value?.GetType() ?? property.PropertyType);
        }

        throw new MissingMemberException(type.FullName, name);
    }

    private static bool IsSafeQueryMethod(string name)
    {
        string[] prefixes = { "Get", "Is", "Can", "Has", "Contains", "ToString" };
        return prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static bool TryBuildArguments(
        DataContext context,
        ParameterInfo[] parameters,
        IReadOnlyList<string> argumentTexts,
        out object?[] arguments,
        out int score)
    {
        arguments = new object?[parameters.Length];
        score = 0;
        int inputIndex = 0;
        for (int i = 0; i < parameters.Length; i++)
        {
            ParameterInfo parameter = parameters[i];
            if (parameter.ParameterType == typeof(DataContext))
            {
                arguments[i] = context;
                continue;
            }
            if (inputIndex < argumentTexts.Count)
            {
                if (!TryConvertArgument(argumentTexts[inputIndex], parameter.ParameterType, out object? converted, out int conversionScore))
                    return false;
                arguments[i] = converted;
                score += conversionScore;
                inputIndex++;
                continue;
            }
            if (parameter.HasDefaultValue)
            {
                arguments[i] = parameter.DefaultValue;
                score += 10;
                continue;
            }
            return false;
        }
        return inputIndex == argumentTexts.Count;
    }

    private static bool TryConvertArgument(string text, Type parameterType, out object? value, out int score)
    {
        value = null;
        score = 0;
        string trimmed = text.Trim();
        Type? nullableType = Nullable.GetUnderlyingType(parameterType);
        Type targetType = nullableType ?? parameterType;

        if (string.Equals(trimmed, "null", StringComparison.OrdinalIgnoreCase))
            return nullableType != null || !parameterType.IsValueType;

        if (targetType == typeof(string))
        {
            if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
            {
                try { value = JsonConvert.DeserializeObject<string>(trimmed); return value != null; }
                catch { return false; }
            }
            if (trimmed.Length >= 2 && trimmed[0] == '\'' && trimmed[^1] == '\'')
            {
                value = trimmed[1..^1].Replace("\\'", "'").Replace("\\\\", "\\");
                return true;
            }
            value = trimmed;
            score = 2;
            return true;
        }
        if (targetType == typeof(bool))
        {
            if (bool.TryParse(trimmed, out bool boolean)) { value = boolean; return true; }
            if (trimmed == "1" || trimmed == "0") { value = trimmed == "1"; score = 1; return true; }
            return false;
        }
        if (targetType == typeof(char))
        {
            string unquoted = trimmed.Trim('\'', '"');
            if (unquoted.Length == 1) { value = unquoted[0]; return true; }
            return false;
        }
        if (targetType.IsEnum)
        {
            try { value = Enum.Parse(targetType, trimmed.Trim('\'', '"'), ignoreCase: true); return true; }
            catch { }
            if (long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out long enumNumber))
            {
                value = Enum.ToObject(targetType, enumNumber);
                score = 2;
                return true;
            }
            return false;
        }
        if (targetType == typeof(Guid))
        {
            if (Guid.TryParse(trimmed.Trim('\'', '"'), out Guid guid)) { value = guid; return true; }
            return false;
        }

        NumberStyles integerStyle = NumberStyles.Integer;
        NumberStyles floatStyle = NumberStyles.Float;
        IFormatProvider culture = CultureInfo.InvariantCulture;
        if (targetType == typeof(int) && int.TryParse(trimmed, integerStyle, culture, out int intValue)) { value = intValue; return true; }
        if (targetType == typeof(long) && long.TryParse(trimmed, integerStyle, culture, out long longValue)) { value = longValue; score = 1; return true; }
        if (targetType == typeof(short) && short.TryParse(trimmed, integerStyle, culture, out short shortValue)) { value = shortValue; score = 1; return true; }
        if (targetType == typeof(sbyte) && sbyte.TryParse(trimmed, integerStyle, culture, out sbyte sbyteValue)) { value = sbyteValue; score = 1; return true; }
        if (targetType == typeof(uint) && uint.TryParse(trimmed, integerStyle, culture, out uint uintValue)) { value = uintValue; score = 1; return true; }
        if (targetType == typeof(ulong) && ulong.TryParse(trimmed, integerStyle, culture, out ulong ulongValue)) { value = ulongValue; score = 1; return true; }
        if (targetType == typeof(ushort) && ushort.TryParse(trimmed, integerStyle, culture, out ushort ushortValue)) { value = ushortValue; score = 1; return true; }
        if (targetType == typeof(byte) && byte.TryParse(trimmed, integerStyle, culture, out byte byteValue)) { value = byteValue; score = 1; return true; }
        if (targetType == typeof(double) && double.TryParse(trimmed, floatStyle, culture, out double doubleValue)) { value = doubleValue; score = 2; return true; }
        if (targetType == typeof(float) && float.TryParse(trimmed, floatStyle, culture, out float floatValue)) { value = floatValue; score = 2; return true; }
        if (targetType == typeof(decimal) && decimal.TryParse(trimmed, floatStyle, culture, out decimal decimalValue)) { value = decimalValue; score = 2; return true; }
        return false;
    }

    private static List<string> SplitArguments(string argumentsText)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(argumentsText)) return result;
        int start = 0;
        int depth = 0;
        bool inString = false;
        bool escaped = false;
        char quote = '\0';
        for (int i = 0; i < argumentsText.Length; i++)
        {
            char c = argumentsText[i];
            if (inString)
            {
                if (escaped) { escaped = false; continue; }
                if (c == '\\') { escaped = true; continue; }
                if (c == quote) inString = false;
                continue;
            }
            if (c == '"' || c == '\'') { inString = true; quote = c; continue; }
            if (c is '(' or '[' or '{') depth++;
            else if (c is ')' or ']' or '}') depth--;
            else if (c == ',' && depth == 0)
            {
                result.Add(argumentsText[start..i].Trim());
                start = i + 1;
            }
        }
        if (inString) throw new FormatException("方法参数字符串缺少结束引号");
        result.Add(argumentsText[start..].Trim());
        return result;
    }

    private static string FormatSignature(MethodInfo method)
    {
        string parameters = string.Join(", ", method.GetParameters().Select(p =>
            FriendlyName(p.ParameterType) + " " + p.Name + (p.HasDefaultValue ? " = " + (p.DefaultValue ?? "null") : string.Empty)));
        return $"{FriendlyName(method.ReturnType)} {method.Name}({parameters})";
    }

    private static string SearchTypes(string query, int limit)
    {
        if (string.IsNullOrWhiteSpace(query)) return "<query 不能为空>";
        var matches = new List<string>();
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies().OrderBy(a => a.GetName().Name))
        {
            foreach (Type type in GetLoadableTypes(assembly))
            {
                string fullName = type.FullName ?? type.Name;
                if (fullName.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                matches.Add($"{fullName}  [{assembly.GetName().Name}]");
                if (matches.Count >= limit) break;
            }
            if (matches.Count >= limit) break;
        }
        return matches.Count == 0 ? "<没有匹配的后端类型>" : string.Join("\n", matches);
    }

    private static string ListMembers(string typeName, string? filter, bool includeNonPublic, int limit)
    {
        Type? type = FindType(typeName);
        if (type == null) return "<未找到后端类型: " + typeName + ">";
        return DescribeType(type, includeNonPublic, limit, filter);
    }

    private static string DescribeType(Type type, bool includeNonPublic, int limit, string? filter = null)
    {
        BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
        if (includeNonPublic) flags |= BindingFlags.NonPublic;
        bool Match(string name) => string.IsNullOrWhiteSpace(filter) || name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;

        var lines = new List<string>
        {
            "Type: " + type.FullName,
            "Assembly: " + type.Assembly.GetName().Name
        };
        foreach (FieldInfo f in type.GetFields(flags).Where(f => Match(f.Name)).OrderBy(f => f.Name))
            lines.Add($"[field] {Visibility(f)}{Static(f)}{FriendlyName(f.FieldType)} {f.Name}");
        foreach (PropertyInfo p in type.GetProperties(flags).Where(p => Match(p.Name)).OrderBy(p => p.Name))
            lines.Add($"[property] {Visibility(p.GetMethod ?? p.SetMethod)}{Static(p.GetMethod ?? p.SetMethod)}{FriendlyName(p.PropertyType)} {p.Name}");
        foreach (MethodInfo m in type.GetMethods(flags).Where(m => !m.IsSpecialName && Match(m.Name)).OrderBy(m => m.Name))
        {
            string parameters = string.Join(", ", m.GetParameters().Select(p => FriendlyName(p.ParameterType) + " " + p.Name));
            lines.Add($"[method] {Visibility(m)}{Static(m)}{FriendlyName(m.ReturnType)} {m.Name}({parameters})");
        }
        int total = lines.Count - 2;
        if (lines.Count > limit + 2) lines.RemoveRange(limit + 2, lines.Count - limit - 2);
        lines.Insert(2, $"Members: showing {Math.Min(total, limit)} / {total}");
        return string.Join("\n", lines);
    }

    private static string TailLog(string? contains, int limit)
    {
        string backendDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string gameDir = Directory.GetParent(backendDir)?.FullName ?? backendDir;
        string logDir = Path.Combine(gameDir, "Logs");
        FileInfo? latest = new DirectoryInfo(logDir).Exists
            ? new DirectoryInfo(logDir).GetFiles("GameData_*.log").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault()
            : null;
        if (latest == null) return "<未找到 GameData 日志: " + logDir + ">";

        string[] lines;
        using (var stream = new FileStream(latest.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true))
            lines = reader.ReadToEnd().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        IEnumerable<string> selected = lines;
        if (!string.IsNullOrWhiteSpace(contains))
            selected = selected.Where(line => line.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0);
        string[] tail = selected.TakeLast(limit).ToArray();
        return $"Log: {latest.FullName}\nLines: {tail.Length}\n" + string.Join("\n", tail);
    }

    private static Type? FindType(string fullName)
    {
        Type? direct = Type.GetType(fullName, throwOnError: false, ignoreCase: false);
        if (direct != null) return direct;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type? type = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);
            if (type != null) return type;
        }
        return null;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
        catch { return Array.Empty<Type>(); }
    }

    private static List<string> SplitExpression(string expression)
    {
        var result = new List<string>();
        int start = 0;
        int depth = 0;
        for (int i = 0; i < expression.Length; i++)
        {
            if (expression[i] == '(') depth++;
            else if (expression[i] == ')') depth--;
            else if (expression[i] == ',' && depth == 0)
            {
                result.Add(expression[start..i].Trim());
                start = i + 1;
            }
        }
        result.Add(expression[start..].Trim());
        return result;
    }

    private static string FormatValue(object value, Type type, int depth)
    {
        if (value is string s) return "\"" + s + "\"";
        if (type.IsPrimitive || type.IsEnum || value is decimal || value is DateTime || value is Guid)
            return value.ToString() ?? string.Empty;
        if (value is IDictionary dict)
        {
            var lines = new List<string> { $"[Dictionary Count={dict.Count}]" };
            int count = 0;
            foreach (DictionaryEntry entry in dict)
            {
                if (count++ >= 20) { lines.Add("..."); break; }
                lines.Add($"  {entry.Key}: {ShortValue(entry.Value)}");
            }
            return string.Join("\n", lines);
        }
        if (value is IEnumerable enumerable)
        {
            var lines = new List<string> { "[Enumerable]" };
            int count = 0;
            foreach (object? item in enumerable)
            {
                if (count++ >= 20) { lines.Add("..."); break; }
                lines.Add($"  [{count - 1}] {ShortValue(item)}");
            }
            return string.Join("\n", lines);
        }
        string text = value.ToString() ?? type.Name;
        return $"[{type.FullName}] {text}";
    }

    private static string ShortValue(object? value)
    {
        if (value == null) return "null";
        string text = value is string s ? "\"" + s + "\"" : value.ToString() ?? value.GetType().Name;
        return text.Length <= 240 ? text : text[..240] + "...";
    }

    private static Type GetElementType(Type collectionType) =>
        collectionType.IsGenericType ? collectionType.GetGenericArguments()[0] : typeof(object);

    private static string FriendlyName(Type type) => type.FullName ?? type.Name;
    private static string Visibility(MethodBase? method) => method?.IsPublic == true ? "public " : "nonpublic ";
    private static string Visibility(FieldInfo field) => field.IsPublic ? "public " : "nonpublic ";
    private static string Static(MethodBase? method) => method?.IsStatic == true ? "static " : string.Empty;
    private static string Static(FieldInfo field) => field.IsStatic ? "static " : string.Empty;
}
