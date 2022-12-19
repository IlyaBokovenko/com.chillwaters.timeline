using UnityEditor.Timeline;
using UnityEngine;

namespace CW.Core.Timeline.Editor
{
    public class CwTimelineWindowState
    {
        TimelineVisualizer _window;

        readonly SpacePartitioner _spacePartitioner = new SpacePartitioner();

        internal SpacePartitioner SpacePartitioner => _spacePartitioner;


        public CwTimelineWindowState(TimelineVisualizer window)
        {
            _window = window;
        }

        public Rect ActivitiesRect => _window.ActivitiesRect;
        
        public Vector2 TimeAreaTranslation => _window.TimeArea.translation;
        public Vector2 TimeAreaScale => _window.TimeArea.scale;
        public ActivityTreeViewItem ActivitiesRoot => (ActivityTreeViewItem) _window.TreeViewData.root;
        public ActivityTreeViewItem ActivitiesRoot2 => (ActivityTreeViewItem) _window.TreeViewData2?.root;

        public ITimelineStyles Styles => _window.Styles;
    }
}