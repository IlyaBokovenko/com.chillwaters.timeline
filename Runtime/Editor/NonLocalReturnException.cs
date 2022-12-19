using System;

namespace CW.Core.Timeline.Editor
{
    internal class NonLocalReturnException<T> : Exception
    {
        public T Item;

        public NonLocalReturnException(T item)
        {
            Item = item;
        }
    }
}