using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CW.Core.Timeline.Editor
{
    internal class CwTimelineTreeView : ITreeViewGUI
    {
        const float TRACK_HEIGHT = 30f;
        TreeViewController _controller;

        List<Rect> _rowRects = new List<Rect>();
        CwTimelineWindowState _state;
        
        public Vector2 scrollPosition
        {
            get => _controller.state.scrollPos;
            set
            {
                Rect r = _controller.GetTotalRect();
                Vector2 visibleContent = _controller.GetContentSize();
                _controller.state.scrollPos = new Vector2(value.x, Mathf.Min(Mathf.Clamp(value.y, 0.0f, visibleContent.y - r.height)));
            }
        }

        public CwTimelineTreeView(TreeViewController controller, CwTimelineWindowState state)
        {
            _controller = controller;
            _state = state;
        }

        public virtual void OnInitialize()
        {
        }

        public Rect GetRowRect(int row,  float rowWidth)
        {
            return GetRowRect(row);
        }
        
        public Rect GetRowRect(int row)
        {
            if (_rowRects.Count != 0)
                return _rowRects[row];
            Debug.LogError("Ensure precalc rects");
            return new Rect();
        }

        public Rect GetRenameRect(Rect rowRect, int row, TreeViewItem item)
        {
            return new Rect();
        }

        public Rect GetRectForFraming(int row)
        {
            return GetRowRect(row, 1f);
        }

        public void OnRowGUI(
            Rect rowRect,
            TreeViewItem item,
            int row,
            bool selected,
            bool focused)
        {
            var aitem = (ActivityTreeViewItem)item;
            aitem.TreeViewToWindowTransformation = _controller.GetTotalRect().position - _controller.state.scrollPos;
            aitem.Draw(rowRect, _state);
        }

        Vector2 GetSizeOfRow(TreeViewItem item)
        {
            if (item.parent == null)
                return new Vector2(_controller.GetTotalRect().width, 0.0f);

            return new Vector2(_controller.GetTotalRect().width, TRACK_HEIGHT);
        }

        public void CalculateRowRects()
        {
            if (_controller.isSearching)
                return;
            
            var shiftY = ((CwTimelineTreeViewDataSource) _controller.data).TheSettings.ShiftY;
            
            IList<TreeViewItem> rows = _controller.data.GetRows();
            _rowRects = new List<Rect>(rows.Count);
            float curY = 2f + shiftY;
            for (int index = 0; index < rows.Count; ++index)
            {
                TreeViewItem treeViewItem = rows[index];
                float y = curY;
                Vector2 sizeOfRow = GetSizeOfRow(treeViewItem);
                _rowRects.Add(new Rect(0.0f, y, sizeOfRow.x, sizeOfRow.y));
                curY = y + sizeOfRow.y;
            }
        }

        public Vector2 GetTotalSize()
        {
            if (_rowRects.Count == 0)
            {
                return Vector2.zero;
            }

            return new Vector2(_controller.GetTotalRect().width, _rowRects[_rowRects.Count - 1].yMax);
        }

        public int GetNumRowsOnPageUpDown(TreeViewItem fromItem, bool pageUp, float heightOfTreeView)
        {
            Debug.LogError("GetNumRowsOnPageUpDown: Not impemented");
            return (int) Mathf.Floor(heightOfTreeView / 30f);
        }

        public void GetFirstAndLastRowVisible(out int firstRowVisible, out int lastRowVisible)
        {
            float y = _controller.state.scrollPos.y;
            float height = _controller.GetTotalRect().height;
            int rowCount = _controller.data.rowCount;
            if (rowCount != _rowRects.Count)
            {
                Debug.LogError(
                    "Mismatch in state: rows vs cached rects. Did you remember to hook up: dataSource.onVisibleRowsChanged += gui.CalculateRowRects ?");
                CalculateRowRects();
            }

            int curFirstRowVisible = -1;
            int curLastRowVisible = -1;
            for (int index = 0; index < _rowRects.Count; ++index)
            {
                if (_rowRects[index].y > (double) y && _rowRects[index].y < y + (double) height ||
                    _rowRects[index].yMax > (double) y && _rowRects[index].yMax < y + (double) height)
                {
                    if (curFirstRowVisible == -1)
                        curFirstRowVisible = index;
                    curLastRowVisible = index;
                }
            }

            if (curFirstRowVisible != -1 && curLastRowVisible != -1)
            {
                firstRowVisible = curFirstRowVisible;
                lastRowVisible = curLastRowVisible;
            }
            else
            {
                firstRowVisible = 0;
                lastRowVisible = rowCount - 1;
            }
        }

        public virtual void BeginRowGUI()
        {
            if (_controller.GetTotalRect().width != GetRowRect(0).width)
            {
                CalculateRowRects();
            }
        }

        public virtual void EndRowGUI()
        {
        }

        public virtual void BeginPingItem(TreeViewItem item, float topPixelOfRow, float availableWidth)
        {
            throw new NotImplementedException();
        }

        public virtual void EndPingItem()
        {
            throw new NotImplementedException();
        }

        public virtual bool BeginRename(TreeViewItem item, float delay)
        {
            throw new NotImplementedException();
        }

        public virtual void EndRename()
        {
            throw new NotImplementedException();
        }

        public virtual float halfDropBetweenHeight
        {
            get { return 8f; }
        }

        public virtual float topRowMargin { get; private set; }

        public virtual float bottomRowMargin { get; private set; }

        public virtual float GetContentIndent(TreeViewItem item)
        {
            return 0f;
        }
    }
}