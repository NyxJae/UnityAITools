namespace AgentCommands.SkillsExporter
{
    /// <summary>
    /// Python脚本模板,用于生成每个技能的执行脚本.
    /// </summary>
    public static class PythonScriptTemplate
    {
        /// <summary>
        /// Python脚本模板内容.
        /// 占位符 {AGENT_COMMANDS_DATA_DIR} 会在生成时被替换为实际路径.
        /// </summary>
        public const string ScriptTemplate = @"import json
import os
import sys
import glob
import time
import codecs

# 配置stdout为UTF-8编码，解决Windows命令行中文乱码问题
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8')
elif sys.platform.startswith('win'):
    sys.stdout = codecs.getwriter('utf-8')(sys.stdout.buffer, 'strict')
    sys.stderr = codecs.getwriter('utf-8')(sys.stderr.buffer, 'strict')

# 占位符,生成时会被替换为实际路径,例如：F:/UnityProject/SL/SL_402/Code/Assets/AgentCommands
# 使用原始字符串r''避免Windows路径中的反斜杠被当作转义符
AGENT_COMMANDS_DATA_DIR = r""{AGENT_COMMANDS_DATA_DIR}""
PENDING_DIR = os.path.join(AGENT_COMMANDS_DATA_DIR, ""pending"")
RESULTS_DIR = os.path.join(AGENT_COMMANDS_DATA_DIR, ""results"")
DONE_DIR = os.path.join(AGENT_COMMANDS_DATA_DIR, ""done"")

TIMEOUT = 30  # 超时时间(秒)
POLL_INTERVAL = 0.5  # 轮询间隔(秒)
MAX_RESULT_AGE = 5  # 结果文件最大年龄(秒)

def execute_command(input_json):
    """"""
    执行命令并返回结果

    Args:
        input_json: 输入JSON字符串或字典,必须包含batchId字段

    Returns:
        结果JSON字典

    Raises:
        TimeoutError: 超时未找到结果文件
        ValueError: batchId缺失
    """"""
    # 解析输入
    if isinstance(input_json, str):
        data = json.loads(input_json)
    else:
        data = input_json

    # 提取batchId(简单提取,不做其他校验)
    batch_id = data.get(""batchId"")
    if not batch_id:
        raise ValueError(""Missing required field: batchId"")

    # 写入pending目录
    pending_file = os.path.join(PENDING_DIR, f""{batch_id}.json"")
    with open(pending_file, 'w', encoding='utf-8') as f:
        json.dump(data, f, ensure_ascii=False, indent=2)

    # 轮询results目录
    start_time = time.time()
    while time.time() - start_time < TIMEOUT:
        # 查找结果文件
        pattern = os.path.join(RESULTS_DIR, f""{batch_id}.json"")
        result_files = glob.glob(pattern)

        for result_file in result_files:
            # 检查文件生成时间
            file_time = os.path.getmtime(result_file)
            if time.time() - file_time <= MAX_RESULT_AGE:
                # 尝试读取结果，捕获异常并重试
                try:
                    with open(result_file, 'r', encoding='utf-8') as f:
                        result = json.load(f)
                except (PermissionError, IOError, json.JSONDecodeError) as e:
                    # 文件可能被Unity占用或正在写入，跳过继续等待
                    continue

                # 检查状态
                status = result.get(""status"")
                if status in [""completed"", ""error""]:
                    # 在结果中添加结果文件路径，方便用户查看原始文件
                    # 创建新字典,将_resultFile放在最前面,避免输出太长时路径被截断
                    final_result = {""_resultFile"": result_file}
                    final_result.update(result)
                    return final_result
                # 状态不是completed或error，继续等待

        # 等待后再次轮询
        time.sleep(POLL_INTERVAL)

    # 超时处理
    raise TimeoutError(f""Timeout after {TIMEOUT} seconds. No result found for batchId: {batch_id}"")

# 命令行入口
if __name__ == ""__main__"":
    # 检查命令行参数
    if len(sys.argv) > 1:
        # 从命令行参数获取JSON字符串(推荐方式)
        # 示例: python execute_unity_command.py '{""batchId"":""batch_001"",""commands"":[...]}'
        input_json_str = "" "" .join(sys.argv[1:])
    else:
        # 示例用法(当没有参数时)
        example_input = {
            ""batchId"": ""batch_log_001"",
            ""timeout"": 30000,
            ""commands"": [{
                ""id"": ""cmd_001"",
                ""type"": ""log.query"",
                ""params"": {
                    ""n"": 50,
                    ""level"": ""Error""
                }
            }]
        }
        print(""Usage: python execute_unity_command.py '<JSON_STRING>'"")
        print(""Example:"")
        example_json = json.dumps(example_input, ensure_ascii=False)
        print(f""  python execute_unity_command.py '{example_json}'"")
        sys.exit(1)

    try:
        # 执行命令
        result = execute_command(input_json_str)

        # 输出结果(JSON格式,便于解析)
        print(json.dumps(result, indent=2, ensure_ascii=False))

    except TimeoutError as e:
        print(f""Error: {e}"", file=sys.stderr)
        sys.exit(1)
    except ValueError as e:
        print(f""Input error: {e}"", file=sys.stderr)
        sys.exit(1)
    except Exception as e:
        print(f""Unexpected error: {e}"", file=sys.stderr)
        sys.exit(1)

# 在Python代码中导入并调用(推荐用于复杂场景):
# from execute_unity_command import execute_command
# result = execute_command({""batchId"":""batch_001"",""commands"":[...]})
";
    }
}
