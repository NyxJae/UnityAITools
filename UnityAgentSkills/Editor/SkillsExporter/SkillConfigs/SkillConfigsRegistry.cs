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
                { SkillConfig_UnityPrefabEdit.SkillName, new SkillConfig
                    {
                        Name = SkillConfig_UnityPrefabEdit.SkillName,
                        Description = SkillConfig_UnityPrefabEdit.SkillDescription,
                        Markdown = SkillConfig_UnityPrefabEdit.SkillMarkdown
                    }
                },

                { SkillConfig_UnityPlayMode.SkillName, new SkillConfig
                    {
                        Name = SkillConfig_UnityPlayMode.SkillName,
                        Description = SkillConfig_UnityPlayMode.SkillDescription,
                        Markdown = SkillConfig_UnityPlayMode.SkillMarkdown
                    }
                },
                { SkillConfig_UnitySceneView.SkillName, new SkillConfig
                    {
                        Name = SkillConfig_UnitySceneView.SkillName,
                        Description = SkillConfig_UnitySceneView.SkillDescription,
                        Markdown = SkillConfig_UnitySceneView.SkillMarkdown
                    }
                },
                { SkillConfig_UnitySceneEdit.SkillName, new SkillConfig
                    {
                        Name = SkillConfig_UnitySceneEdit.SkillName,
                        Description = SkillConfig_UnitySceneEdit.SkillDescription,
                        Markdown = SkillConfig_UnitySceneEdit.SkillMarkdown
                    }
                },
                { SkillConfig_UnityPrefabBridge.SkillName, new SkillConfig
                    {
                        Name = SkillConfig_UnityPrefabBridge.SkillName,
                        Description = SkillConfig_UnityPrefabBridge.SkillDescription,
                        Markdown = SkillConfig_UnityPrefabBridge.SkillMarkdown
                    }
                },
                { SkillConfig_UnityEditorAction.SkillName, new SkillConfig
                    {
                        Name = SkillConfig_UnityEditorAction.SkillName,
                        Description = SkillConfig_UnityEditorAction.SkillDescription,
                        Markdown = SkillConfig_UnityEditorAction.SkillMarkdown
                    }
                },
                { SkillConfig_UnityEditorControl.SkillName, new SkillConfig
                    {
                        Name = SkillConfig_UnityEditorControl.SkillName,
                        Description = SkillConfig_UnityEditorControl.SkillDescription,
                        Markdown = SkillConfig_UnityEditorControl.SkillMarkdown
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
