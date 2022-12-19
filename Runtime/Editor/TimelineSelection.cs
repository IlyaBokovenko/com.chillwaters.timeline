namespace CW.Core.Timeline.Editor
{
    public class TimelineSelection
    {
        private ActivityTreeViewItem Root => _state.ActivitiesRoot;
        private ActivityTreeViewItem Root2 => _state.ActivitiesRoot2;
        private CwTimelineWindowState _state;

        public ActivityTreeViewItem GetSelection()
        {
            try
            {
                Root.ChildrenDo(item =>
                {
                    if (item.IsSelected())
                        throw new NonLocalReturnException<ActivityTreeViewItem>(item);
                });
            }
            catch (NonLocalReturnException<ActivityTreeViewItem> e)
            {
                return e.Item;
            }

            return null;
        }

        public TimelineSelection(CwTimelineWindowState state)
        {
            _state = state;
        }

        public void Select(ICwSelectable selectable)
        {
            Deselect();
            selectable.Select();
        }

        public void Deselect()
        {
            Root.Deselect();
            Root.ChildrenDo(child => child.Deselect());
            
            Root2?.Deselect();
            Root2?.ChildrenDo(child => child.Deselect());
        }
    }
} 