namespace UnityAgentSkills.Utils
{
    /// <summary>
    /// 层级搜索查询语义.
    /// 承载归一化后的多关键词 OR 搜索输入与截断配置.
    /// </summary>
    internal sealed class HierarchySearchQuery
    {
        /// <summary>
        /// 初始化搜索查询.
        /// </summary>
        /// <param name="keywords">归一化后的关键词数组.</param>
        /// <param name="maxMatches">最大返回数.</param>
        public HierarchySearchQuery(string[] keywords, int maxMatches)
        {
            Keywords = keywords ?? System.Array.Empty<string>();
            MaxMatches = maxMatches;
        }

        /// <summary>
        /// 归一化后的关键词数组.
        /// </summary>
        public string[] Keywords { get; }

        /// <summary>
        /// 最大返回数.
        /// </summary>
        public int MaxMatches { get; }

        /// <summary>
        /// 是否存在可执行搜索条件.
        /// </summary>
        public bool HasSearch => Keywords.Length > 0;
    }
}
