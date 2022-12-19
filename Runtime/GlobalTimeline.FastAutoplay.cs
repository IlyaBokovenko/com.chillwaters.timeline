using System;

namespace CW.Core.Timeline
{
    public partial class GlobalTimeline
    {
        private bool _areSubsystemsDisabled = false;

        public void DisableSubstemsDuring(Action action)
        {
            _areSubsystemsDisabled = true;
            action.Invoke();
            _areSubsystemsDisabled = false;
        }
    }
}