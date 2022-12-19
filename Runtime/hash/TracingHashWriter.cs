using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CW.Core.Hash
{
    public class TracingHashWriter : ITracingHashWriter
    {
        private StringBuilder _builder;
        private BinaryWriterWrapper _wrapper;
        private int _indentLevel;

        public TracingHashWriter()
        {
            _builder = new StringBuilder();
            _wrapper = new BinaryWriterWrapper();
        }
        
        public List<string> GetTrace()
        {
            return _builder.ToString().Split(new[]{Environment.NewLine}, StringSplitOptions.None).ToList();
        }

        public byte[] ComputeHash()
        {
            var md5 = MD5.Create();
            _wrapper.Writer.BaseStream.Seek(0, SeekOrigin.Begin);
            return md5.ComputeHash(_wrapper.Writer.BaseStream);
        }

        public override string ToString()
        {
            return _builder.ToString();
        }

        public void Dispose()
        {
            _wrapper.Dispose();
            _builder = null;
        }

        public IDisposable IndentationBlock()
        {
            return new IndentationBlockImpl(this);
        }

        public ILineHashWriter Indent()
        {
            return new LineHashWriter(_builder, _wrapper, _indentLevel + 1);
        }

        public ILineHashWriter Trace(string msg)
        {
            return new LineHashWriter(_builder, _wrapper, _indentLevel).Trace(msg);
        }

        public void Write(object data)
        {
            ILineHashWriter line = new LineHashWriter(_builder, _wrapper, _indentLevel);
            if (HashWriterHelper.WritePrimitive(data, (IHashWriter)line))
            {
                return;
            }

            var (isOk, msg) = WriteContainer(data);
            if (isOk)
            {
                return;
            }

            msg = msg ?? $"Unrecognized type {data.GetType()}";
            msg = $"{msg}\n{_builder}";

            throw new ArgumentException(msg);
        }

        public ILineHashWriter Write(bool value)
        {
            return new LineHashWriter(_builder, _wrapper, _indentLevel).Write(value);
        }

        public ILineHashWriter Write(byte value)
        {
            return new LineHashWriter(_builder, _wrapper, _indentLevel).Write(value);
        }

        public ILineHashWriter Write(byte[] value)
        {
            return new LineHashWriter(_builder, _wrapper, _indentLevel).Write(value);
        }

        public ILineHashWriter Write(char value)
        {
            return new LineHashWriter(_builder, _wrapper, _indentLevel).Write(value);
        }

        public ILineHashWriter Write(char[] value)
        {
            return new LineHashWriter(_builder, _wrapper, _indentLevel).Write(value);
        }

        public ILineHashWriter Write(decimal value)
        {
            return new LineHashWriter(_builder, _wrapper, _indentLevel).Write(value);
        }

        public ILineHashWriter Write(double value)
        {
            return new LineHashWriter(_builder, _wrapper, _indentLevel).Write(value);
        }

        public ILineHashWriter Write(short value)
        {
            return new LineHashWriter(_builder, _wrapper, _indentLevel).Write(value);
        }

        public ILineHashWriter Write(int value)
        {
            return new LineHashWriter(_builder, _wrapper, _indentLevel).Write(value);
        }

        public ILineHashWriter Write(long value)
        {
            return new LineHashWriter(_builder, _wrapper, _indentLevel).Write(value);
        }

        public ILineHashWriter Write(sbyte value)
        {
            return new LineHashWriter(_builder, _wrapper, _indentLevel).Write(value);
        }

        public ILineHashWriter Write(float value)
        {
            return new LineHashWriter(_builder, _wrapper, _indentLevel).Write(value);
        }

        public ILineHashWriter Write(string value)
        {
            return new LineHashWriter(_builder, _wrapper, _indentLevel).Write(value);
        }

        public ILineHashWriter Write(ushort value)
        {
            return new LineHashWriter(_builder, _wrapper, _indentLevel).Write(value);
        }

        public ILineHashWriter Write(uint value)
        {
            return new LineHashWriter(_builder, _wrapper, _indentLevel).Write(value);
        }

        public ILineHashWriter Write(ulong value)
        {
            return new LineHashWriter(_builder, _wrapper, _indentLevel).Write(value);
        }

        private (bool isOk, string message) WriteContainer(object data)
        {
            if (data is IContentHash hasher)
            {
                Trace(data.GetType().Name);
                using (IndentationBlock())
                {
                    hasher.WriteContentHash(this);                    
                }
                return (true, null);
            }
            
            if (data is IDictionary dictionary)
            {
                Trace("dict");
                using (IndentationBlock())
                {
                    var keys = dictionary.Keys.Cast<IComparable>().ToArray();
                    Array.Sort(keys);
                    foreach (var key in keys)
                    {
                        var value = dictionary[key];
                        var elementLine = new LineHashWriter(_builder, _wrapper, _indentLevel);
                        if (!HashWriterHelper.WritePrimitive(key, elementLine))
                        {
                            return (false, $"dictionary key must be primitive. Got {key.GetType().Name}");
                        }

                        if (!HashWriterHelper.WritePrimitive(value, elementLine))
                        {
                            Trace("value");
                            using (IndentationBlock())
                            {
                                var (isOk, msg) = WriteContainer(value);
                                if (!isOk)
                                {
                                    return (false, $"key {key} " + msg);
                                }
                            }
                        }

                    }

                }
                
                return (true, null);
            }
            
            if (data is IEnumerable enumerable)
            {
                Trace("list");
                using (IndentationBlock())
                {
                    foreach (var element in enumerable)
                    {
                        Write(element);
                    }
                }
                return (true, null);
            }

            return (false, null);
        }

        private struct IndentationBlockImpl : IDisposable
        {
            private TracingHashWriter _writer;

            public IndentationBlockImpl(TracingHashWriter writer)
            {
                _writer = writer;
                _writer._indentLevel++;
            }

            public void Dispose()
            {
                _writer._indentLevel--;
            }
        }
    }

    internal struct LineHashWriter : ILineHashWriter, IHashWriter
    {
        public static readonly char INDENTATION_SYMBOL = '\t';
        
        private StringBuilder _builder;
        private IHashWriter _writer;
        private int _indentLevel;
        private bool _isFirstWrite;

        public LineHashWriter(StringBuilder builder, IHashWriter writer, int indentLevel)
        {
            _builder = builder;
            _writer = writer;
            _indentLevel = indentLevel;
            _isFirstWrite = true;
        }

#region ILineHashWriter

        public ILineHashWriter Trace(string msg)
        {
            TraceValue(msg);
            return this;
        }

        public ILineHashWriter Write(bool value)
        {
            ((IHashWriter) this).Write(value);
            return this;
        }

        public ILineHashWriter Write(byte value)
        {
            ((IHashWriter) this).Write(value);
            return this;
        }

        public ILineHashWriter Write(byte[] value)
        {
            ((IHashWriter) this).Write(value);
            return this;
        }

        public ILineHashWriter Write(char value)
        {
            ((IHashWriter) this).Write(value);
            return this;
        }

        public ILineHashWriter Write(char[] value)
        {
            ((IHashWriter) this).Write(value);
            return this;
        }

        public ILineHashWriter Write(decimal value)
        {
            ((IHashWriter) this).Write(value);
            return this;
        }

        public ILineHashWriter Write(double value)
        {
            ((IHashWriter) this).Write(value);
            return this;
        }

        public ILineHashWriter Write(short value)
        {
            ((IHashWriter) this).Write(value);
            return this;
        }

        public ILineHashWriter Write(int value)
        {
            ((IHashWriter) this).Write(value);
            return this;
        }

        public ILineHashWriter Write(long value)
        {
            ((IHashWriter) this).Write(value);
            return this;
        }

        public ILineHashWriter Write(sbyte value)
        {
            ((IHashWriter) this).Write(value);
            return this;
        }

        public ILineHashWriter Write(float value)
        {
            ((IHashWriter) this).Write(value);
            return this;
        }

        public ILineHashWriter Write(string value)
        {
            ((IHashWriter) this).Write(value);
            return this;
        }

        public ILineHashWriter Write(ushort value)
        {
            ((IHashWriter) this).Write(value);
            return this;
        }

        public ILineHashWriter Write(uint value)
        {
            ((IHashWriter) this).Write(value);
            return this;
        }

        public ILineHashWriter Write(ulong value)
        {
            ((IHashWriter) this).Write(value);
            return this;
        }

#endregion

#region IHashWriter

        void IHashWriter.Write(bool value)
        {
            TraceValue(value);
            _writer.Write(value);
        }

        void IHashWriter.Write(byte value)
        {
            TraceValue(value);
            _writer.Write(value);
        }

        void IHashWriter.Write(byte[] value)
        {
            TraceValue(value);
            _writer.Write(value);
        }

        void IHashWriter.Write(char value)
        {
            TraceValue(value);
            _writer.Write(value);
        }

        void IHashWriter.Write(char[] value)
        {
            TraceValue(value);
            _writer.Write(value);
        }

        void IHashWriter.Write(decimal value)
        {
            TraceValue(value);
            _writer.Write(value);
        }

        void IHashWriter.Write(double value)
        {
            TraceValue(value);
            _writer.Write(value);
        }

        void IHashWriter.Write(short value)
        {
            TraceValue(value);
            _writer.Write(value);
        }

        void IHashWriter.Write(int value)
        {
            TraceValue(value);
            _writer.Write(value);
        }

        void IHashWriter.Write(long value)
        {
            TraceValue(value);
            _writer.Write(value);
        }

        void IHashWriter.Write(sbyte value)
        {
            TraceValue(value);
            _writer.Write(value);
        }

        void IHashWriter.Write(float value)
        {
            TraceValue(value);
            _writer.Write(value);
        }

        void IHashWriter.Write(string value)
        {
            TraceValue(value);
            _writer.Write(value);
        }

        void IHashWriter.Write(ushort value)
        {
            TraceValue(value);
            _writer.Write(value);
        }

        void IHashWriter.Write(uint value)
        {
            TraceValue(value);
            _writer.Write(value);
        }

        void IHashWriter.Write(ulong value)
        {
            TraceValue(value);
            _writer.Write(value);
        }

#endregion

        void TraceValue(object o)
        {
            if (_isFirstWrite)
            {
                _builder.AppendLine();
                _builder.Append(new String(INDENTATION_SYMBOL, _indentLevel));
                _isFirstWrite = false;
            }
            else
            {
                _builder.Append(INDENTATION_SYMBOL);
            }

            _builder.Append(Convert.ToString(o, CultureInfo.InvariantCulture));
        }
    }

    internal class BinaryWriterWrapper : IHashWriter, IDisposable
    {
        public BinaryWriter Writer { get; }

        public BinaryWriterWrapper()
        {
            Writer = new BinaryWriter(new MemoryStream());
        }

        public void Dispose()
        {
            Writer.Dispose();
        }

#region IHashWriter

        public void Write(bool value)
        {
            Writer.Write(value);
        }

        public void Write(byte value)
        {
            Writer.Write(value);
        }

        public void Write(byte[] value)
        {
            Writer.Write(value);
        }

        public void Write(char value)
        {
            Writer.Write(value);
        }

        public void Write(char[] value)
        {
            Writer.Write(value);
        }

        public void Write(decimal value)
        {
            Writer.Write(value);
        }

        public void Write(double value)
        {
            Writer.Write(value);
        }

        public void Write(short value)
        {
            Writer.Write(value);
        }

        public void Write(int value)
        {
            Writer.Write(value);
        }

        public void Write(long value)
        {
            Writer.Write(value);
        }

        public void Write(sbyte value)
        {
            Writer.Write(value);
        }

        public void Write(float value)
        {
            Writer.Write(value);
        }

        public void Write(string value)
        {
            var hash = PlatformIndependentHash.CalculateHash(value);
            Writer.Write(hash);
        }

        public void Write(ushort value)
        {
            Writer.Write(value);
        }

        public void Write(uint value)
        {
            Writer.Write(value);
        }

        public void Write(ulong value)
        {
            Writer.Write(value);
        }

#endregion
    }
    
    
}