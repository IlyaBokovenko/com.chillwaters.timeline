using System;

namespace CW.Extensions.Pooling
{
    public class PoolingException : Exception
    {
        public PoolingException()
        {
        }

        public PoolingException(string message) : base(message)
        {
        }

        public PoolingException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}