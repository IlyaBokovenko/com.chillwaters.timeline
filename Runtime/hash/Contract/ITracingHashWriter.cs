using System;

namespace CW.Core.Hash
{
    public interface ITracingHashWriter : IDisposable
    {
        IDisposable IndentationBlock();
        ILineHashWriter Indent();
        ILineHashWriter Trace(string msg);
        void Write(object data);
        ILineHashWriter Write(bool value);
        ILineHashWriter Write(byte value);
        ILineHashWriter Write(byte[] value);
        ILineHashWriter Write(char value);
        ILineHashWriter Write(char[] value);
        ILineHashWriter Write(decimal value);
        ILineHashWriter Write(double value);
        ILineHashWriter Write(short value);
        ILineHashWriter Write(int value);
        ILineHashWriter Write(long value);
        ILineHashWriter Write(sbyte value);
        ILineHashWriter Write(float value);
        ILineHashWriter Write(string value);
        ILineHashWriter Write(ushort value);
        ILineHashWriter Write(uint value);
        ILineHashWriter Write(ulong value);
    }

    public interface ILineHashWriter
    {
        ILineHashWriter Trace(string msg);
        ILineHashWriter Write(bool   value);
        ILineHashWriter Write(byte   value);
        ILineHashWriter Write(byte[]   value);
        ILineHashWriter Write(char   value);
        ILineHashWriter Write(char[]   value);
        ILineHashWriter Write(decimal   value);
        ILineHashWriter Write(double   value);
        ILineHashWriter Write(short   value);
        ILineHashWriter Write(int   value);
        ILineHashWriter Write(long   value);
        ILineHashWriter Write(sbyte   value);
        ILineHashWriter Write(float   value);
        ILineHashWriter Write(string   value);
        ILineHashWriter Write(ushort   value);
        ILineHashWriter Write(uint   value);
        ILineHashWriter Write(ulong   value);
    }
}