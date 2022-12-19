public static class PlatformIndependentHash
{
    public static int CalculateHash(string str)
    {
        var hash = 17;
        foreach(var c in str) 
        {
            unchecked { hash = hash * 257 + c.GetHashCode(); } 
        }

        return hash;
    }
}
