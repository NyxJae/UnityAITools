using System.IO;
using UnityEngine;

namespace UnityAgentSkills.Core
{
    /// <summary>
    /// 路径处理工具类.
    /// </summary>
    public static class PathUtils
    {
        /// <summary>
        /// 获取相对路径的完整路径.
        /// </summary>
        /// <param name="relativePath">相对路径,空值或空字符串将使用 "Assets"</param>
        /// <returns>完整路径</returns>
        public static string GetFullPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                relativePath = "Assets";
            }
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
        }
    }
}
