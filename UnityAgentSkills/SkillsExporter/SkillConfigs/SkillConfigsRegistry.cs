using System.Collections.Generic;

namespace UnityAgentSkills.SkillsExporter
{
    /// <summary>
    /// 技能配置数据结构.
    /// </summary>
    public class SkillConfig
    {
        /// <summary>
        /// 技能名称.
        /// </summary>
        public string Name;

        /// <summary>
        /// 技能描述.
        /// </summary>
        public string Description;

        /// <summary>
        /// 技能Markdown文档内容.
        /// </summary>
        public string Markdown;
    }

    /// <summary>
    /// 技能配置集中管理.
    /// </summary>
    public static class SkillConfigsRegistry
    {
        private static readonly Dictionary<string, SkillConfig> AllSkills;

        static SkillConfigsRegistry()
        {
            AllSkills = new Dictionary<string, SkillConfig>
            {
                { SkillConfig_UnityLog.SkillName, new SkillConfig
                    {
                        Name = SkillConfig_UnityLog.SkillName,
                        Description = SkillConfig_UnityLog.SkillDescription,
                        Markdown = SkillConfig_UnityLog.SkillMarkdown
                    }
                },
                { SkillConfig_UnityPrefabView.SkillName, new SkillConfig
                    {
                        Name = SkillConfig_UnityPrefabView.SkillName,
                        Description = SkillConfig_UnityPrefabView.SkillDescription,
                        Markdown = SkillConfig_UnityPrefabView.SkillMarkdown
                    }
                },
                { SkillConfig_UnityK3Prefab.SkillName, new SkillConfig
                    {
                        Name = SkillConfig_UnityK3Prefab.SkillName,
                        Description = SkillConfig_UnityK3Prefab.SkillDescription,
                        Markdown = SkillConfig_UnityK3Prefab.SkillMarkdown
                    }
                }
            };
        }

        /// <summary>
        /// 获取所有技能配置.
        /// </summary>
        public static IEnumerable<SkillConfig> GetAllSkills()
        {
            return AllSkills.Values;
        }

        /// <summary>
        /// 获取Python脚本模板.
        /// </summary>
        public static string GetPythonScriptTemplate()
        {
            return PythonScriptTemplate.ScriptTemplate;
        }
    }
}
