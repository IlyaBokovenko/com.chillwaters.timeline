namespace CW.Core.Hash
{
    public static class ContentHashHelper
    {
        public static byte[] ComputeHash(this IContentHash ch)
        {
            using (var writer = new TracingHashWriter())
            {
                ch.WriteContentHash(writer);
                return writer.ComputeHash();
            }
        }
    }
}