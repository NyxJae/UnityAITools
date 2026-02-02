using UnityEditor;
using UnityEngine;

namespace UnityAgentSkills.AutoCompile
{
    /// <summary>
    /// AutoCompile 服务启动器 - 后台自动编译服务.
    /// 在外部编辑器修改代码后,如果Unity编辑器失去焦点,自动触发编译.
    /// </summary>
    [InitializeOnLoad]
    public class AutoCompileService
    {
        static AutoCompileService()
        {
            // Unity 编辑器启动时自动初始化服务
            var config = AutoCompileConfigProvider.LoadConfig();
            AutoCompileController.Initialize(config);
        }

        /// <summary>
        /// 手动重启服务（用于配置更改后）.
        /// </summary>
        public static void Restart()
        {
            AutoCompileController.Shutdown();
            var config = AutoCompileConfigProvider.LoadConfig();
            AutoCompileController.Initialize(config);
        }
    }
}
