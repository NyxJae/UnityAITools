using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityAgentSkills.UI.Components
{
    /// <summary>
    /// Tab内容管理器,负责管理所有tab页面的注册和切换.
    /// </summary>
    public class TabContentManager
    {
        private readonly List<ITabContent> _tabs = new List<ITabContent>();
        private int _currentTabIndex = 0;

        /// <summary>
        /// 注册新的tab内容.
        /// </summary>
        public void RegisterTab(ITabContent tab)
        {
            if (tab == null)
            {
                Debug.LogWarning("[TabContentManager] 尝试注册null tab");
                return;
            }

            _tabs.Add(tab);
        }

        /// <summary>
        /// 获取所有已注册的tab.
        /// </summary>
        public IReadOnlyList<ITabContent> GetAllTabs()
        {
            return _tabs.AsReadOnly();
        }

        /// <summary>
        /// 获取当前激活的tab索引.
        /// </summary>
        public int CurrentTabIndex
        {
            get => _currentTabIndex;
            set
            {
                if (value >= 0 && value < _tabs.Count)
                {
                    _currentTabIndex = value;
                }
            }
        }

        /// <summary>
        /// 获取当前激活的tab.
        /// </summary>
        public ITabContent CurrentTab
        {
            get
            {
                if (_currentTabIndex >= 0 && _currentTabIndex < _tabs.Count)
                {
                    return _tabs[_currentTabIndex];
                }
                return null;
            }
        }

        /// <summary>
        /// 绘制tab切换按钮.
        /// </summary>
        public void DrawTabButtons()
        {
            if (_tabs.Count == 0)
            {
                EditorGUILayout.HelpBox("没有可用的tab", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginHorizontal();

            for (int i = 0; i < _tabs.Count; i++)
            {
                bool isActive = (i == _currentTabIndex);
                // 统一使用toolbar样式,保持视觉一致性
                if (GUILayout.Button(_tabs[i].TabName, isActive ? EditorStyles.toolbarButton : EditorStyles.toolbarButton))
                {
                    _currentTabIndex = i;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制当前激活的tab内容.
        /// </summary>
        public void DrawCurrentTabContent()
        {
            ITabContent currentTab = CurrentTab;
            if (currentTab != null)
            {
                currentTab.OnGUI();
            }
            else
            {
                EditorGUILayout.HelpBox("请选择一个tab", MessageType.Info);
            }
        }
    }
}
