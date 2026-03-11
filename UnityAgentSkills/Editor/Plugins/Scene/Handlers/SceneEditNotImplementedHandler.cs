using System;
using LitJson2_utf;

namespace UnityAgentSkills.Plugins.Scene.Handlers
{
    /// <summary>
    /// 场景编辑命令占位执行器.
    /// 在共享基础设施落地前,为注册骨架提供稳定的可编译入口.
    /// </summary>
    internal static class SceneEditNotImplementedHandler
    {
        /// <summary>
        /// 抛出未实现异常.
        /// </summary>
        /// <param name="commandType">命令类型.</param>
        /// <param name="rawParams">原始参数.</param>
        /// <returns>不会返回.</returns>
        public static JsonData Execute(string commandType, JsonData rawParams)
        {
            throw new NotSupportedException(commandType + " is not implemented yet.");
        }
    }
}
