using System;
using System.Text;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Timeline;

namespace CW.Core.Timeline.Editor
{
    public class ActivityTreeViewItem : TreeViewItem, ICwSelectable
    {
        public Vector2 TreeViewToWindowTransformation;
        bool _clipViewDirty;
        public float Start => (Data.HasBeginning ? Data.Start.ToSeconds : MinTime) + _settings.ShiftX;
        public float End => (Data.IsComplete ? Data.End.ToSeconds : MaxTime) +  _settings.ShiftX;

        private float MinTime => _settings.Start;
        private float MaxTime => _settings.End;

        public readonly ActivityVisualizationData Data;


        private SelectionState _selected;

        TimelineClip _clip;
        CwClipDrawData _clipDrawData;
        Rect m_ClipCenterSection;
        private readonly CwTimelineTreeViewDataSource.Settings _settings;

        internal ActivityTreeViewItem(ActivityVisualizationData data, CwTimelineTreeViewDataSource.Settings settings)
        {
            _settings = settings;
            Data = data;
        }

        Rect ToWindowSpace(Rect localRect)
        {
            localRect.position += TreeViewToWindowTransformation;
            return localRect;
        }

        public override int id => Data.Id;
        public override string displayName => Data.Description;

        void AppendDescriptionIdented(StringBuilder sb, int indent)
        {
            sb.AppendLine($"{"".PadLeft(indent)}{displayName} {(parent != null ? ($"[{Start} - {End}]") : "")}");
            if(children == null)
                return;
            
            foreach (var child in children)
            {
                (child as ActivityTreeViewItem).AppendDescriptionIdented(sb, indent + 1);
            }
        }
        public string HierarchyDescriptionString()
        {
            var sb = new StringBuilder();
            AppendDescriptionIdented(sb, 0);
            return sb.ToString();
        }

        public void Draw(Rect rowRect, CwTimelineWindowState state)
        {
            var timelineRect = RectToTimeline(rowRect, state);
            if (Event.current.type == EventType.Repaint)
            {
                var drawAsReference = Mathf.Approximately(timelineRect.width, 0f) && !Mathf.Approximately(timelineRect.height, 0f);
                if (drawAsReference)
                {
                    const float REFERENCE_SHIFT = 50f;
                    const float REFERENCE_LABEL_WIDTH = 200;
                    var referenceRect = new Rect(timelineRect.xMin, timelineRect.yMin, REFERENCE_SHIFT, timelineRect.height);
                    timelineRect = new Rect(referenceRect.xMax, timelineRect.yMin, REFERENCE_LABEL_WIDTH, timelineRect.height);
                    CwClipDrawer.DrawReference(referenceRect);                    
                }

                state.SpacePartitioner.AddBounds(this, ToWindowSpace(timelineRect));
                UpdateDrawData(timelineRect, _selected, state.Styles);
                CwClipDrawer.DrawSimpleClip(_clipDrawData, drawAsReference);
            }
        }

        void UpdateDrawData(Rect drawRect, SelectionState selected, ITimelineStyles styles)
        {
            _clipDrawData.rect = drawRect;
            _clipDrawData.selectionState = selected;
            //_clipDrawData.title = $"t:{_clipDrawData.targetRect.width} u:{_clipDrawData.unclippedRect.width} c:{_clipDrawData.clippedRect.width}";
            _clipDrawData.title = displayName;

            _clipDrawData.rightEdgeIsOpen = !Data.IsComplete;
            _clipDrawData.leftEdgeIsOpen = !Data.HasBeginning;

            _clipDrawData.alpha = Data.IsPhantom ? 0.5f : 1f;

            if (Data.StyleId != null && styles != null && styles.HasStyleForId(Data.StyleId))
            {
                _clipDrawData.highlightColor = styles.GetStyleForId(Data.StyleId).color;
            }
            else
            {
                _clipDrawData.highlightColor = _settings.Tint ?? new Color(0.8f, 0.8f, 0.8f);                
            }

            _clipDrawData.selectionState = _selected;
        }

        void ResetClipChanged()
        {
            if (Event.current.type == EventType.Repaint)
                _clipViewDirty = false;
        }

        Rect RectToTimeline(Rect trackRect, CwTimelineWindowState state)
        {
            var offsetFromTimeSpaceToPixelSpace = state.TimeAreaTranslation.x + trackRect.xMin;

            var start = (float)(DiscreteTime)Start;
            var end = (float)(DiscreteTime)End;

            var xmin = Mathf.Round(start * state.TimeAreaScale.x + offsetFromTimeSpaceToPixelSpace);
            var xmax = Mathf.Round(end * state.TimeAreaScale.x + offsetFromTimeSpaceToPixelSpace);
            return Rect.MinMaxRect(
                xmin, Mathf.Round(trackRect.yMin),
                xmax, Mathf.Round(trackRect.yMax)
            );
        }

#region ISelectable

        public void Select()
        {
            _selected = SelectionState.Selected;
            ParentsDo(aParent => aParent._selected = SelectionState.ChildSelected);
            ChildrenDo(aChild => aChild._selected = SelectionState.ParentSelected);
        }

        public bool IsSelected()
        {
            return _selected == SelectionState.Selected;
        }

        public void Deselect()
        {
            _selected = SelectionState.None;
        }

#endregion

        public void ParentsDo(Action<ActivityTreeViewItem> action)
        {
            if (parent != null && parent is ActivityTreeViewItem parentActivity)
            {
                action(parentActivity);
                parentActivity.ParentsDo(action);
            }
        }

        public void ChildrenDo(Action<ActivityTreeViewItem> action)
        {
            if (hasChildren)
            {
                foreach (ActivityTreeViewItem child in children)
                {
                    action(child);
                    child.ChildrenDo(action);
                }
            }
        }

        public void SetDepth()
        {
            ParentsDo(_ => depth++);
        }
    }
}