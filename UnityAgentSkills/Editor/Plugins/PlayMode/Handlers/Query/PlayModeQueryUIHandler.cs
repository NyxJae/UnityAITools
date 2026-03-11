using UnityAgentSkills.Core;
using UnityAgentSkills.Plugins.PlayMode.Utils;
using LitJson2_utf;

namespace UnityAgentSkills.Plugins.PlayMode.Handlers
{
    /// <summary>
    /// playmode.queryUI 命令处理器.
    /// </summary>
    internal static class PlayModeQueryUIHandler
    {
        /// <summary>
        /// 命令类型.
        /// </summary>
        public const string CommandType = "playmode.queryUI";

        /// <summary>
        /// 查询当前场景中的 UI 元素.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);
            string[] componentFilter = PlayModeParamUtils.ParseComponentFilter(rawParams);
            string[] nameContains = PlayModeParamUtils.ParseStringArray(rawParams, "nameContains");
            string[] textContains = PlayModeParamUtils.ParseStringArray(rawParams, "textContains");
            bool visibleOnly = parameters.GetBool("visibleOnly", false);
            bool interactableOnly = parameters.GetBool("interactableOnly", false);
            int maxResults = parameters.GetInt("maxResults", 200);
            JsonData screenRect = rawParams != null && rawParams.IsObject && rawParams.ContainsKey("screenRect")
                ? rawParams["screenRect"]
                : null;

            return PlayModeUIQueryUtils.QueryAll(nameContains, textContains, componentFilter, visibleOnly, interactableOnly, maxResults, screenRect);
        }
    }
}
