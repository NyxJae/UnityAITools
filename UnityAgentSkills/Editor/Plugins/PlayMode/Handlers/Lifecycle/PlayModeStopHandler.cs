using UnityAgentSkills.Core;
using LitJson2_utf;

namespace UnityAgentSkills.Plugins.PlayMode.Handlers
{
    /// <summary>
    /// playmode.stop 命令处理器.
    /// </summary>
    internal static class PlayModeStopHandler
    {
        /// <summary>
        /// 命令类型.
        /// </summary>
        public const string CommandType = "playmode.stop";

        /// <summary>
        /// 执行停止 Play Mode.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            PlayModeSession.Stop();

            JsonData result = new JsonData();
            result["sessionState"] = PlayModeSession.State.ToString();
            return result;
        }
    }
}
