using System.Reflection;

namespace UnityAgentSkills.Plugins.EditorAction.Catalog
{
    /// <summary>
    /// EditorAction 动作描述符.
    /// </summary>
    internal sealed class EditorActionDescriptor
    {
        /// <summary>
        /// 动作标识,固定格式 Namespace.ClassName.MethodName.
        /// </summary>
        public string ActionId { get; set; }

        /// <summary>
        /// 目标方法信息.
        /// </summary>
        public MethodInfo Method { get; set; }

        /// <summary>
        /// 方法参数列表.
        /// </summary>
        public ParameterInfo[] Parameters { get; set; }
    }
}
