using System.Linq;
using UnityEngine;

namespace CW.Core.Timeline.Editor
{
    public class SelectManipulator : CwManipulator
    {
        private TimelineSelection _selection;

        public SelectManipulator(TimelineSelection selection)
        {
            _selection = selection;
        }

        protected override bool MouseDown(Event evt, CwTimelineWindowState state)
        {
            if (evt.alt || evt.button != 0)
                return false;

            return HandleSingleSelection(evt);
        }

        private bool HandleSingleSelection(Event evt)
        {
            var item = CwPicker.PickedElements.LastOrDefault();

            if (item != null)
            {
                if (item.IsSelected())
                {
                    _selection.Deselect();
                }
                else
                {
                    _selection.Select(item);
                }

                return true;
            }

            return false;
        }
    }
}