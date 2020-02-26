using System;
using System.Collections.Generic;
using Capstones.UnityEngineEx;

namespace Capstones.Net
{
    public struct ListSegment<T> : ICollection<T>, IEnumerable<T>, System.Collections.IEnumerable, IList<T>, IReadOnlyCollection<T>, IReadOnlyList<T>
    {
        public ListSegment(IList<T> list) : this (list, 0, list.Count)
        { }
        public ListSegment(IList<T> list, int offset, int count)
        {
            _List = list;
            _Offset = offset;
            _Count = count;
        }

        private IList<T> _List;
        private int _Count;
        private int _Offset;
        public IList<T> List { get { return _List; } }
        public int Count { get { return _Count; } }
        public int Offset { get { return _Offset; } }

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= _Count)
                {
                    throw new IndexOutOfRangeException();
                }
                return _List[_Offset + index];
            }
            set
            {
                if (index < 0 || index >= _Count)
                {
                    throw new IndexOutOfRangeException();
                }
                _List[_Offset + index] = value;
            }
        }

        void ICollection<T>.Add(T item) { throw new NotSupportedException(); }
        bool ICollection<T>.Remove(T item) { throw new NotSupportedException(); }
        void ICollection<T>.Clear() { throw new NotSupportedException(); }
        bool ICollection<T>.IsReadOnly { get { return true; } }
        public bool Contains(T item)
        {
            for (int i = 0; i < _Count; ++i)
            {
                if (Equals(this[i], item))
                {
                    return true;
                }
            }
            return false;
        }
        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < _Count; ++i)
            {
                yield return this[i];
            }
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            for (int i = 0; i < _Count; ++i)
            {
                array[arrayIndex + i] = this[i];
            }
        }
        public int IndexOf(T item)
        {
            for (int i = 0; i < _Count; ++i)
            {
                if (Equals(item, this[i]))
                {
                    return i;
                }
            }
            return -1;
        }
        void IList<T>.Insert(int index, T item) { throw new NotSupportedException(); }
        void IList<T>.RemoveAt(int index) { throw new NotSupportedException(); }


        public bool Equals(ListSegment<T> obj)
        {
            return obj._List == this._List && obj._Offset == this._Offset && obj._Count == this._Count;
        }
        public override bool Equals(object obj)
        {
            return obj is ListSegment<T> && Equals((ListSegment<T>)obj);
        }
        public override int GetHashCode()
        {
            if (_List != null)
            {
                return _List.GetHashCode() ^ _Offset.GetHashCode() ^ _Count.GetHashCode();
            }
            return 0;
        }

        public static bool operator ==(ListSegment<T> a, ListSegment<T> b)
        {
            return a.Equals(b);
        }
        public static bool operator !=(ListSegment<T> a, ListSegment<T> b)
        {
            return !a.Equals(b);
        }
    }

    public struct ProtobufValue
    {
        public object Parsed;
        public ListSegment<byte> RawData;
    }

    public enum ProtobufLowLevelType
    {
        Varint = 0,
        Fixed64 = 1,
        LengthDelimited = 2,
        Fixed32 = 5,
    }
    public struct ProtobufHighLevelType
    {
        public Type KnownType;
        public string MessageName; // when it is a dynamic message.
    }
    public struct ProtobufFieldDesc
    {
        public int Number;
        public string Name;
        public ProtobufHighLevelType Type;
    }

    public class ProtobufMessage
    {
        public struct FieldSlot
        {
            public ProtobufFieldDesc Desc;
            public ValueList<ProtobufValue> Value;
        }

        protected internal FieldSlot[] _LowFields = new FieldSlot[16];
        protected internal Dictionary<int, FieldSlot> _HighFields = new Dictionary<int, FieldSlot>();
        public ProtobufMessage()
        {
            for (int i = 0; i < 16; ++i)
            {
                _LowFields[i].Desc.Number = i + 1;
            }
        }
    }

    public class ProtobufMessageReader
    {
        public bool ReadVariant(ListSegment<byte> data, out ulong value, out int readbytecount)
        {
            value = 0;
            readbytecount = 0;
            for (int i = 0; i < data.Count; ++i)
            {
                ++readbytecount;
                var b = data[i];
                ulong part = b;
                if (b >= 128)
                {
                    part &= 0x7F;
                }
                part <<= (7 * i);
                value += part;
                if (b < 128)
                {
                    return true;
                }
            }
            return false;
        }
        public bool ReadTag(ListSegment<byte> data, out int number, out ProtobufLowLevelType ltype, out int readbytecount)
        {
            ulong tag;
            if (ReadVariant(data, out tag, out readbytecount))
            {
                number = (int)(tag >> 3);
                ltype = (ProtobufLowLevelType)(tag & 0x7);
                return true;
            }
            number = 0;
            ltype = 0;
            return false;
        }

        //public bool Read(ListSegment<byte> data, out int number, out ProtobufLowLevelType ltype, out ProtobufValue value, out int readbytecount)
        //{
        //    value = new ProtobufValue();
        //}
    }
}