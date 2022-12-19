using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CW.Core.Timeline.Editor
{
   public static class CwPicker
    {
        public static List<ICwSelectable> PickedElements { get; private set; }

        public static void DoPick(CwTimelineWindowState state, Vector2 mousePosition)
        {
            if (state.ActivitiesRect.Contains(mousePosition))
            {
                PickedElements = state.SpacePartitioner.GetItemsAtPosition<ICwSelectable>(mousePosition).ToList();
            }
            else
            {
                if (PickedElements != null)
                    PickedElements.Clear();
                else
                    PickedElements = new List<ICwSelectable>();
            }
        }
    }
}