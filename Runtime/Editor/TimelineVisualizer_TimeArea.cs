using UnityEditor;
using UnityEngine;

namespace CW.Core.Timeline.Editor
{
    public partial class TimelineVisualizer
    {
        public Rect TimeAreaRect =>
            new Rect(
                0,
                MENU_HEIGHT,
                Mathf.Max(position.width, TimelineVisualizer.TIME_AREA_MIN_WIDTH),
                TIME_AREA_HEIGHT
            );

        void InitializeTimeArea()
        {
            if (_timeArea == null)
            {
                var rangeMin = Mathf.Max(0f,TreeViewData.Start - 1f);
                var rangeMax = TreeViewData.End + 1f;
                _timeArea = new TimeArea(false)
                {
                    hRangeLocked = false,
                    vRangeLocked = true,
                    margin = 10,
                    scaleWithWindow = true,
                    hSlider = true,
                    vSlider = false,
                    hBaseRangeMin = rangeMin,
                    hBaseRangeMax = rangeMax,
                    hScaleMax = MAX_TIME_AREA_SCALING,
                    rect = TimeAreaRect
                };
                TimeArea.hTicks.SetTickModulosForFrameRate(1000);
                TimeArea.SetShownHRange(rangeMin, rangeMax);
            }
        }

        void TimelineGUI()
        {
            
            Rect rect = TimeAreaRect;
            _timeArea.rect = new Rect(rect.x, rect.y, rect.width, position.height - rect.y);
            
            _timeArea.BeginViewGUI();
            _timeArea.TimeRuler(TimeAreaRect, 1000f, true, false, 1.0f, TimeArea.TimeFormat.TimeFrame);
            _timeArea.EndViewGUI();
        }
    }
}