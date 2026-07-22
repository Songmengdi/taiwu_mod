using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.RegularExpressions;
using GameData.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TaiwuProbeBackend;

internal static class CSharpQueryRunner
{
    private const int MaxCodeLength = 100_000;
    private const int MaxOutputLength = 200_000;
    private const string GeneratedTypeName = "TaiwuProbe.Dynamic.Query";
    private static readonly Regex NamespacePattern = new(
        "^[A-Za-z_][A-Za-z0-9_]*(\\.[A-Za-z_][A-Za-z0-9_]*)*$",
        RegexOptions.CultureInvariant);

    private static readonly string[] DefaultUsings =
    {
        "System",
        "System.Collections",
        "System.Collections.Generic",
        "System.Linq",
        "System.Reflection",
        "GameData.Common",
        "GameData.Domains"
    };

    internal static string Execute(DataContext context, string code, IEnumerable<string> requestedUsings)
    {
        if (string.IsNullOrWhiteSpace(code)) return "<code 不能为空；代码必须通过 return 返回结果>";
        if (code.Length > MaxCodeLength) return $"<code 过长，最大 {MaxCodeLength} 字符>";

        string[] usings;
        try { usings = BuildUsings(requestedUsings); }
        catch (ArgumentException ex) { return "<无效 using: " + ex.Message + ">"; }

        string source = BuildSource(code, usings, out int wrapperLineCount);
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp12));
        IReadOnlyList<MetadataReference> references = BuildReferences();
        string assemblyName = "TaiwuProbe.Dynamic." + Guid.NewGuid().ToString("N");
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable));

        using var peStream = new MemoryStream();
        var emitResult = compilation.Emit(peStream);
        if (!emitResult.Success)
            return FormatDiagnostics(emitResult.Diagnostics, wrapperLineCount);

        peStream.Position = 0;
        var loadContext = new QueryLoadContext();
        try
        {
            Assembly assembly = loadContext.LoadFromStream(peStream);
            Type type = assembly.GetType(GeneratedTypeName, throwOnError: true)!;
            MethodInfo method = type.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static)!;
            var stopwatch = Stopwatch.StartNew();
            object? result;
            try
            {
                result = method.Invoke(null, new object?[] { context });
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                return $"<C# 运行异常: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}>";
            }
            stopwatch.Stop();
            string output = BackendTools.FormatExternalResult(result);
            if (output.Length > MaxOutputLength)
                output = output[..MaxOutputLength] + $"\n... <输出已截断，原长度 {output.Length}>";
            return output + $"\n\n[C# query elapsed: {stopwatch.Elapsed.TotalMilliseconds:F2} ms]";
        }
        finally
        {
            loadContext.Unload();
        }
    }

    private static string[] BuildUsings(IEnumerable<string> requested)
    {
        var result = new HashSet<string>(DefaultUsings, StringComparer.Ordinal);
        foreach (string? item in requested)
        {
            string value = item?.Trim() ?? string.Empty;
            if (!NamespacePattern.IsMatch(value))
                throw new ArgumentException(value.Length == 0 ? "命名空间为空" : value);
            result.Add(value);
        }
        return result.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    private static string BuildSource(string code, IEnumerable<string> usings, out int wrapperLineCount)
    {
        var builder = new StringBuilder();
        builder.AppendLine("#nullable enable");
        foreach (string item in usings) builder.Append("using ").Append(item).AppendLine(";");
        builder.AppendLine("namespace TaiwuProbe.Dynamic;");
        builder.AppendLine("public static class Query");
        builder.AppendLine("{");
        builder.AppendLine("    public static object? Execute(DataContext context)");
        builder.AppendLine("    {");
        wrapperLineCount = builder.ToString().Count(c => c == '\n');
        builder.AppendLine(code);
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static IReadOnlyList<MetadataReference> BuildReferences()
    {
        var references = new List<MetadataReference>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic || string.IsNullOrWhiteSpace(assembly.Location)) continue;
            string path = assembly.Location;
            if (!paths.Add(path)) continue;
            try { references.Add(MetadataReference.CreateFromFile(path)); }
            catch (BadImageFormatException) { }
            catch (FileNotFoundException) { }
        }
        return references;
    }

    private static string FormatDiagnostics(IEnumerable<Diagnostic> diagnostics, int wrapperLineCount)
    {
        var lines = new List<string> { "<C# 编译失败>" };
        foreach (Diagnostic diagnostic in diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(50))
        {
            FileLinePositionSpan span = diagnostic.Location.GetLineSpan();
            int line = Math.Max(1, span.StartLinePosition.Line + 1 - wrapperLineCount);
            int column = span.StartLinePosition.Character + 1;
            lines.Add($"({line},{column}) {diagnostic.Id}: {diagnostic.GetMessage()}");
        }
        return string.Join("\n", lines);
    }

    private sealed class QueryLoadContext : AssemblyLoadContext
    {
        internal QueryLoadContext() : base(isCollectible: true) { }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(
                assembly => AssemblyName.ReferenceMatchesDefinition(assembly.GetName(), assemblyName));
        }
    }
}
