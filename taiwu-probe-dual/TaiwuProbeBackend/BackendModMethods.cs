using System;
using GameData.Common;
using GameData.Domains;
using GameData.Domains.Mod;
using GameData.Serializer;

namespace TaiwuProbeBackend;

internal static class BackendModMethods
{
    internal const string ExecuteMethod = "ExecuteProbeTool";

    internal static void Register(string modId)
    {
        DomainManager.Mod.AddModMethod(
            modId,
            ExecuteMethod,
            (Func<DataContext, SerializableModData, SerializableModData>)Execute);
    }

    private static SerializableModData Execute(DataContext context, SerializableModData parameter)
    {
        var response = new SerializableModData();
        try
        {
            string tool = parameter.Get("Tool", out string toolValue) ? toolValue : string.Empty;
            string argumentsJson = parameter.Get("ArgumentsJson", out string argsValue) ? argsValue : "{}";
            string text = BackendTools.Execute(context, tool, argumentsJson);
            response.Set("Success", true);
            response.Set("Text", text);
        }
        catch (Exception ex)
        {
            response.Set("Success", false);
            response.Set("Text", $"<后端工具异常: {ex.GetType().Name}: {ex.Message}>");
        }
        return response;
    }
}
