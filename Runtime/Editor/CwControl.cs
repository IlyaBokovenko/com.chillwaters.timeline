using System.Collections.Generic;

namespace CW.Core.Timeline.Editor
{
    class CwControl
    {
        readonly List<CwManipulator> _manipulators = new List<CwManipulator>();

        public bool HandleManipulatorsEvents(CwTimelineWindowState state)
        {
            var isHandled = false;

            foreach (var manipulator in _manipulators)
            {
                isHandled = manipulator.HandleEvent(state);
                if (isHandled)
                    break;
            }

            return isHandled;
        }

        public void AddManipulator(CwManipulator m)
        {
            _manipulators.Add(m);
        }
    }}