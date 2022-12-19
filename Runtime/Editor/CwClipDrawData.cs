using UnityEngine;

namespace CW.Core.Timeline.Editor
{
    public struct CwClipDrawData
    {
        public string title;
        public SelectionState selectionState;
        public Rect rect;
        public Color highlightColor;
        public bool rightEdgeIsOpen;
        public bool leftEdgeIsOpen;
        public float alpha;
    }
}