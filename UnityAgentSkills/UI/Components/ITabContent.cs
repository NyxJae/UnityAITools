using UnityEngine;

namespace UnityAgentSkills.UI.Components
{
    /// <summary>
    /// Tab内容接口,所有tab页面都必须实现此接口.
    /// </summary>
    public interface ITabContent
    {
        /// <summary>
        /// Tab的显示名称.
        /// </summary>
        string TabName { get; }

        /// <summary>
        /// 绘制tab内容.
        /// </summary>
        void OnGUI();
    }
}
