using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TaiwuProbeFrontend
{
    /// <summary>
    /// 大地图移动工具（taiwu_move / taiwu_map_info）。通过反射访问游戏移动系统。
    /// 在主线程执行（由 JsonRpc 的 EvalOnMainThread 保证）。
    /// </summary>
    internal static class UiTools
    {
        #region 大地图移动 —— 反射访问游戏移动系统

        /// <summary>
        /// 大地图移动相关常量与缓存类型，避免硬编码魔法字符串。
        /// 类型定义在游戏程序集，通过反射按全名加载，这里集中维护全名字符串。
        /// </summary>
        private static class MapMoveTypes
        {
            public const string WorldMapModel = "WorldMapModel";
            public const string ViewWorldMap = "Game.Views.Map.ViewWorldMap";
            public const string MapBlockData = "GameData.Domains.Map.MapBlockData";
            public const string MoveDirection = "UnityEngine.EventSystems.MoveDirection";
            public const string SingletonObject = "SingletonObject";
            public const string Location = "GameData.Domains.Map.Location";
        }

        /// <summary>
        /// 按方向在大地图上移动一步。方向不区分大小写。
        /// 内部反射链：拿 WorldMapModel 单例 → 当前地块 → GetNeighbor 算目标 →
        /// ViewWorldMap.MoveToBlock 触发原生寻路移动。
        /// 必须在主线程执行（由 JsonRpc 的 EvalOnMainThread 保证）。
        /// </summary>
        /// <param name="direction">up / down / left / right（大小写不敏感）</param>
        /// <returns>移动结果文本，含起点→终点 blockId 和方向。</returns>
        public static string MoveByDirection(string direction)
        {
            if (string.IsNullOrWhiteSpace(direction))
                return "用法：MoveByDirection(up|down|left|right)";

            string dir = direction.Trim().ToLowerInvariant();
            // MoveDirection 枚举值：Left=0, Up=1, Right=2, Down=3（Unity 内置枚举）
            int dirValue = dir switch
            {
                "left" => 0,
                "up" => 1,
                "right" => 2,
                "down" => 3,
                _ => -1,
            };
            if (dirValue < 0)
                return $"未知方向 \"{direction}\"，支持：up / down / left / right";

            try
            {
                // 1. 拿 WorldMapModel 单例（通过泛型 SingletonObject.getInstance<T>()）
                object? mapModel = GetWorldMapModel();
                if (mapModel == null)
                    return "无法获取 WorldMapModel 单例（游戏可能未进入大地图）";

                Type modelType = mapModel.GetType();

                // 2. 检查移动状态：TaiwuMoveState 必须是 Idle 才能移动
                object moveState = GetPropertyOrField(modelType, mapModel, "TaiwuMoveState");
                if (moveState != null && moveState.ToString() != "Idle")
                    return $"当前正在移动中（{moveState}），请等待到达后再试";

                // 3. 拿当前地块 MapBlockData
                object? currentBlock = GetPropertyOrField(modelType, mapModel, "CurrentBlockData");
                if (currentBlock == null)
                    return "无法获取当前地块数据";

                // 4. GetNeighbor(currentBlock, MoveDirection.<dir>, needPassable=true)
                Type blockType = currentBlock.GetType();
                Type moveDirType = GetTypeByFullName(MapMoveTypes.MoveDirection);
                if (moveDirType == null) return "未找到 MoveDirection 类型";

                object dirEnum = Enum.ToObject(moveDirType, dirValue);
                object? neighbor = InvokeMethod(
                    modelType, mapModel, "GetNeighbor",
                    new[] { blockType, moveDirType, typeof(bool) },
                    new object[] { currentBlock, dirEnum, true });
                if (neighbor == null)
                    return $"{dir} 方向越界或地块不可通行（MoveCost < 0）";

                // 5. 取目标 blockId，调 ViewWorldMap.MoveToBlock(short)
                object? targetBlockIdObj = GetPropertyOrField(blockType, neighbor, "BlockId");
                if (targetBlockIdObj == null) return "目标地块无 BlockId 字段";
                short targetBlockId = Convert.ToInt16(targetBlockIdObj);

                object? currentBlockIdObj = GetPropertyOrField(blockType, currentBlock, "BlockId");
                short currentBlockId = currentBlockIdObj != null ? Convert.ToInt16(currentBlockIdObj) : (short)-1;

                // 6. 拿 ViewWorldMap 实例并调 MoveToBlock
                object? viewWorldMap = FindViewWorldMapInstance();
                if (viewWorldMap == null)
                    return "未找到 ViewWorldMap 实例（不在大地图界面）";

                InvokeMethod(
                    viewWorldMap.GetType(), viewWorldMap, "MoveToBlock",
                    new[] { typeof(short) },
                    new object[] { targetBlockId });

                return $"已触发移动：{dir}，地块 {currentBlockId} → {targetBlockId}（原生寻路，走到为止）";
            }
            catch (Exception ex)
            {
                return $"移动异常: {ex.Message}";
            }
        }

        /// <summary>
        /// 查询当前大地图位置信息和四个方向的可达性。
        /// 返回：AreaId/BlockId、移动状态、IsMoveBanned、四方向是否可走 + 目标 blockId。
        /// 供 AI 决策"往哪走"，无需多次发 eval 探查。
        /// 必须在主线程执行。
        /// </summary>
        public static string GetMapInfo()
        {
            try
            {
                object? mapModel = GetWorldMapModel();
                if (mapModel == null)
                    return "无法获取 WorldMapModel 单例（游戏可能未进入大地图）";
                Type modelType = mapModel.GetType();

                var sb = new StringBuilder();
                sb.AppendLine($"AreaId: {GetPropertyOrField(modelType, mapModel, "CurrentAreaId")}");
                sb.AppendLine($"BlockId: {GetPropertyOrField(modelType, mapModel, "CurrentBlockId")}");
                sb.AppendLine($"MoveState: {GetPropertyOrField(modelType, mapModel, "TaiwuMoveState")}");
                sb.AppendLine($"IsMoveBanned: {GetPropertyOrField(modelType, mapModel, "IsMoveBanned")}");

                // 四方向可达性探测
                object? currentBlock = GetPropertyOrField(modelType, mapModel, "CurrentBlockData");
                if (currentBlock == null)
                {
                    sb.AppendLine("（无法获取当前地块，跳过方向探测）");
                    return sb.ToString().TrimEnd();
                }

                Type blockType = currentBlock.GetType();
                Type moveDirType = GetTypeByFullName(MapMoveTypes.MoveDirection);
                string[] dirNames = { "up", "down", "left", "right" };
                int[] dirValues = { 1, 3, 0, 2 };  // Up=1, Down=3, Left=0, Right=2
                sb.AppendLine("可去方向：");
                for (int i = 0; i < dirNames.Length; i++)
                {
                    object dirEnum = Enum.ToObject(moveDirType, dirValues[i]);
                    object? neighbor = InvokeMethod(
                        modelType, mapModel, "GetNeighbor",
                        new[] { blockType, moveDirType, typeof(bool) },
                        new object[] { currentBlock, dirEnum, true });
                    if (neighbor != null)
                    {
                        var nbId = GetPropertyOrField(blockType, neighbor, "BlockId");
                        sb.AppendLine($"  {dirNames[i]}: 可达 → blockId {nbId}");
                    }
                    else
                    {
                        sb.AppendLine($"  {dirNames[i]}: 越界或不可通行");
                    }
                }
                return sb.ToString().TrimEnd();
            }
            catch (Exception ex)
            {
                return $"查询地图信息异常: {ex.Message}";
            }
        }

        /// <summary>
        /// 获取已存在的 WorldMapModel 单例实例。
        /// ★ 不能调用 SingletonObject.getInstance&lt;T&gt;()！该方法在实例不存在时会 new + Init()，
        /// 而探针若在游戏自己初始化前（如主菜单）触发它，会导致 WorldMapModel.Init() 提前执行、
        /// 数据监控注册时机错误，引发后端 GameData 进程 NullReferenceException 崩溃。
        /// 这里改为直接读 SingletonMap 字典里**已存在**的实例，不存在则返回 null（表示游戏尚未进入大地图）。
        /// </summary>
        private static object? GetWorldMapModel()
        {
            Type? singletonType = GetTypeByFullName(MapMoveTypes.SingletonObject);
            Type? modelType = GetTypeByFullName(MapMoveTypes.WorldMapModel);
            if (singletonType == null || modelType == null) return null;

            // 直接读静态字段 SingletonMap（private，用 NonPublic 反射读取）
            var field = singletonType.GetField("SingletonMap",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (field == null) return null;
            var dict = field.GetValue(null) as System.Collections.IDictionary;
            if (dict == null) return null;
            return dict.Contains(modelType) ? dict[modelType] : null;
        }

        /// <summary>
        /// 通过 GameObject.Find 找 ViewWorldMap 节点并 GetComponent 拿实例。
        /// 返回 Game.Views.Map.ViewWorldMap 实例，不在大地图界面时返回 null。
        /// </summary>
        private static object? FindViewWorldMapInstance()
        {
            GameObject? go = GameObject.Find("LayerBack/ViewWorldMap");
            if (go == null) return null;
            var comp = go.GetComponent(MapMoveTypes.ViewWorldMap);
            return comp;
        }

        /// <summary>读取实例的属性或字段（属性优先，找不到回退字段）。</summary>
        private static object? GetPropertyOrField(Type type, object instance, string name)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var prop = type.GetProperty(name, flags);
            if (prop != null && prop.GetIndexParameters().Length == 0)
                return prop.GetValue(instance);
            var field = type.GetField(name, flags);
            return field?.GetValue(instance);
        }

        /// <summary>
        /// 反射调用方法。先尝试精确参数类型匹配，找不到则尝试按方法名+参数数量模糊匹配。
        /// 用 NonPublic 标志，覆盖 internal/private 方法。
        /// </summary>
        private static object? InvokeMethod(Type type, object? instance, string name,
            Type[] paramTypes, object[] args)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.Instance | BindingFlags.Static;
            var method = type.GetMethod(name, flags, null, paramTypes, null);
            if (method != null) return method.Invoke(instance, args);
            // 回退：按方法名 + 参数数量找第一个匹配（处理 ref/out 参数签名差异）
            foreach (var m in type.GetMethods(flags))
            {
                if (m.Name == name && m.GetParameters().Length == args.Length)
                    return m.Invoke(instance, args);
            }
            throw new MissingMethodException(type.Name, name);
        }

        /// <summary>按全名加载类型，缓存结果避免重复反射搜索。</summary>
        private static readonly System.Collections.Generic.Dictionary<string, Type?> _typeCache = new();
        private static Type? GetTypeByFullName(string fullName)
        {
            if (_typeCache.TryGetValue(fullName, out var cached)) return cached;
            Type? t = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(fullName);
                if (t != null) break;
            }
            _typeCache[fullName] = t;
            return t;
        }

        #endregion
    }
}
