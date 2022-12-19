using System;
using UnityEngine;

namespace CW.Core.Timeline.Editor
{
    public interface ITimelineStyles
    {
        bool HasStyleForId(string id);
        TimelineStyle GetStyleForId(string id);
    }
    
    [Serializable]
    public readonly struct TimelineStyle
    {
        public readonly Color color;

        public TimelineStyle(Color color)
        {
            this.color = color;
        }
    }

}