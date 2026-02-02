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
                { SkillConfig_LogQuery.SkillName, new SkillConfig
                    {
                        Name = SkillConfig_LogQuery.SkillName,
                        Description = SkillConfig_LogQuery.SkillDescription,
                        Markdown = SkillConfig_LogQuery.SkillMarkdown
                    }
                },
                { SkillConfig_PrefabView.SkillName, new SkillConfig
                    {
                        Name = SkillConfig_PrefabView.SkillName,
                        Description = SkillConfig_PrefabView.SkillDescription,
                        Markdown = SkillConfig_PrefabView.SkillMarkdown
                    }
                },
                { SkillConfig_K3Prefab.SkillName, new SkillConfig
                    {
                        Name = SkillConfig_K3Prefab.SkillName,
                        Description = SkillConfig_K3Prefab.SkillDescription,
                        Markdown = SkillConfig_K3Prefab.SkillMarkdown
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
