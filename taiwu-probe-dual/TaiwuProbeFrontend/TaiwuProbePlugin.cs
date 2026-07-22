using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using GameData.Domains.Mod;
using GameData.Utilities;
using TaiwuModdingLib.Core.Plugin;
using UnityEngine;

namespace TaiwuProbeFrontend
{
    /// <summary>
    /// 调试探针 MOD 入口。游戏加载后在 Initialize 里启动 MCP HTTP server，
    /// Dispose 时停止。监听 localhost:13131/mcp，配置 taiwu-probe MCP 即可连。
    ///
    /// 主线程调度：Unity 的 UI/GameObject 操作必须在主线程。HTTP 请求在后台线程收到，
    /// 需要主线程的查询通过 MainThreadRunner 排到主线程执行（用 MonoBehaviour 的 Update 消费队列）。
    /// </summary>
    [PluginConfig("TaiwuProbe", "Magian", "0.4.0")]
    public class TaiwuProbePlugin : TaiwuRemakePlugin
    {
        #region 日志与状态

        /// <summary>日志标签，用于在 Player.log 中搜索过滤。固定为可读字符串，不与 ModIdStr 耦合。</summary>
        public const string LogTag = "TaiwuProbe";

        /// <summary>MCP HTTP 服务实例（后台线程处理请求，不阻塞主线程）。</summary>
        private McpHttpServer? _server;

        /// <summary>主线程执行器（MonoBehaviour，通过 Update 消费后台排队的委托）。</summary>
        private MainThreadRunner? _runner;

        /// <summary>太吾生成的 Mod 标识字符串（格式 "0_N"），用于 ModManager.GetSetting。</summary>
        internal static string _modIdStr = "";

        #endregion

        #region 生命周期

        /// <summary>
        /// MOD 初始化入口。游戏加载时由太吾框架调用。
        ///
        /// 流程：
        ///   1. 缓存 ModIdStr（后续 GetSetting 需要）
        ///   2. 从 Mod 设置读取端口号，非法值回退到 13131
        ///   3. 创建主线程执行器（MonoBehaviour）
        ///   4. 启动 MCP HTTP server
        /// </summary>
        public override void Initialize()
        {
            try
            {
                _modIdStr = ModIdStr;

                // 读取端口设置，默认 13131；非法输入（非数字 / 超出端口范围）时回退
                string portStr = GetSettingString("Port", "13131");
                if (!int.TryParse(portStr, out int port) || port < 1 || port > 65535)
                    port = 13131;

                _runner = MainThreadRunner.Create();
                FrontendLogBuffer.Start();
                _server = new McpHttpServer($"http://localhost:{port}/");
                _server.Start();
                AdaptableLog.Info($"[{LogTag}] 调试探针已启动，监听 http://localhost:{port}/mcp");
            }
            catch (Exception ex)
            {
                AdaptableLog.Info($"[{LogTag}] 调试探针启动失败: {ex.Message}");
            }
        }

        /// <summary>
        /// MOD 卸载入口。停止 HTTP 服务并释放主线程执行器。
        /// Harmony patch 由游戏进程自动回收，无需手动 UnpatchAll。
        /// </summary>
        public override void Dispose()
        {
            _server?.Stop();
            _server = null;
            FrontendLogBuffer.Stop();
            _runner?.Dispose();
            _runner = null;
        }

        #endregion

        #region 设置读取

        /// <summary>
        /// 读取 string 类型的 Mod 设置项。
        /// 读不到时返回 defaultValue，保证功能可用。
        /// </summary>
        /// <param name="key">设置项键名，对应 Config.lua → DefaultSettings 中的 Key</param>
        /// <param name="defaultValue">读不到时的默认值</param>
        /// <returns>设置值，或 defaultValue</returns>
        internal static string GetSettingString(string key, string defaultValue)
        {
            try
            {
                string val = defaultValue;
                return ModManager.GetSetting(_modIdStr, key, ref val) ? val : defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        #endregion
    }

    /// <summary>
    /// 主线程执行器：用 MonoBehaviour 的 Update 每帧消费后台线程排入的委托。
    /// 这样 taiwu_eval 查 Unity 对象时能在主线程执行，绕过 Unity 的主线程限制。
    ///
    /// 实现为单例（通过 GameObject 挂载），DontDestroyOnLoad 保证跨场景存活。
    /// </summary>
    internal sealed class MainThreadRunner : MonoBehaviour
    {
        private static MainThreadRunner? _instance;

        internal static bool IsAvailable => _instance != null;

        /// <summary>后台线程安全队列，HTTP 后台线程入队，主线程 Update 出队执行。</summary>
        private readonly ConcurrentQueue<Action> _queue = new();

        /// <summary>
        /// 在主线程执行一个委托。后台线程调用安全。
        /// 将 action 入队后由 Update 在下一帧取出执行。
        /// </summary>
        public static void RunOnMainThread(Action action)
        {
            MainThreadRunner? instance = _instance;
            instance?._queue.Enqueue(action);
        }

        /// <summary>
        /// 在主线程启动协程。截图、等待稳定帧和组合验证通过这一条内部 seam 完成，
        /// HTTP 调用方不需要了解 Unity 的帧生命周期。
        /// </summary>
        public static bool RunCoroutine(IEnumerator routine)
        {
            MainThreadRunner? instance = _instance;
            if (instance == null) return false;
            instance._queue.Enqueue(() => instance.StartCoroutine(routine));
            return true;
        }

        /// <summary>
        /// 创建单例 GameObject 并挂载本组件，启动每帧消费。
        /// 已存在时直接返回现有实例（线程安全由调用方保证）。
        /// </summary>
        public static MainThreadRunner Create()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("[TaiwuProbe-MainThreadRunner]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<MainThreadRunner>();
            return _instance;
        }

        /// <summary>每帧消费队列中所有待执行委托。后台线程入队后下一帧被执行。</summary>
        private void Update()
        {
            while (_queue.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception ex) { AdaptableLog.Info($"[TaiwuProbe] 主线程执行异常: {ex.Message}"); }
            }
        }

        /// <summary>GameObject 销毁时清理实例引用，避免悬挂指针。</summary>
        private void OnDestroy()
        {
            _instance = null;
        }

        /// <summary>销毁挂载的 GameObject，释放资源。</summary>
        internal void DisposeInternal()
        {
            if (gameObject != null) UnityEngine.Object.Destroy(gameObject);
        }

        // 供外部 Dispose 调用（与 IDisposable 接口兼容）
        public void Dispose() => DisposeInternal();
    }
}
