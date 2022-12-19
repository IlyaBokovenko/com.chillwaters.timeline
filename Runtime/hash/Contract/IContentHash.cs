namespace CW.Core.Hash
{
    public interface IContentHash
    {
        void WriteContentHash(ITracingHashWriter writer);
    }
}