using System.Collections.Generic;
using System.Text;
using Dialogs.Utils;
using K3Engine.Common;
using K3Engine.Component;
using K3Engine.Component.Interfaces;
using K3Engine.Component.Tab;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace K3Editor
{
    public struct Comp
    {
        public string name;
        public IK3Component k3;
    }

    public static class AutoGenerateCode
    {
        /// <summary>
        /// 用于表示UI组件层级关系的节点
        /// </summary>
        private class ComponentNode
        {
            public IK3Component Component { get; set; }
            public Transform Transform { get; set; }
            public List<ComponentNode> Children { get; } = new List<ComponentNode>();
        }
        
        [MenuItem("GameObject/K3Editor/Generate Code %&k", false, 0)]
        private static void SelectDialogOrPanel()
        {
            var gameObj = Selection.activeGameObject;
            if (gameObj == null)
            {
                Debug.LogError("未选中任何物体");
                return;
            }

            var dialog = gameObj.GetComponent<K3DialogEx>();
            var panel = gameObj.GetComponent<K3Panel>();

            if (dialog != null || panel != null)
            {
                // 如果是 Dialog 或 Panel，执行树状生成
                ProcessTreeGeneration(gameObj);
            }
            else
            {
                // 否则，只处理选中的对象上的组件
                ProcessSingleComponentGeneration(gameObj);
            }
        }
        
        /// <summary>
        /// 只为选中的GameObject上挂载的K3组件生成代码
        /// </summary>
        /// <param name="gameObj">选中的GameObject</param>
        private static void ProcessSingleComponentGeneration(GameObject gameObj)
        {
            var components = gameObj.GetComponents<IK3Component>();
            if (components == null || components.Length == 0)
            {
                Debug.LogError($"选中物体 [{gameObj.name}] 不是K3Dialog/K3Panel，且其自身未附加任何IK3Component组件。");
                return;
            }

            var sb = new StringBuilder();
            foreach (var comp in components)
            {
                var constructor = GetConstructor(comp);
                if (!string.IsNullOrEmpty(constructor))
                {
                    sb.AppendLine(constructor);
                }
            }

            var result = sb.ToString().TrimEnd();
            if (!string.IsNullOrEmpty(result))
            {
                GUIUtility.systemCopyBuffer = result;
                Debug.Log(result);
            }
        }
        
        /// <summary>
        /// 【树状生成】处理选中的GameObject，构建组件树并生成代码
        /// </summary>
        /// <param name="rootObject"></param>
        private static void ProcessTreeGeneration(GameObject rootObject)
        {
            // 1. 从根节点开始构建组件树
            var rootNode = BuildComponentTree(rootObject.transform);
            if (rootNode == null)
            {
                Debug.LogWarning($"在 [{rootObject.name}] 及其子对象中找不到任何有效的 IK3Component 用于生成代码。");
                return;
            }

            // 2. 从树生成代码
            string code = GenerateCodeFromTree(rootNode);
            
            // 3. 输出代码
            if (!string.IsNullOrEmpty(code))
            {
                GUIUtility.systemCopyBuffer = code;
                Debug.Log(code);
            }
        }

        /// <summary>
        /// 递归构建组件树
        /// </summary>
        /// <param name="transform">当前处理的Transform</param>
        /// <returns>构建的节点，如果此transform及其所有子节点都没有IK3Component，则返回null</returns>
        private static ComponentNode BuildComponentTree(Transform transform)
        {
            var component = transform.GetComponent<IK3Component>();

            var node = new ComponentNode
            {
                Component = component,
                Transform = transform,
            };

            bool hasValidChildren = false;
            foreach (Transform child in transform)
            {
                var childNode = BuildComponentTree(child);
                if (childNode != null)
                {
                    node.Children.Add(childNode);
                    hasValidChildren = true;
                }
            }

            // 只有当一个节点本身没有组件，并且也没有任何有效的子节点时，才认为这个分支是无效的
            if (component == null && !hasValidChildren)
            {
                return null;
            }
    
            return node;
        }

        /// <summary>
        /// 从构建好的组件树生成代码
        /// </summary>
        /// <param name="rootNode">组件树的根节点</param>
        /// <returns>生成的Lua代码字符串</returns>
        private static string GenerateCodeFromTree(ComponentNode rootNode)
        {
            var sb = new StringBuilder();

            // 1. 为根节点本身生成代码
            if (rootNode.Component != null && !rootNode.Transform.name.StartsWith("K3"))
            {
                var constructor = GetConstructor(rootNode.Component);
                if (!string.IsNullOrEmpty(constructor))
                    sb.AppendLine(constructor);
            }
            
            // 2. 开始为子节点递归生成代码
            GenerateCodeForChildren(rootNode.Children, sb);

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// 为一组兄弟节点及其所有后代递归生成代码，并处理它们之间的空行。
        /// </summary>
        /// <param name="children">要处理的兄弟节点列表</param>
        /// <param name="sb">StringBuilder实例</param>
        private static void GenerateCodeForChildren(List<ComponentNode> children, StringBuilder sb)
        {
            for (var i = 0; i < children.Count; i++)
            {
                var currentNode = children[i];
                var hasGeneratedCodeForCurrentNode = false;

                // A. 为当前节点生成代码
                if (currentNode.Component != null && !currentNode.Transform.name.StartsWith("K3"))
                {
                    var constructor = GetConstructor(currentNode.Component);
                    if (!string.IsNullOrEmpty(constructor))
                    {
                        sb.AppendLine(constructor);
                        hasGeneratedCodeForCurrentNode = true;
                    }
                }
                
                // B. 递归处理当前节点的子节点
                var lengthBefore = sb.Length;
                GenerateCodeForChildren(currentNode.Children, sb);
                var generatedCodeForChildren = sb.Length > lengthBefore;
                
                // C. 在处理完一个完整的节点分支后，判断是否需要与下一个兄弟节点分支之间加空行
                if ((hasGeneratedCodeForCurrentNode || generatedCodeForChildren) && i < children.Count - 1)
                {
                    // 空行规则：如果当前节点(或其子节点)生成了代码，并且它有子节点，则加空行
                    if (currentNode.Children.Count > 0)
                    {
                        // 确保不连续添加空行
                        var content = sb.ToString();
                        if (content.Length > 0 && !content.EndsWith("\n\n") && !content.EndsWith("\r\n\r\n"))
                        {
                            sb.AppendLine();
                        }
                    }
                }
            }
        }

        private static string GetConstructor(IK3Component comp)
        {
            switch (comp)
            {
                case K3Animation k3Animation:
                    return string.Format($"self.{k3Animation.name} = self:getAnimation({k3Animation.property.ID})");
                case K3BloodImage k3BloodImage:
                    break;
                case K3CheckBox k3CheckBox:
                    return string.Format($"self.{k3CheckBox.name} = self:getCheckBox({k3CheckBox.property.ID})");
                case K3Slider k3Slider:
                    return string.Format($"self.{k3Slider.name} = self:getSlider({k3Slider.property.ID})");
                case K3Tab k3Tab:
                    return string.Format($"self.{k3Tab.name} = self:getTab({k3Tab.property.ID})");
                case K3TabButton k3TabButton:
                    return string.Format($"self.{k3TabButton.name} = self:getTabButton({k3TabButton.property.ID})");
                case K3Button k3Button:
                    return string.Format($"self.{k3Button.name} = self:getButton({k3Button.property.ID})\nself.{k3Button.name}:AddEventListener(function(args) end)");
                case K3RadarChart k3RadarChart:
                    break;
                case K3ChartImage k3ChartImage:
                    break;
                case K3Edit k3Edit:
                    return string.Format($"self.{k3Edit.name} = self:getEdit({k3Edit.property.ID})\nself.{k3Edit.name}:AddValueChange(function(args) end)");
                case K3HeadIcon k3HeadIcon:
                    break;
                case K3Itembox k3Itembox:
                    return string.Format($"self.{k3Itembox.name} = self:getItembox({k3Itembox.property.ID})");
                case K3MagicBox k3MagicBox:
                    break;
                case K3ProgressBar k3ProgressBar:
                    return string.Format($"self.{k3ProgressBar.name} = self:getProgressBar({k3ProgressBar.property.ID})");
                case K3Panel k3Panel:
                    return string.Format($"self.{k3Panel.name} = self:getPanel({k3Panel.property.ID})");
                case K3Image k3Image:
                    return string.Format($"self.{k3Image.name} = self:getImage({k3Image.property.ID})");
                case K3InsightImage k3InsightImage:
                    return string.Format($"self.{k3InsightImage.name} = self:getInsightImage({k3InsightImage.property.ID})");
                case K3LabelButton k3LabelButton:
                    return string.Format($"self.{k3LabelButton.name} = self:getLabelButton({k3LabelButton.property.ID})\nself.{k3LabelButton.name}:AddEventListener(function(args) end)");
                case K3LinkLabel k3LinkLabel:
                    return string.Format($"self.{k3LinkLabel.name} = self:getLinkLabel({k3LinkLabel.property.ID})");
                case K3Label k3Label:
                    if (k3Label.GetComponent<LeftTimeTicker>())
                    {
                        return string.Format($"self.{k3Label.name} = self:getLabel({k3Label.property.ID})\nlocal {k3Label.name}_leftTicker = self:getLeftTimeTicker({k3Label.property.ID})");
                    }
                    return string.Format($"self.{k3Label.name} = self:getLabel({k3Label.property.ID})");
                case K3ListView k3ListView:
                    return string.Format($"self.{k3ListView.name}_listView = LuaUtil.SetListView({k3ListView.property.ID}, 1, \"\")");
                case K3Movie k3Movie:
                    break;
                case K3NumImage k3NumImage:
                    return string.Format($"self.{k3NumImage.name} = self:getNumImage({k3NumImage.property.ID})");
            }
            return string.Empty;
        }
    }
}

