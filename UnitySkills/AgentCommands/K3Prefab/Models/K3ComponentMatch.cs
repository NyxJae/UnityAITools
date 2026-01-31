using UnityEngine;
using K3Engine.Component.Interfaces;

namespace AgentCommands.K3Prefab.Models
{
    /// <summary>
    /// K3组件匹配结果模型
    /// </summary>
    public class K3ComponentMatch
    {
        /// <summary>
        /// 索引（从0开始），用于区分多个匹配项
        /// </summary>
        public int index { get; set; }

        /// <summary>
        /// K3组件实例
        /// </summary>
        public IK3Component component { get; set; }

        /// <summary>
        /// 容器GameObject（K3DialogEx或K3Panel）
        /// </summary>
        public GameObject container { get; set; }

        /// <summary>
        /// 组件所属的GameObject
        /// </summary>
        public GameObject gameObject { get; set; }

        /// <summary>
        /// 容器类型（"K3DialogEx" 或 "K3Panel"）
        /// </summary>
        public string containerType { get; set; }
    }
}
