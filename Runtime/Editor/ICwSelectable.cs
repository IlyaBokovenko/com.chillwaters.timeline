namespace CW.Core.Timeline.Editor
{
    public interface ICwSelectable
    {
        void Select();
        bool IsSelected();
        void Deselect();
    }
}