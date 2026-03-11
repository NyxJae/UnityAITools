using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityAgentSkills.Utils
{
    /// <summary>
    /// 层级搜索语义共享规则.
    /// 统一处理关键词归一化,OR 匹配,稳定排序与截断统计.
    /// </summary>
    internal static class HierarchySearchSemantics
    {
        /// <summary>
        /// 默认最多返回的命中条数.
        /// </summary>
        public const int DefaultMaxMatches = 50;

        /// <summary>
        /// 将 `nameContains:string[]` 归一化为统一的 OR 搜索规则.
        /// </summary>
        /// <param name="nameContains">名称关键词数组.</param>
        /// <param name="keywordsData">附加关键词输入,当前应传 null.</param>
        /// <param name="maxMatches">最大返回数.</param>
        /// <returns>归一化后的搜索规则,无有效关键词时返回 null.</returns>
        public static HierarchySearchQuery BuildOrDefault(string nameContains, object keywordsData, int maxMatches)
        {
            string[] keywords = NormalizeKeywords(nameContains, keywordsData);
            if (keywords.Length == 0)
            {
                return null;
            }

            if (maxMatches <= 0)
            {
                throw new ArgumentException("maxMatches must be > 0", nameof(maxMatches));
            }

            return new HierarchySearchQuery(keywords, maxMatches);
        }

        /// <summary>
        /// 对关键词做 Trim,去空,去重,并保留首次出现顺序.
        /// 当前主协议只要求处理 `nameContains:string[]` 的 OR 语义.
        /// </summary>
        /// <param name="nameContains">名称关键词数组.</param>
        /// <param name="keywordsData">附加关键词输入,当前应传 null.</param>
        /// <returns>归一化后的关键词数组.</returns>
        public static string[] NormalizeKeywords(string nameContains, object keywordsData)
        {
            List<string> rawKeywords = new List<string>();
            AppendKeywords(rawKeywords, keywordsData);
            AppendKeywords(rawKeywords, nameContains);

            HashSet<string> uniqueKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> normalizedKeywords = new List<string>();
            foreach (string rawKeyword in rawKeywords)
            {
                string normalizedKeyword = (rawKeyword ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(normalizedKeyword))
                {
                    continue;
                }

                if (uniqueKeywords.Add(normalizedKeyword))
                {
                    normalizedKeywords.Add(normalizedKeyword);
                }
            }

            return normalizedKeywords.ToArray();
        }

        /// <summary>
        /// 判断名称是否命中任一关键词.
        /// </summary>
        /// <param name="name">目标名称.</param>
        /// <param name="query">搜索规则.</param>
        /// <returns>命中任一关键词时返回 true.</returns>
        public static bool IsMatch(string name, HierarchySearchQuery query)
        {
            if (query == null || query.Keywords.Length == 0 || string.IsNullOrEmpty(name))
            {
                return false;
            }

            for (int i = 0; i < query.Keywords.Length; i++)
            {
                if (name.IndexOf(query.Keywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 对命中结果做稳定排序.
        /// </summary>
        /// <param name="matches">原始命中列表.</param>
        /// <returns>稳定排序后的结果.</returns>
        public static List<HierarchyNode> SortMatches(IEnumerable<HierarchyNode> matches)
        {
            if (matches == null)
            {
                return new List<HierarchyNode>();
            }

            return matches
                .OrderBy(node => node.path ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(node => node.siblingIndex)
                .ThenBy(node => node.instanceID)
                .ToList();
        }

        private static void AppendKeywords(List<string> target, object source)
        {
            if (source == null)
            {
                return;
            }

            if (source is string singleKeyword)
            {
                target.Add(singleKeyword);
                return;
            }

            if (source is IEnumerable<string> keywordList)
            {
                foreach (string keyword in keywordList)
                {
                    target.Add(keyword);
                }
            }
}
    }
}