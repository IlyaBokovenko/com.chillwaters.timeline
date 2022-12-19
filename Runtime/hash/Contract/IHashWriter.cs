namespace CW.Core.Hash
{
    public interface IHashWriter
    {
        void Write(bool   value);
        void Write(byte   value);
        void Write(byte[]   value);
        void Write(char   value);
        void Write(char[]   value);
        void Write(decimal   value);
        void Write(double   value);
        void Write(short   value);
        void Write(int   value);
        void Write(long   value);
        void Write(sbyte   value);
        void Write(float   value);
        void Write(string   value);
        void Write(ushort   value);
        void Write(uint   value);
        void Write(ulong   value);
    }
}