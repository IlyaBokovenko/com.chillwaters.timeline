using System;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CW.Core.Timeline.Editor
{
    class CwTimelineTreeViewDataSource : TreeViewDataSource
    {
        public class Settings
        {
            public Color? Tint;
            public float ShiftX;
            public float ShiftY;
            public float Start;
            public float End;
        }

        public GlobalTimelineVisualizationData Data { get; private set; }
        public Settings TheSettings  { get; private set; } = new Settings();

        public CwTimelineTreeViewDataSource(TreeViewController treeView) : base(treeView)
        {
            
        }

        public float Start => Data.MinTime.ToSeconds;
        public float End => Data.MaxTime.ToSeconds;
        public float Duration => End - Start;

        public override void FetchData()
        {
            m_RootItem = BuildTreeItems(Data.Root, TheSettings);
            m_Rows = null;
        }

        public void SetData(GlobalTimelineVisualizationData data, Settings settings = default)
        {
            Data = data;
            TheSettings = settings??new Settings();
            TheSettings.Start = data.MinTime.ToSeconds;
            TheSettings.End = data.MaxTime.ToSeconds;
            FetchData();
        }

        private TreeViewItem BuildTreeItems(ActivityVisualizationData dataRoot, Settings settings)
        {
            var root = new ActivityTreeViewItem(dataRoot, settings);
            CreateChildren(root);

            return root;

            void CreateChildren(ActivityTreeViewItem node)
            {
                if (!node.Data.HasChildren)
                    return;
                
                foreach (var child in node.Data.Children)
                {
                    var treeChild = new ActivityTreeViewItem(child, settings);
                    node.AddChild(treeChild);
                    CreateChildren(treeChild);
                }
            }
        }

        public void ExpandItems(TreeViewItem item)
        {
            SetExpanded(item, true);

            if (item.children != null)
            {
                foreach (var t in item.children)
                {
                    ExpandItems(t);
                }
            }
        }

        public int FindNextTerm(string term, int curId = 0)
        {
            try
            {
                bool curPassed = false;
                RecursiveDo(item =>
                {
                    if (!curPassed)
                    {
                        if (item.id == curId)
                            curPassed = true;
                        return;
                    }
                
                    if(item.displayName.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        throw new NonLocalReturnException<int>(item.id);
                    }

                }, (ActivityTreeViewItem)m_RootItem);
            }
            catch (NonLocalReturnException<int> e)
            {
                return e.Item;
            }

            return 0;
        }

        void RecursiveDo(Action<ActivityTreeViewItem> action, ActivityTreeViewItem item = null)
        {
            if (item == null)
            {
                item = (ActivityTreeViewItem)m_RootItem;
            }

            action(item);
            item.ChildrenDo(action);
        }

    }
}