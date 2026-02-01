namespace AgentCommands.Plugins.K3Prefab.Models
{
    /// <summary>
    /// K3组件属性修改请求模型
    /// </summary>
    public class K3PropertyModification
    {
        /// <summary>
        /// 属性名称
        /// </summary>
        public string property { get; set; }

        /// <summary>
        /// 期望的旧值（用于验证）
        /// </summary>
        public object oldValue { get; set; }

        /// <summary>
        /// 要修改的新值
        /// </summary>
        public object newValue { get; set; }
    }
}
