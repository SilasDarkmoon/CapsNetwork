using System;
using System.Collections;
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
        public T[] ToArray()
        {
            var rv = new T[_Count];
            CopyTo(rv, 0);
            return rv;
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

        public ListSegment<T> ConsumeTail(int cnt)
        {
            return new ListSegment<T>(_List, _Offset, _Count - cnt);
        }
        public ListSegment<T> ConsumeHead(int cnt)
        {
            return new ListSegment<T>(_List, _Offset + cnt, _Count - cnt);
        }
    }

    public struct ProtobufParsedValue : IEquatable<ProtobufParsedValue>
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
        private struct OverlappedValue
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public long _Int64Val;
            [System.Runtime.InteropServices.FieldOffset(0)]
            public ulong _UInt64Val;
            [System.Runtime.InteropServices.FieldOffset(0)]
            public float _SingleVal;
            [System.Runtime.InteropServices.FieldOffset(4)]
            public float _SingleValBE;
            [System.Runtime.InteropServices.FieldOffset(0)]
            public double _DoubleVal;
        }

        internal ProtobufNativeType _Type;
        private OverlappedValue _Union;
        internal object _ObjectVal;

        public ProtobufNativeType NativeType
        {
            get { return _Type; }
            internal set { _Type = value; }
        }
        public bool Boolean
        {
            get { return Get<bool>(); }
            set { Set(value); }
        }
        public byte Byte
        {
            get { return Get<byte>(); }
            set { Set(value); }
        }
        public sbyte SByte
        {
            get { return Get<sbyte>(); }
            set { Set(value); }
        }
        public short Int16
        {
            get { return Get<short>(); }
            set { Set(value); }
        }
        public ushort UInt16
        {
            get { return Get<ushort>(); }
            set { Set(value); }
        }
        public int Int32
        {
            get { return Get<int>(); }
            set { Set(value); }
        }
        public uint UInt32
        {
            get { return Get<uint>(); }
            set { Set(value); }
        }
        public long Int64
        {
            get { return Get<long>(); }
            set { Set(value); }
        }
        public ulong UInt64
        {
            get { return Get<ulong>(); }
            set { Set(value); }
        }
        public IntPtr IntPtr
        {
            get { return Get<IntPtr>(); }
            set { Set(value); }
        }
        public UIntPtr UIntPtr
        {
            get { return Get<UIntPtr>(); }
            set { Set(value); }
        }
        public float Single
        {
            get { return Get<float>(); }
            set { Set(value); }
        }
        public double Double
        {
            get { return Get<double>(); }
            set { Set(value); }
        }
        public object Object
        {
            get { return Get<object>(); }
            set { Set(value); }
        }
        public string String
        {
            get { return Get<string>(); }
            set { Set(value); }
        }
        public byte[] Bytes
        {
            get { return Get<byte[]>(); }
            set { Set(value); }
        }
        public ProtobufUnknowValue Unknown
        {
            get { return Get<ProtobufUnknowValue>(); }
            set { Set(value); }
        }
        public ProtobufMessage Message
        {
            get { return Get<ProtobufMessage>(); }
            set { Set(value); }
        }

        public T GetEnum<T>() where T : struct
        {
            return _EnumAccessor.GetEnum<T>(ref this);
        }
        public void SetEnum<T>(T val) where T : struct
        {
            _EnumAccessor.SetEnum<T>(ref this, val);
        }

        public ProtobufParsedValue(ProtobufNativeType ntype)
            : this()
        {
            _Type = ntype;
        }

        public bool IsEmpty
        {
            get { return _Type == 0; }
        }
        private static HashSet<ProtobufNativeType> _ObjNativeTypes = new HashSet<ProtobufNativeType>()
        {
            ProtobufNativeType.TYPE_BYTES,
            ProtobufNativeType.TYPE_GROUP,
            ProtobufNativeType.TYPE_MESSAGE,
            ProtobufNativeType.TYPE_STRING,
            ProtobufNativeType.TYPE_UNKNOWN,
        };
        public bool IsObject
        {
            get { return _ObjNativeTypes.Contains(_Type); }
        }
        #region Accessors
        private interface IProtobufParsedValueAccessor
        {
            object Get(ref ProtobufParsedValue pval);
            bool Set(ref ProtobufParsedValue pval, object val);
        }
        private abstract class ProtobufParsedValueAccessor<T> : IProtobufParsedValueAccessor
        {
            public abstract bool Get(ref ProtobufParsedValue pval, out T val);
            public abstract bool Set(ref ProtobufParsedValue pval, T val);
            public T Get(ref ProtobufParsedValue pval)
            {
                T val;
                Get(ref pval, out val);
                return val;
            }
            object IProtobufParsedValueAccessor.Get(ref ProtobufParsedValue pval)
            {
                return Get(ref pval);
            }
            public bool Set(ref ProtobufParsedValue pval, object val)
            {
                if (val is T)
                {
                    return Set(ref pval, (T)val);
                }
                return false;
            }
        }
        private class ProtobufParsedBooleanAccessor : ProtobufParsedValueAccessor<bool>
        {
            public override bool Get(ref ProtobufParsedValue pval, out bool val)
            {
                if (pval.IsObject)
                {
                    val = default(bool);
                    return false;
                }
                else
                {
                    val = pval._Union._UInt64Val != 0;
                    return true;
                }
            }
            public override bool Set(ref ProtobufParsedValue pval, bool val)
            {
                if (pval.IsEmpty)
                {
                    pval._Type = ProtobufNativeType.TYPE_BOOL;
                }
                if (pval.IsObject)
                {
                    return false;
                }
                else
                {
                    pval._Union._Int64Val = val ? -1L : 0L;
                    return true;
                }
            }
        }
        private class ProtobufParsedByteAccessor : ProtobufParsedValueAccessor<byte>
        {
            public override bool Get(ref ProtobufParsedValue pval, out byte val)
            {
                if (pval.IsObject)
                {
                    val = default(byte);
                    return false;
                }
                else
                {
                    val = (byte)pval._Union._UInt64Val;
                    return true;
                }
            }
            public override bool Set(ref ProtobufParsedValue pval, byte val)
            {
                if (pval.IsEmpty)
                {
                    pval._Type = ProtobufNativeType.TYPE_UINT32;
                }
                if (pval.IsObject)
                {
                    return false;
                }
                else
                {
                    pval._Union._UInt64Val = val;
                    return true;
                }
            }
        }
        private class ProtobufParsedSByteAccessor : ProtobufParsedValueAccessor<sbyte>
        {
            public override bool Get(ref ProtobufParsedValue pval, out sbyte val)
            {
                if (pval.IsObject)
                {
                    val = default(sbyte);
                    return false;
                }
                else
                {
                    val = (sbyte)pval._Union._Int64Val;
                    return true;
                }
            }
            public override bool Set(ref ProtobufParsedValue pval, sbyte val)
            {
                if (pval.IsEmpty)
                {
                    pval._Type = ProtobufNativeType.TYPE_INT32;
                }
                if (pval.IsObject)
                {
                    return false;
                }
                else
                {
                    pval._Union._Int64Val = val;
                    return true;
                }
            }
        }
        private class ProtobufParsedInt16Accessor : ProtobufParsedValueAccessor<short>
        {
            public override bool Get(ref ProtobufParsedValue pval, out short val)
            {
                if (pval.IsObject)
                {
                    val = default(short);
                    return false;
                }
                else
                {
                    val = (short)pval._Union._Int64Val;
                    return true;
                }
            }
            public override bool Set(ref ProtobufParsedValue pval, short val)
            {
                if (pval.IsEmpty)
                {
                    pval._Type = ProtobufNativeType.TYPE_INT32;
                }
                if (pval.IsObject)
                {
                    return false;
                }
                else
                {
                    pval._Union._Int64Val = val;
                    return true;
                }
            }
        }
        private class ProtobufParsedUInt16Accessor : ProtobufParsedValueAccessor<ushort>
        {
            public override bool Get(ref ProtobufParsedValue pval, out ushort val)
            {
                if (pval.IsObject)
                {
                    val = default(ushort);
                    return false;
                }
                else
                {
                    val = (ushort)pval._Union._UInt64Val;
                    return true;
                }
            }
            public override bool Set(ref ProtobufParsedValue pval, ushort val)
            {
                if (pval.IsEmpty)
                {
                    pval._Type = ProtobufNativeType.TYPE_UINT32;
                }
                if (pval.IsObject)
                {
                    return false;
                }
                else
                {
                    pval._Union._UInt64Val = val;
                    return true;
                }
            }
        }
        private class ProtobufParsedInt32Accessor : ProtobufParsedValueAccessor<int>
        {
            public override bool Get(ref ProtobufParsedValue pval, out int val)
            {
                if (pval.IsObject)
                {
                    val = default(int);
                    return false;
                }
                else
                {
                    val = (int)pval._Union._Int64Val;
                    return true;
                }
            }
            public override bool Set(ref ProtobufParsedValue pval, int val)
            {
                if (pval.IsEmpty)
                {
                    pval._Type = ProtobufNativeType.TYPE_INT32;
                }
                if (pval.IsObject)
                {
                    return false;
                }
                else
                {
                    pval._Union._Int64Val = val;
                    return true;
                }
            }
        }
        private class ProtobufParsedUInt32Accessor : ProtobufParsedValueAccessor<uint>
        {
            public override bool Get(ref ProtobufParsedValue pval, out uint val)
            {
                if (pval.IsObject)
                {
                    val = default(uint);
                    return false;
                }
                else
                {
                    val = (uint)pval._Union._UInt64Val;
                    return true;
                }
            }
            public override bool Set(ref ProtobufParsedValue pval, uint val)
            {
                if (pval.IsEmpty)
                {
                    pval._Type = ProtobufNativeType.TYPE_UINT32;
                }
                if (pval.IsObject)
                {
                    return false;
                }
                else
                {
                    pval._Union._UInt64Val = val;
                    return true;
                }
            }
        }
        private class ProtobufParsedInt64Accessor : ProtobufParsedValueAccessor<long>
        {
            public override bool Get(ref ProtobufParsedValue pval, out long val)
            {
                if (pval.IsObject)
                {
                    val = default(long);
                    return false;
                }
                else
                {
                    val = pval._Union._Int64Val;
                    return true;
                }
            }
            public override bool Set(ref ProtobufParsedValue pval, long val)
            {
                if (pval.IsEmpty)
                {
                    pval._Type = ProtobufNativeType.TYPE_INT64;
                }
                if (pval.IsObject)
                {
                    return false;
                }
                else
                {
                    pval._Union._Int64Val = val;
                    return true;
                }
            }
        }
        private class ProtobufParsedUInt64Accessor : ProtobufParsedValueAccessor<ulong>
        {
            public override bool Get(ref ProtobufParsedValue pval, out ulong val)
            {
                if (pval.IsObject)
                {
                    val = default(ulong);
                    return false;
                }
                else
                {
                    val = pval._Union._UInt64Val;
                    return true;
                }
            }
            public override bool Set(ref ProtobufParsedValue pval, ulong val)
            {
                if (pval.IsEmpty)
                {
                    pval._Type = ProtobufNativeType.TYPE_UINT64;
                }
                if (pval.IsObject)
                {
                    return false;
                }
                else
                {
                    pval._Union._UInt64Val = val;
                    return true;
                }
            }
        }
        private class ProtobufParsedIntPtrAccessor : ProtobufParsedValueAccessor<IntPtr>
        {
            public override bool Get(ref ProtobufParsedValue pval, out IntPtr val)
            {
                if (pval.IsObject)
                {
                    val = default(IntPtr);
                    return false;
                }
                else
                {
                    val = (IntPtr)pval._Union._Int64Val;
                    return true;
                }
            }
            public override bool Set(ref ProtobufParsedValue pval, IntPtr val)
            {
                if (pval.IsEmpty)
                {
                    if (IntPtr.Size > 4)
                    {
                        pval._Type = ProtobufNativeType.TYPE_INT64;
                    }
                    else
                    {
                        pval._Type = ProtobufNativeType.TYPE_INT32;
                    }
                }
                if (pval.IsObject)
                {
                    return false;
                }
                else
                {
                    pval._Union._Int64Val = (long)val;
                    return true;
                }
            }
        }
        private class ProtobufParsedUIntPtrAccessor : ProtobufParsedValueAccessor<UIntPtr>
        {
            public override bool Get(ref ProtobufParsedValue pval, out UIntPtr val)
            {
                if (pval.IsObject)
                {
                    val = default(UIntPtr);
                    return false;
                }
                else
                {
                    val = (UIntPtr)pval._Union._UInt64Val;
                    return true;
                }
            }
            public override bool Set(ref ProtobufParsedValue pval, UIntPtr val)
            {
                if (pval.IsEmpty)
                {
                    if (UIntPtr.Size > 4)
                    {
                        pval._Type = ProtobufNativeType.TYPE_UINT64;
                    }
                    else
                    {
                        pval._Type = ProtobufNativeType.TYPE_UINT32;
                    }
                }
                if (pval.IsObject)
                {
                    return false;
                }
                else
                {
                    pval._Union._UInt64Val = (ulong)val;
                    return true;
                }
            }
        }
        private class ProtobufParsedSingleAccessor : ProtobufParsedValueAccessor<float>
        {
            public override bool Get(ref ProtobufParsedValue pval, out float val)
            {
                if (pval.IsObject)
                {
                    val = default(float);
                    return false;
                }
                else
                {
                    if (BitConverter.IsLittleEndian)
                    {
                        val = pval._Union._SingleVal;
                    }
                    else
                    {
                        val = pval._Union._SingleValBE;
                    }
                    return true;
                }
            }
            public override bool Set(ref ProtobufParsedValue pval, float val)
            {
                if (pval.IsEmpty)
                {
                    pval._Type = ProtobufNativeType.TYPE_FLOAT;
                }
                if (pval.IsObject)
                {
                    return false;
                }
                else
                {
                    if (BitConverter.IsLittleEndian)
                    {
                        pval._Union._SingleVal = val;
                    }
                    else
                    {
                        pval._Union._SingleValBE = val;
                    }
                    return true;
                }
            }
        }
        private class ProtobufParsedDoubleAccessor : ProtobufParsedValueAccessor<double>
        {
            public override bool Get(ref ProtobufParsedValue pval, out double val)
            {
                if (pval.IsObject)
                {
                    val = default(double);
                    return false;
                }
                else
                {
                    val = pval._Union._DoubleVal;
                    return true;
                }
            }
            public override bool Set(ref ProtobufParsedValue pval, double val)
            {
                if (pval.IsEmpty)
                {
                    pval._Type = ProtobufNativeType.TYPE_DOUBLE;
                }
                if (pval.IsObject)
                {
                    return false;
                }
                else
                {
                    pval._Union._DoubleVal = val;
                    return true;
                }
            }
        }
        private class ProtobufParsedObjectAccessor : IProtobufParsedValueAccessor
        {
            public bool Get(ref ProtobufParsedValue pval, out object val)
            {
                if (pval.IsObject)
                {
                    val = pval._ObjectVal;
                    return true;
                }
                else
                {
                    val = null;
                    return false;
                }
            }
            public bool Set(ref ProtobufParsedValue pval, object val)
            {
                if (pval.IsEmpty)
                {
                    if (val != null)
                    {
                        pval._ObjectVal = val;
                        if (val is string)
                        {
                            pval._Type = ProtobufNativeType.TYPE_STRING;
                        }
                        else if (val is byte[])
                        {
                            pval._Type = ProtobufNativeType.TYPE_BYTES;
                        }
                        else if (val is ProtobufUnknowValue)
                        {
                            pval._Type = ProtobufNativeType.TYPE_UNKNOWN;
                        }
                        else
                        {
                            pval._Type = ProtobufNativeType.TYPE_MESSAGE;
                        }
                    }
                    return true;
                }
                if (pval.IsObject)
                {
                    pval._ObjectVal = val;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            public T Get<T>(ref ProtobufParsedValue pval)
            {
                object val;
                if (Get(ref pval, out val))
                {
                    if (val is T)
                    {
                        return (T)val;
                    }
                }
                return default(T);
            }
            public void Set<T>(ref ProtobufParsedValue pval, T val)
            {
                Set(ref pval, (object)val);
            }
            public object Get(ref ProtobufParsedValue pval)
            {
                object val;
                if (Get(ref pval, out val))
                {
                    return val;
                }
                return null;
            }
        }
        private class ProtobufParsedEnumAccessor : IProtobufParsedValueAccessor
        {
            public bool Get(ref ProtobufParsedValue pval, out object val)
            {
                if (pval._Type == ProtobufNativeType.TYPE_ENUM)
                {
                    if (pval._ObjectVal is Type)
                    {
                        val = Enum.ToObject(pval._ObjectVal as Type, pval._Union._UInt64Val);
                    }
                    else
                    {
                        val = pval._Union._UInt64Val;
                    }
                    return true;
                }
                else
                {
                    val = null;
                    return false;
                }
            }
            public bool Set(ref ProtobufParsedValue pval, object val)
            {
                if (val is Enum)
                {
                    if (pval._Type == 0 || pval._Type == ProtobufNativeType.TYPE_ENUM)
                    {
                        pval._Type = ProtobufNativeType.TYPE_ENUM;
                        pval._ObjectVal = val.GetType();
                        pval._Union._UInt64Val = Convert.ToUInt64(val);
                        return true;
                    }
                }
                return false;
            }
            public T Get<T>(ref ProtobufParsedValue pval)
            {
                object val;
                if (Get(ref pval, out val))
                {
                    if (val is T)
                    {
                        return (T)val;
                    }
                    else if (val is ulong)
                    {
                        if (typeof(T).IsEnum)
                        {
                            return (T)Enum.ToObject(typeof(T), (ulong)val);
                        }
                    }
                }
                return default(T);
            }
            public void Set<T>(ref ProtobufParsedValue pval, T val)
            {
                Set(ref pval, (object)val);
            }
            public T GetEnum<T>(ref ProtobufParsedValue pval) where T : struct
            {
                if (!pval.IsObject)
                {
                    var raw = pval.UInt64;
                    return EnumUtils.ConvertToEnum<T>(raw);
                }
                return default(T);
            }
            public void SetEnum<T>(ref ProtobufParsedValue pval, T val) where T : struct
            {
                if (pval._Type == 0 || pval._Type == ProtobufNativeType.TYPE_ENUM)
                {
                    pval._Type = ProtobufNativeType.TYPE_ENUM;
                    pval._ObjectVal = typeof(T);
                    pval._Union._UInt64Val = EnumUtils.ConvertFromEnum<T>(val);
                }
            }
            public object Get(ref ProtobufParsedValue pval)
            {
                object val;
                if (Get(ref pval, out val))
                {
                    return val;
                }
                return null;
            }
        }
        private static ProtobufParsedObjectAccessor _ObjAccessor = new ProtobufParsedObjectAccessor();
        private static ProtobufParsedEnumAccessor _EnumAccessor = new ProtobufParsedEnumAccessor();
        private static ProtobufParsedBooleanAccessor _BooleanAccessor = new ProtobufParsedBooleanAccessor();
        private static ProtobufParsedByteAccessor _ByteAccessor = new ProtobufParsedByteAccessor();
        private static ProtobufParsedSByteAccessor _SByteAccessor = new ProtobufParsedSByteAccessor();
        private static ProtobufParsedInt16Accessor _Int16Accessor = new ProtobufParsedInt16Accessor();
        private static ProtobufParsedUInt16Accessor _UInt16Accessor = new ProtobufParsedUInt16Accessor();
        private static ProtobufParsedInt32Accessor _Int32Accessor = new ProtobufParsedInt32Accessor();
        private static ProtobufParsedUInt32Accessor _UInt32Accessor = new ProtobufParsedUInt32Accessor();
        private static ProtobufParsedInt64Accessor _Int64Accessor = new ProtobufParsedInt64Accessor();
        private static ProtobufParsedUInt64Accessor _UInt64Accessor = new ProtobufParsedUInt64Accessor();
        private static ProtobufParsedIntPtrAccessor _IntPtrAccessor = new ProtobufParsedIntPtrAccessor();
        private static ProtobufParsedUIntPtrAccessor _UIntPtrAccessor = new ProtobufParsedUIntPtrAccessor();
        private static ProtobufParsedSingleAccessor _SingleAccessor = new ProtobufParsedSingleAccessor();
        private static ProtobufParsedDoubleAccessor _DoubleAccessor = new ProtobufParsedDoubleAccessor();
        private static Dictionary<Type, IProtobufParsedValueAccessor> _TypedAccessors = new Dictionary<Type, IProtobufParsedValueAccessor>()
        {
            { typeof(bool), _BooleanAccessor },
            { typeof(byte), _ByteAccessor },
            { typeof(sbyte), _SByteAccessor },
            { typeof(short), _Int16Accessor },
            { typeof(ushort), _UInt16Accessor },
            { typeof(int), _Int32Accessor },
            { typeof(uint), _UInt32Accessor },
            { typeof(long), _Int64Accessor },
            { typeof(ulong), _UInt64Accessor },
            { typeof(IntPtr), _IntPtrAccessor },
            { typeof(UIntPtr), _UIntPtrAccessor },
            { typeof(float), _SingleAccessor },
            { typeof(double), _DoubleAccessor },
        };
        private static Dictionary<ProtobufNativeType, IProtobufParsedValueAccessor> _NativeAccessors = new Dictionary<ProtobufNativeType, IProtobufParsedValueAccessor>()
        {
            { ProtobufNativeType.TYPE_BOOL, _BooleanAccessor },
            { ProtobufNativeType.TYPE_BYTES, _ObjAccessor },
            { ProtobufNativeType.TYPE_DOUBLE, _DoubleAccessor },
            { ProtobufNativeType.TYPE_ENUM, _EnumAccessor },
            { ProtobufNativeType.TYPE_FIXED32, _UInt32Accessor },
            { ProtobufNativeType.TYPE_FIXED64, _UInt64Accessor },
            { ProtobufNativeType.TYPE_FLOAT, _SingleAccessor },
            { ProtobufNativeType.TYPE_GROUP, _ObjAccessor },
            { ProtobufNativeType.TYPE_INT32, _Int32Accessor },
            { ProtobufNativeType.TYPE_INT64, _Int64Accessor },
            { ProtobufNativeType.TYPE_MESSAGE, _ObjAccessor },
            { ProtobufNativeType.TYPE_SFIXED32, _Int32Accessor },
            { ProtobufNativeType.TYPE_SFIXED64, _Int64Accessor },
            { ProtobufNativeType.TYPE_SINT32, _Int32Accessor },
            { ProtobufNativeType.TYPE_SINT64, _Int64Accessor },
            { ProtobufNativeType.TYPE_STRING, _ObjAccessor },
            { ProtobufNativeType.TYPE_UINT32, _UInt32Accessor },
            { ProtobufNativeType.TYPE_UINT64, _UInt64Accessor },
            { ProtobufNativeType.TYPE_UNKNOWN, _ObjAccessor },
        };
        #endregion
        public T Get<T>()
        {
            var type = typeof(T);
            if (type.IsEnum)
            {
                return _EnumAccessor.Get<T>(ref this);
            }
            else
            {
                IProtobufParsedValueAccessor accessor;
                if (_TypedAccessors.TryGetValue(type, out accessor))
                {
                    var taccessor = accessor as ProtobufParsedValueAccessor<T>;
                    if (taccessor != null)
                    {
                        return taccessor.Get(ref this);
                    }
                }
            }
            var obj = _ObjAccessor.Get<T>(ref this);
            return obj;
        }
        public object Get(Type type)
        {
            if (type == null)
            {
                return Get();
            }
            else if (type.IsEnum)
            {
                return _EnumAccessor.Get(ref this);
            }
            else
            {
                IProtobufParsedValueAccessor accessor;
                if (_TypedAccessors.TryGetValue(type, out accessor))
                {
                    return accessor.Get(ref this);
                }
            }
            var obj = _ObjAccessor.Get(ref this);
            if (type.IsInstanceOfType(obj))
            {
                return obj;
            }
            return null;
        }
        public object Get()
        {
            IProtobufParsedValueAccessor accessor;
            if (_NativeAccessors.TryGetValue(_Type, out accessor))
            {
                return accessor.Get(ref this);
            }
            return null;
        }
        public void Set<T>(T val)
        {
            var type = typeof(T);
            if (type.IsEnum)
            {
                _EnumAccessor.Set<T>(ref this, val);
                return;
            }
            else
            {
                IProtobufParsedValueAccessor accessor;
                if (_TypedAccessors.TryGetValue(type, out accessor))
                {
                    var taccessor = accessor as ProtobufParsedValueAccessor<T>;
                    if (taccessor != null)
                    {
                        taccessor.Set(ref this, val);
                        return;
                    }
                }
            }
            _ObjAccessor.Set<T>(ref this, val);
        }
        public void Set(object val)
        {
            if (val == null)
            {
                _Union._UInt64Val = 0;
                _ObjectVal = null;
            }
            else
            {
                if (val is Enum)
                {
                    _EnumAccessor.Set(ref this, val);
                    return;
                }
                else
                {
                    IProtobufParsedValueAccessor accessor;
                    if (_TypedAccessors.TryGetValue(val.GetType(), out accessor))
                    {
                        accessor.Set(ref this, val);
                        return;
                    }
                }
                _ObjAccessor.Set(ref this, val);
            }
        }

        public bool Equals(ProtobufParsedValue other)
        {
            return _Type == other._Type && _Union._Int64Val == other._Union._Int64Val && Equals(_ObjectVal, other._ObjectVal);
        }
        public override bool Equals(object obj)
        {
            return obj is ProtobufParsedValue && Equals((ProtobufParsedValue)obj);
        }
        public override int GetHashCode()
        {
            return _Type.GetHashCode() ^ _Union._Int64Val.GetHashCode() ^ (_ObjectVal == null ? 0 : _ObjectVal.GetHashCode());
        }
        public static bool operator==(ProtobufParsedValue v1, ProtobufParsedValue v2)
        {
            return v1.Equals(v2);
        }
        public static bool operator!=(ProtobufParsedValue v1, ProtobufParsedValue v2)
        {
            return !v1.Equals(v2);
        }

        #region Converters
        public static implicit operator ProtobufMessage(ProtobufParsedValue thiz)
        {
            return thiz.Get<ProtobufMessage>();
        }
        public static implicit operator ProtobufParsedValue(ProtobufMessage val)
        {
            var pval = new ProtobufParsedValue();
            pval.Set(val);
            return pval;
        }
        public static implicit operator string(ProtobufParsedValue thiz)
        {
            return thiz.Get<string>();
        }
        public static implicit operator ProtobufParsedValue(string val)
        {
            var pval = new ProtobufParsedValue();
            pval.Set(val);
            return pval;
        }
        public static implicit operator byte[](ProtobufParsedValue thiz)
        {
            return thiz.Get<byte[]>();
        }
        public static implicit operator ProtobufParsedValue(byte[] val)
        {
            var pval = new ProtobufParsedValue();
            pval.Set(val);
            return pval;
        }
        public static implicit operator ProtobufUnknowValue(ProtobufParsedValue thiz)
        {
            return thiz.Get<ProtobufUnknowValue>();
        }
        public static implicit operator ProtobufParsedValue(ProtobufUnknowValue val)
        {
            var pval = new ProtobufParsedValue();
            pval.Set(val);
            return pval;
        }
        public static implicit operator Enum(ProtobufParsedValue thiz)
        {
            return thiz.Get<Enum>();
        }
        public static implicit operator ProtobufParsedValue(Enum val)
        {
            var pval = new ProtobufParsedValue();
            pval.Set(val);
            return pval;
        }
        public static implicit operator bool(ProtobufParsedValue thiz)
        {
            return thiz.Get<bool>();
        }
        public static implicit operator ProtobufParsedValue(bool val)
        {
            var pval = new ProtobufParsedValue();
            pval.Set(val);
            return pval;
        }
        public static implicit operator byte(ProtobufParsedValue thiz)
        {
            return thiz.Get<byte>();
        }
        public static implicit operator ProtobufParsedValue(byte val)
        {
            var pval = new ProtobufParsedValue();
            pval.Set(val);
            return pval;
        }
        public static implicit operator sbyte(ProtobufParsedValue thiz)
        {
            return thiz.Get<sbyte>();
        }
        public static implicit operator ProtobufParsedValue(sbyte val)
        {
            var pval = new ProtobufParsedValue();
            pval.Set(val);
            return pval;
        }
        public static implicit operator short(ProtobufParsedValue thiz)
        {
            return thiz.Get<short>();
        }
        public static implicit operator ProtobufParsedValue(short val)
        {
            var pval = new ProtobufParsedValue();
            pval.Set(val);
            return pval;
        }
        public static implicit operator ushort(ProtobufParsedValue thiz)
        {
            return thiz.Get<ushort>();
        }
        public static implicit operator ProtobufParsedValue(ushort val)
        {
            var pval = new ProtobufParsedValue();
            pval.Set(val);
            return pval;
        }
        public static implicit operator int(ProtobufParsedValue thiz)
        {
            return thiz.Get<int>();
        }
        public static implicit operator ProtobufParsedValue(int val)
        {
            var pval = new ProtobufParsedValue();
            pval.Set(val);
            return pval;
        }
        public static implicit operator uint(ProtobufParsedValue thiz)
        {
            return thiz.Get<uint>();
        }
        public static implicit operator ProtobufParsedValue(uint val)
        {
            var pval = new ProtobufParsedValue();
            pval.Set(val);
            return pval;
        }
        public static implicit operator long(ProtobufParsedValue thiz)
        {
            return thiz.Get<long>();
        }
        public static implicit operator ProtobufParsedValue(long val)
        {
            var pval = new ProtobufParsedValue();
            pval.Set(val);
            return pval;
        }
        public static implicit operator ulong(ProtobufParsedValue thiz)
        {
            return thiz.Get<ulong>();
        }
        public static implicit operator ProtobufParsedValue(ulong val)
        {
            var pval = new ProtobufParsedValue();
            pval.Set(val);
            return pval;
        }
        public static implicit operator IntPtr(ProtobufParsedValue thiz)
        {
            return thiz.Get<IntPtr>();
        }
        public static implicit operator ProtobufParsedValue(IntPtr val)
        {
            var pval = new ProtobufParsedValue();
            pval.Set(val);
            return pval;
        }
        public static implicit operator UIntPtr(ProtobufParsedValue thiz)
        {
            return thiz.Get<UIntPtr>();
        }
        public static implicit operator ProtobufParsedValue(UIntPtr val)
        {
            var pval = new ProtobufParsedValue();
            pval.Set(val);
            return pval;
        }
        public static implicit operator float(ProtobufParsedValue thiz)
        {
            return thiz.Get<float>();
        }
        public static implicit operator ProtobufParsedValue(float val)
        {
            var pval = new ProtobufParsedValue();
            pval.Set(val);
            return pval;
        }
        public static implicit operator double(ProtobufParsedValue thiz)
        {
            return thiz.Get<double>();
        }
        public static implicit operator ProtobufParsedValue(double val)
        {
            var pval = new ProtobufParsedValue();
            pval.Set(val);
            return pval;
        }
        #endregion
    }

    public struct ProtobufValue
    {
        public ProtobufParsedValue Parsed;
        public ListSegment<byte> RawData;

        public ProtobufValue(ProtobufNativeType ntype)
        {
            Parsed = new ProtobufParsedValue() { _Type = ntype };
            RawData = new ListSegment<byte>();
        }

        public bool IsValid
        {
            get
            {
                return !Parsed.IsEmpty || RawData.List != null;
            }
        }

        public override string ToString()
        {
            if (!Parsed.IsEmpty)
            {
                return Parsed.Get().ToString();
            }
            else if (!IsValid)
            {
                return "*Invalid*";
            }
            else
            {
                return string.Format("*RawData[{0}]*", RawData.Count);
            }
        }
    }
    public class ProtobufUnknowValue
    {
        public ListSegment<byte> Raw;
        public override string ToString()
        {
            return string.Format("*Unknown[{0}]*", Raw == null ? 0 : Raw.Count);
        }
    }

    public enum ProtobufLowLevelType
    {
        Varint = 0,
        Fixed64 = 1,
        LengthDelimited = 2,
        Fixed32 = 5,
        // TODO: support group?
    }
    public enum ProtobufNativeType : int
    {
        // 0 is reserved for errors.
        // Order is weird for historical reasons.
        TYPE_DOUBLE = 1,
        TYPE_FLOAT = 2,
        // Not ZigZag encoded.  Negative numbers take 10 bytes.  Use TYPE_SINT64 if
        // negative values are likely.
        TYPE_INT64 = 3,
        TYPE_UINT64 = 4,
        // Not ZigZag encoded.  Negative numbers take 10 bytes.  Use TYPE_SINT32 if
        // negative values are likely.
        TYPE_INT32 = 5,
        TYPE_FIXED64 = 6,
        TYPE_FIXED32 = 7,
        TYPE_BOOL = 8,
        TYPE_STRING = 9,
        // Tag-delimited aggregate.
        // Group type is deprecated and not supported in proto3. However, Proto3
        // implementations should still be able to parse the group wire format and
        // treat group fields as unknown fields.
        TYPE_GROUP = 10,
        TYPE_MESSAGE = 11,  // Length-delimited aggregate.

        // New in version 2.
        TYPE_BYTES = 12,
        TYPE_UINT32 = 13,
        TYPE_ENUM = 14,
        TYPE_SFIXED32 = 15,
        TYPE_SFIXED64 = 16,
        TYPE_SINT32 = 17,  // Uses ZigZag encoding.
        TYPE_SINT64 = 18,  // Uses ZigZag encoding.

        TYPE_EMPTY = 0,
        TYPE_UNKNOWN = 255,
    };
    public enum ProtobufFieldLabel
    {
        LABEL_OPTIONAL = 1,
        LABEL_REQUIRED = 2,
        LABEL_REPEATED = 3,
    }
    public struct ProtobufHighLevelType
    {
        public ProtobufNativeType KnownType;
        private object _TypeDesc;
        public string MessageName
        {
            get { return _TypeDesc as string; }
            set { _TypeDesc = value; }
        }
        public Type CLRType
        {
            get { return _TypeDesc as Type; }
            set { _TypeDesc = value; }
        }
        public object TypeDesc
        {
            get { return _TypeDesc; }
            set { _TypeDesc = value; }
        }
    }
    public struct ProtobufFieldDesc
    {
        public int Number;
        public string Name;
        public ProtobufHighLevelType Type;
        public ProtobufFieldLabel Label;
    }
    public class ProtobufMessage // TODO: IDictionary
    {
        protected internal class FieldSlot
        {
            public ProtobufFieldDesc Desc;
            public ValueList<ProtobufValue> Values;

            public ProtobufValue FirstValue
            {
                get
                {
                    if (Values.Count > 0)
                    {
                        return Values[0];
                    }
                    return default(ProtobufValue);
                }
                set
                {
                    if (Values.Count > 0)
                    {
                        Values[0] = value;
                    }
                    else
                    {
                        Values.Add(value);
                    }
                }
            }
        }

        protected internal FieldSlot[] _LowFields = new FieldSlot[16];
        protected internal Dictionary<int, FieldSlot> _HighFields = new Dictionary<int, FieldSlot>();
        public ProtobufMessage()
        {
            for (int i = 0; i < 16; ++i)
            {
                _LowFields[i] = new FieldSlot() { Desc = new ProtobufFieldDesc() { Number = i + 1 } };
            }
            Slots = new SlotAccessor(this);
        }
        public ProtobufMessage(ProtobufMessage template)
            : this()
        {
            ApplyTemplate(template);
        }
        protected internal FieldSlot GetSlot(int num)
        {
            if (num <= 0)
            {
                return null;
            }
            else if (num <= 16)
            {
                return _LowFields[num - 1];
            }
            else
            {
                FieldSlot value;
                _HighFields.TryGetValue(num, out value);
                return value;
            }
        }
        protected internal FieldSlot GetOrCreateSlot(int num)
        {
            //if (num <= 0)
            //{ // this should not happen
            //    return new FieldSlot();
            //}
            //else
            if (num <= 16)
            {
                return _LowFields[num - 1];
            }
            else
            {
                FieldSlot value;
                _HighFields.TryGetValue(num, out value);
                if (value == null)
                {
                    _HighFields[num] = value = new FieldSlot() { Desc = new ProtobufFieldDesc() { Number = num } };
                }
                return value;
            }
        }

        protected internal SlotAccessor Slots;
        protected internal struct SlotAccessor : IEnumerable<FieldSlot>
        {
            private ProtobufMessage _Parent;
            public SlotAccessor(ProtobufMessage parent)
            {
                _Parent = parent;
            }

            public FieldSlot this[int index]
            {
                get
                {
                    return _Parent.GetOrCreateSlot(index);
                }
            }

            public IEnumerator<FieldSlot> GetEnumerator()
            {
                for (int i = 0; i < 16; ++i)
                {
                    var slot = _Parent._LowFields[i];
                    //if (slot.Desc.Name != null || slot.Values.Count > 1 || slot.FirstValue.Parsed != null)
                    {
                        yield return slot;
                    }
                }
                foreach (var kvpslot in _Parent._HighFields)
                {
                    var slot = kvpslot.Value;
                    //if (slot.Desc.Name != null || slot.Values.Count > 1 || slot.FirstValue.Parsed != null)
                    {
                        yield return slot;
                    }
                }
            }
            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        protected static void WriteToJson(System.Text.StringBuilder sb, FieldSlot slot, int indent, HashSet<ProtobufMessage> alreadyHandledNodes)
        {
            bool shouldWriteSlot = slot.Values.Count > 0 || slot.Desc.Name != null;
            if (shouldWriteSlot)
            {
                { // key
                    if (indent >= 0)
                    {
                        sb.AppendLine();
                        sb.Append(' ', indent * 4 + 4);
                    }
                    sb.Append('"');
                    if (slot.Desc.Name != null)
                    {
                        sb.Append(slot.Desc.Name);
                    }
                    else
                    {
                        sb.Append(slot.Desc.Number);
                    }
                    sb.Append('"');
                    if (indent >= 0)
                    {
                        sb.Append(" ");
                    }
                    sb.Append(":");
                    if (indent >= 0)
                    {
                        sb.Append(" ");
                    }
                }
                { // value
                    if (slot.Values.Count <= 0)
                    {
                        sb.Append("null");
                    }
                    else if (slot.Values.Count > 1)
                    { // array
                        int startindex = sb.Length;
                        sb.Append("[");
                        for (int j = 0; j < slot.Values.Count; ++j)
                        {
                            if (indent >= 0)
                            {
                                sb.Append(" ");
                            }
                            var val = slot.Values[j];
                            WriteToJson(sb, val, indent < 0 ? indent : indent + 2, alreadyHandledNodes);
                            sb.Append(",");
                        }
                        { // eat last ','
                            if (sb[sb.Length - 1] == ',')
                            {
                                sb.Remove(sb.Length - 1, 1);
                            }
                        }
                        if (indent >= 0)
                        {
                            bool newline = false;
                            for (int j = startindex; j < sb.Length; ++j)
                            {
                                var ch = sb[j];
                                if (ch == '\r' || ch == '\n')
                                {
                                    newline = true;
                                    break;
                                }
                            }
                            if (newline)
                            {
                                sb.Insert(startindex, " ", indent * 4 + 4);
                                sb.Insert(startindex, Environment.NewLine);
                                sb.AppendLine();
                                sb.Append(' ', indent * 4 + 4);
                            }
                            else
                            {
                                sb.Append(" ");
                            }
                        }
                        sb.Append("]");
                    }
                    else
                    {
                        WriteToJson(sb, slot.FirstValue, indent < 0 ? indent : indent + 1, alreadyHandledNodes);
                    }
                }
                sb.Append(",");
            }
        }
        protected internal static HashSet<Type> _NumericTypes = new HashSet<Type>()
        {
            typeof(byte),
            typeof(sbyte),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            //typeof(IntPtr),
            //typeof(UIntPtr),
            typeof(float),
            typeof(double),
            typeof(decimal),
        };
        protected static char[] _LineEndings = new[] { '\r', '\n' };
        protected static void WriteToJson(System.Text.StringBuilder sb, ProtobufValue value, int indent, HashSet<ProtobufMessage> alreadyHandledNodes)
        {
            if (!value.IsValid)
            {
                sb.Append("\"*Invalid*\"");
            }
            else if (value.Parsed.IsEmpty)
            {
                sb.Append("\"*RawData(");
                sb.Append(value.RawData.Count);
                sb.Append(")*\"");
            }
            else
            {
                var val = value.Parsed.Get();
                if (val == null)
                {
                    ;
                }
                if (val is ProtobufMessage)
                {
                    if (indent >= 0)
                    {
                        sb.AppendLine();
                    }
                    var message = (ProtobufMessage)val;
                    message.ToJson(sb, indent, alreadyHandledNodes);
                }
                else if (val is ProtobufUnknowValue)
                {
                    sb.Append("\"");
                    sb.Append(val.ToString());
                    sb.Append("\"");
                }
                else if (val is bool)
                {
                    if ((bool)val)
                    {
                        sb.Append("true");
                    }
                    else
                    {
                        sb.Append("false");
                    }
                }
                else if (_NumericTypes.Contains(val.GetType()))
                {
                    sb.Append(val.ToString());
                }
                else if (val is string)
                {
                    sb.Append("\"");
                    sb.Append((string)val);
                    sb.Append("\"");
                }
                else
                {
                    var str = val.ToString();
                    var trim = str.Trim();
                    if (trim.StartsWith("{") && trim.EndsWith("}") || trim.StartsWith("[") && trim.EndsWith("]"))
                    { // perhaps this is json object.
                        var lines = trim.Split(_LineEndings, StringSplitOptions.RemoveEmptyEntries);
                        if (lines.Length > 1)
                        {
                            for (int i = 0; i < lines.Length; ++i)
                            {
                                var line = lines[i].Trim();
                                if (indent >= 0)
                                {
                                    sb.AppendLine();
                                    sb.Append(' ', indent * 4);
                                    if (i != 0 && !(i == lines.Length - 1 && (line.StartsWith("}") || line.StartsWith("]"))))
                                    {
                                        sb.Append(' ', 4);
                                    }
                                }
                                sb.Append(line);
                            }
                        }
                        else
                        {
                            sb.Append(str);
                        }
                    }
                    else
                    {
                        sb.Append("\"");
                        sb.Append("*(");
                        sb.Append(val.GetType().FullName);
                        sb.Append(")*");
                        sb.Append(val.ToString());
                        sb.Append("\"");
                    }
                }
            }
        }

        protected class TooLongToReanderToJsonException : Exception { }
        public virtual void ToJson(System.Text.StringBuilder sb, int indent, HashSet<ProtobufMessage> alreadyHandledNodes)
        {
            if (alreadyHandledNodes == null && (indent > 100 || sb.Length > 1024 * 1024))
            {
                throw new TooLongToReanderToJsonException();
            }
            if (alreadyHandledNodes != null && !alreadyHandledNodes.Add(this))
            {
                return;
            }
            { // {
                if (indent >= 0)
                {
                    sb.Append(' ', indent * 4);
                }
                sb.Append('{');
            }
            for (int i = 0; i < 16; ++i)
            {
                var slot = _LowFields[i];
                WriteToJson(sb, slot, indent, alreadyHandledNodes);
            }
            int[] highnums = new int[_HighFields.Count];
            _HighFields.Keys.CopyTo(highnums, 0);
            Array.Sort(highnums);
            for (int i = 0; i < highnums.Length; ++i)
            {
                var num = highnums[i];
                var slot = _HighFields[num];
                WriteToJson(sb, slot, indent, alreadyHandledNodes);
            }
            { // eat last ','
                if (sb[sb.Length - 1] == ',')
                {
                    sb.Remove(sb.Length - 1, 1);
                }
            }
            { // }
                if (indent >= 0)
                {
                    sb.AppendLine();
                    sb.Append(' ', indent * 4);
                }
                sb.Append('}');
            }
            if (alreadyHandledNodes != null)
            {
                alreadyHandledNodes.Remove(this);
            }
        }
        protected string ToJson(int indent, HashSet<ProtobufMessage> alreadyHandledNodes)
        {
            var sb = new System.Text.StringBuilder();
            try
            {
                ToJson(sb, indent, alreadyHandledNodes);
            }
            catch (TooLongToReanderToJsonException)
            {
                PlatDependant.LogError("Too long to render to json!");
            }
            catch (StackOverflowException)
            {
                PlatDependant.LogError("Too long to render to json!");
            }
            return sb.ToString();
        }
        public virtual string ToJson(int indent)
        {
            return ToJson(indent, null);
        }
        public string ToJson()
        {
            return ToJson(-1);
        }
        public override string ToString()
        {
            return ToJson(0);
        }

        protected Dictionary<string, FieldSlot> _FieldMap = new Dictionary<string, FieldSlot>();
        protected internal virtual void FinishBuild()
        {
            _FieldMap.Clear();
            foreach (var slot in Slots)
            {
                for (int i = 0; i < slot.Values.Count; ++i)
                {
                    var val = slot.Values[i];
                    if (val.Parsed.IsEmpty)
                    {
                        val.Parsed = new ProtobufUnknowValue() { Raw = new ListSegment<byte>(val.RawData.ToArray()) };
                    }
                    val.RawData = default(ListSegment<byte>);
                    slot.Values[i] = val;
                    var sub = val.Parsed.Message;
                    if (sub != null)
                    {
                        sub.FinishBuild();
                    }
                }
                var name = slot.Desc.Name;
                if (name != null)
                {
                    _FieldMap[name] = slot;
                }
            }
        }

        public struct SlotValueAccessor : IList<ProtobufParsedValue>
        {
            internal FieldSlot _Slot;
            internal SlotValueAccessor(FieldSlot slot)
            {
                _Slot = slot;
            }

            public bool IsValid { get { return _Slot != null; } }

            public int Count { get { return _Slot.Values.Count; } }
            public bool IsReadOnly { get { return false; } }
            public ProtobufParsedValue this[int index]
            {
                get { return _Slot.Values[index].Parsed; }
                set
                {
                    var old = _Slot.Values[index];
                    old.Parsed = value;
                    _Slot.Values[index] = old;
                }
            }
            public int IndexOf(ProtobufParsedValue item)
            {
                for (int i = 0; i < Count; ++i)
                {
                    if (this[i] == item)
                    {
                        return i;
                    }
                }
                return -1;
            }
            public void Insert(int index, ProtobufParsedValue item)
            {
                var newVal = new ProtobufValue() { Parsed = item };
                if (_Slot.Desc.Type.KnownType == ProtobufNativeType.TYPE_ENUM)
                {
                    newVal.Parsed._ObjectVal = _Slot.Desc.Type.CLRType;
                }
                _Slot.Values.Insert(index, newVal);
            }
            public void RemoveAt(int index)
            {
                _Slot.Values.RemoveAt(index);
            }
            public void Add(ProtobufParsedValue item)
            {
                Insert(Count, item);
            }
            public void Clear()
            {
                _Slot.Values.Clear();
            }
            public bool Contains(ProtobufParsedValue item)
            {
                return IndexOf(item) >= 0;
            }
            public void CopyTo(ProtobufParsedValue[] array, int arrayIndex)
            {
                for (int i = 0; i < Count && i + arrayIndex < array.Length; ++i)
                {
                    array[i + arrayIndex] = _Slot.Values[i].Parsed;
                }
            }
            public bool Remove(ProtobufParsedValue item)
            {
                bool found = false;
                for (int i = Count - 1; i >= 0; --i)
                {
                    if (this[i] == item)
                    {
                        RemoveAt(i);
                        found = true;
                    }
                }
                return found;
            }
            public IEnumerator<ProtobufParsedValue> GetEnumerator()
            {
                for (int i = 0; i < Count; ++i)
                {
                    yield return _Slot.Values[i].Parsed;
                }
            }
            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
            public ProtobufParsedValue[] ToArray()
            {
                ProtobufParsedValue[] arr = new ProtobufParsedValue[Count];
                CopyTo(arr, 0);
                return arr;
            }

            public T As<T>()
            {
                return Get<T>();
            }
            public T AsEnum<T>() where T : struct
            {
                return GetEnum<T>();
            }
            public SlotValueAccessor<T> AsList<T>()
            {
                return new SlotValueAccessor<T>(_Slot);
            }
            public SlotEnumAccessor<T> AsEnums<T>() where T : struct
            {
                return new SlotEnumAccessor<T>(_Slot);
            }

            public T Get<T>()
            {
                return _Slot.FirstValue.Parsed.Get<T>();
            }
            public void Set<T>(T val)
            {
                var old = _Slot.FirstValue; old.Parsed.Set<T>(val); _Slot.FirstValue = old;
            }
            public T GetEnum<T>() where T : struct
            {
                return _Slot.FirstValue.Parsed.GetEnum<T>();
            }
            public void SetEnum<T>(T val) where T : struct
            {
                var old = _Slot.FirstValue; old.Parsed.SetEnum<T>(val); _Slot.FirstValue = old;
            }

            public bool Boolean
            {
                get { return _Slot.FirstValue.Parsed.Boolean; }
                set { var old = _Slot.FirstValue; old.Parsed.Boolean = value; _Slot.FirstValue = old; }
            }
            public byte Byte
            {
                get { return _Slot.FirstValue.Parsed.Byte; }
                set { var old = _Slot.FirstValue; old.Parsed.Byte = value; _Slot.FirstValue = old; }
            }
            public sbyte SByte
            {
                get { return _Slot.FirstValue.Parsed.SByte; }
                set { var old = _Slot.FirstValue; old.Parsed.SByte = value; _Slot.FirstValue = old; }
            }
            public short Int16
            {
                get { return _Slot.FirstValue.Parsed.Int16; }
                set { var old = _Slot.FirstValue; old.Parsed.Int16 = value; _Slot.FirstValue = old; }
            }
            public ushort UInt16
            {
                get { return _Slot.FirstValue.Parsed.UInt16; }
                set { var old = _Slot.FirstValue; old.Parsed.UInt16 = value; _Slot.FirstValue = old; }
            }
            public int Int32
            {
                get { return _Slot.FirstValue.Parsed.Int32; }
                set { var old = _Slot.FirstValue; old.Parsed.Int32 = value; _Slot.FirstValue = old; }
            }
            public uint UInt32
            {
                get { return _Slot.FirstValue.Parsed.UInt32; }
                set { var old = _Slot.FirstValue; old.Parsed.UInt32 = value; _Slot.FirstValue = old; }
            }
            public long Int64
            {
                get { return _Slot.FirstValue.Parsed.Int64; }
                set { var old = _Slot.FirstValue; old.Parsed.Int64 = value; _Slot.FirstValue = old; }
            }
            public ulong UInt64
            {
                get { return _Slot.FirstValue.Parsed.UInt64; }
                set { var old = _Slot.FirstValue; old.Parsed.UInt64 = value; _Slot.FirstValue = old; }
            }
            public IntPtr IntPtr
            {
                get { return _Slot.FirstValue.Parsed.IntPtr; }
                set { var old = _Slot.FirstValue; old.Parsed.IntPtr = value; _Slot.FirstValue = old; }
            }
            public UIntPtr UIntPtr
            {
                get { return _Slot.FirstValue.Parsed.UIntPtr; }
                set { var old = _Slot.FirstValue; old.Parsed.UIntPtr = value; _Slot.FirstValue = old; }
            }
            public float Single
            {
                get { return _Slot.FirstValue.Parsed.Single; }
                set { var old = _Slot.FirstValue; old.Parsed.Single = value; _Slot.FirstValue = old; }
            }
            public double Double
            {
                get { return _Slot.FirstValue.Parsed.Double; }
                set { var old = _Slot.FirstValue; old.Parsed.Double = value; _Slot.FirstValue = old; }
            }
            public object Object
            {
                get { return _Slot.FirstValue.Parsed.Object; }
                set { var old = _Slot.FirstValue; old.Parsed.Object = value; _Slot.FirstValue = old; }
            }
            public string String
            {
                get { return _Slot.FirstValue.Parsed.String; }
                set { var old = _Slot.FirstValue; old.Parsed.String = value; _Slot.FirstValue = old; }
            }
            public byte[] Bytes
            {
                get { return _Slot.FirstValue.Parsed.Bytes; }
                set { var old = _Slot.FirstValue; old.Parsed.Bytes = value; _Slot.FirstValue = old; }
            }
            public ProtobufUnknowValue Unknown
            {
                get { return _Slot.FirstValue.Parsed.Unknown; }
                set { var old = _Slot.FirstValue; old.Parsed.Unknown = value; _Slot.FirstValue = old; }
            }
            public ProtobufMessage Message
            {
                get { return _Slot.FirstValue.Parsed.Message; }
                set { var old = _Slot.FirstValue; old.Parsed.Message = value; _Slot.FirstValue = old; }
            }

            public SlotValueAccessor<bool> Booleans
            {
                get { return new SlotValueAccessor<bool>(_Slot); }
            }
            public SlotValueAccessor<byte> RepeatedByte
            {
                get { return new SlotValueAccessor<byte>(_Slot); }
            }
            public SlotValueAccessor<sbyte> SBytes
            {
                get { return new SlotValueAccessor<sbyte>(_Slot); }
            }
            public SlotValueAccessor<short> Int16s
            {
                get { return new SlotValueAccessor<short>(_Slot); }
            }
            public SlotValueAccessor<ushort> UInt16s
            {
                get { return new SlotValueAccessor<ushort>(_Slot); }
            }
            public SlotValueAccessor<int> Int32s
            {
                get { return new SlotValueAccessor<int>(_Slot); }
            }
            public SlotValueAccessor<uint> UInt32s
            {
                get { return new SlotValueAccessor<uint>(_Slot); }
            }
            public SlotValueAccessor<long> Int64s
            {
                get { return new SlotValueAccessor<long>(_Slot); }
            }
            public SlotValueAccessor<ulong> UInt64s
            {
                get { return new SlotValueAccessor<ulong>(_Slot); }
            }
            public SlotValueAccessor<IntPtr> IntPtrs
            {
                get { return new SlotValueAccessor<IntPtr>(_Slot); }
            }
            public SlotValueAccessor<UIntPtr> UIntPtrs
            {
                get { return new SlotValueAccessor<UIntPtr>(_Slot); }
            }
            public SlotValueAccessor<float> Singles
            {
                get { return new SlotValueAccessor<float>(_Slot); }
            }
            public SlotValueAccessor<double> Doubles
            {
                get { return new SlotValueAccessor<double>(_Slot); }
            }
            public SlotValueAccessor<object> Objects
            {
                get { return new SlotValueAccessor<object>(_Slot); }
            }
            public SlotValueAccessor<string> Strings
            {
                get { return new SlotValueAccessor<string>(_Slot); }
            }
            public SlotValueAccessor<byte[]> RepeatedBytes
            {
                get { return new SlotValueAccessor<byte[]>(_Slot); }
            }
            public SlotValueAccessor<ProtobufUnknowValue> Unknowns
            {
                get { return new SlotValueAccessor<ProtobufUnknowValue>(_Slot); }
            }
            public SlotValueAccessor<ProtobufMessage> Messages
            {
                get { return new SlotValueAccessor<ProtobufMessage>(_Slot); }
            }
            // TODO: implicit operators
        }
        public struct SlotValueAccessor<T> : IList<T>
        {
            internal FieldSlot _Slot;
            internal SlotValueAccessor(FieldSlot slot)
            {
                _Slot = slot;
            }

            public T this[int index]
            {
                get { return _Slot.Values[index].Parsed.Get<T>(); }
                set
                {
                    var old = _Slot.Values[index];
                    old.Parsed.Set<T>(value);
                    _Slot.Values[index] = old;
                }
            }
            public int Count { get { return _Slot.Values.Count; } }
            public bool IsReadOnly { get { return false; } }
            public void Insert(int index, T item)
            {
                var newVal = new ProtobufValue();
                newVal.Parsed.Set<T>(item);
                //if (_Slot.Desc.Type.KnownType == ProtobufNativeType.TYPE_ENUM)
                //{
                //    newVal.Parsed._ObjectVal = _Slot.Desc.Type.CLRType; // maybe we donot need this?
                //}
                _Slot.Values.Insert(index, newVal);
            }
            public int IndexOf(T item)
            {
                for (int i = 0; i < Count; ++i)
                {
                    if (Equals(this[i], item))
                    {
                        return i;
                    }
                }
                return -1;
            }
            public void Add(T item)
            {
                Insert(Count, item);
            }
            public void Clear()
            {
                _Slot.Values.Clear();
            }
            public bool Contains(T item)
            {
                return IndexOf(item) >= 0;
            }
            public void CopyTo(T[] array, int arrayIndex)
            {
                for (int i = 0; i < Count && i + arrayIndex < array.Length; ++i)
                {
                    array[i + arrayIndex] = _Slot.Values[i].Parsed.Get<T>();
                }
            }
            public T[] ToArray()
            {
                T[] arr = new T[Count];
                CopyTo(arr, 0);
                return arr;
            }
            public IEnumerator<T> GetEnumerator()
            {
                for (int i = 0; i < Count; ++i)
                {
                    yield return _Slot.Values[i].Parsed.Get<T>();
                }
            }
            public bool Remove(T item)
            {
                bool found = false;
                for (int i = Count - 1; i >= 0; --i)
                {
                    if (Equals(this[i], item))
                    {
                        RemoveAt(i);
                        found = true;
                    }
                }
                return found;
            }
            public void RemoveAt(int index)
            {
                _Slot.Values.RemoveAt(index);
            }
            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
        public struct SlotEnumAccessor<T> : IList<T> where T : struct
        {
            internal FieldSlot _Slot;
            internal SlotEnumAccessor(FieldSlot slot)
            {
                _Slot = slot;
            }

            public T this[int index]
            {
                get { return _Slot.Values[index].Parsed.GetEnum<T>(); }
                set
                {
                    var old = _Slot.Values[index];
                    old.Parsed.SetEnum<T>(value);
                    _Slot.Values[index] = old;
                }
            }
            public int Count { get { return _Slot.Values.Count; } }
            public bool IsReadOnly { get { return false; } }
            public void Insert(int index, T item)
            {
                var newVal = new ProtobufValue();
                newVal.Parsed.SetEnum<T>(item);
                //if (_Slot.Desc.Type.KnownType == ProtobufNativeType.TYPE_ENUM)
                //{
                //    newVal.Parsed._ObjectVal = _Slot.Desc.Type.CLRType; // maybe we donot need this?
                //}
                _Slot.Values.Insert(index, newVal);
            }
            public int IndexOf(T item)
            {
                for (int i = 0; i < Count; ++i)
                {
                    if (Equals(this[i], item))
                    {
                        return i;
                    }
                }
                return -1;
            }
            public void Add(T item)
            {
                Insert(Count, item);
            }
            public void Clear()
            {
                _Slot.Values.Clear();
            }
            public bool Contains(T item)
            {
                return IndexOf(item) >= 0;
            }
            public void CopyTo(T[] array, int arrayIndex)
            {
                for (int i = 0; i < Count && i + arrayIndex < array.Length; ++i)
                {
                    array[i + arrayIndex] = _Slot.Values[i].Parsed.GetEnum<T>();
                }
            }
            public T[] ToArray()
            {
                T[] arr = new T[Count];
                CopyTo(arr, 0);
                return arr;
            }
            public IEnumerator<T> GetEnumerator()
            {
                for (int i = 0; i < Count; ++i)
                {
                    yield return _Slot.Values[i].Parsed.GetEnum<T>();
                }
            }
            public bool Remove(T item)
            {
                bool found = false;
                for (int i = Count - 1; i >= 0; --i)
                {
                    if (Equals(this[i], item))
                    {
                        RemoveAt(i);
                        found = true;
                    }
                }
                return found;
            }
            public void RemoveAt(int index)
            {
                _Slot.Values.RemoveAt(index);
            }
            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        public SlotValueAccessor this[string name]
        {
            get
            {
                FieldSlot slot = null;
                _FieldMap.TryGetValue(name, out slot);
                return new SlotValueAccessor(slot);
            }
        }

        public ProtobufMessage ApplyTemplate(ProtobufMessage template)
        {
            return ProtobufEncoder.ApplyTemplate(this, template);
        }
    }

    public class TemplateProtobufMessage : ProtobufMessage, System.Collections.IEnumerable
    {
        public void Add(int fieldno, string fieldname, ProtobufNativeType knownType)
        {
            var slot = GetOrCreateSlot(fieldno);
            slot.Desc.Name = fieldname;
            slot.Desc.Type.KnownType = knownType;
        }
        public void Add(int fieldno, string fieldname, ProtobufMessage subtemplate)
        {
            var slot = GetOrCreateSlot(fieldno);
            slot.Desc.Name = fieldname;
            slot.Desc.Type.KnownType = ProtobufNativeType.TYPE_MESSAGE;
            slot.FirstValue = new ProtobufValue() { Parsed = subtemplate };
        }
        public void Add(int fieldno, string fieldname, TemplateProtobufMessage subtemplate)
        {
            var slot = GetOrCreateSlot(fieldno);
            slot.Desc.Name = fieldname;
            slot.Desc.Type.KnownType = ProtobufNativeType.TYPE_MESSAGE;
            slot.Desc.Type.MessageName = subtemplate.Name;
            slot.FirstValue = new ProtobufValue() { Parsed = subtemplate };
        }
        public void Add<T>(int fieldno, string fieldname, T templateValue) // currently only used for enums
        {
            var slot = GetOrCreateSlot(fieldno);
            if (typeof(T).IsEnum)
            {
                slot.Desc.Type.KnownType = ProtobufNativeType.TYPE_ENUM;
                slot.Desc.Type.CLRType = typeof(T);
            }
            slot.Desc.Name = fieldname;
            var val = new ProtobufValue();
            val.Parsed.Set(templateValue);
            slot.FirstValue = val;
        }
        internal void Add<T>(int fieldno, string fieldname, ProtobufNativeType knownType, T templateValue) // currently only used for enums
        {
            var slot = GetOrCreateSlot(fieldno);
            slot.Desc.Name = fieldname;
            slot.Desc.Type.KnownType = knownType;
            if (knownType == ProtobufNativeType.TYPE_ENUM && typeof(T).IsEnum)
            {
                slot.Desc.Type.CLRType = typeof(T);
            }
            var val = new ProtobufValue(knownType);
            val.Parsed.Set(templateValue);
            slot.FirstValue = val;
        }

        protected bool _BuildFinished;
        protected internal override void FinishBuild()
        {
            if (_BuildFinished)
            {
                return;
            }
            _BuildFinished = true;
            base.FinishBuild();
        }

        public string Name { get; protected internal set; }

        public TemplateProtobufMessage() { }
        public TemplateProtobufMessage(string name)
        {
            Name = name;
        }

        public override string ToJson(int indent)
        {
            return ToJson(indent, new HashSet<ProtobufMessage>());
        }

        public static Dictionary<ProtobufNativeType, string> _NativeType2FriendlyType = new Dictionary<ProtobufNativeType, string>()
        {
            { ProtobufNativeType.TYPE_BOOL, "bool" },
            { ProtobufNativeType.TYPE_BYTES, "bytes" },
            { ProtobufNativeType.TYPE_DOUBLE, "double" },
            { ProtobufNativeType.TYPE_ENUM, "enum" },
            { ProtobufNativeType.TYPE_FIXED32, "fixed32" },
            { ProtobufNativeType.TYPE_FIXED64, "fixed64" },
            { ProtobufNativeType.TYPE_FLOAT, "float" },
            { ProtobufNativeType.TYPE_INT32, "int32" },
            { ProtobufNativeType.TYPE_INT64, "int64" },
            { ProtobufNativeType.TYPE_SFIXED32, "sfixed32" },
            { ProtobufNativeType.TYPE_SFIXED64, "sfixed64" },
            { ProtobufNativeType.TYPE_SINT32, "sint32" },
            { ProtobufNativeType.TYPE_SINT64, "sint64" },
            { ProtobufNativeType.TYPE_STRING, "string" },
            { ProtobufNativeType.TYPE_UINT32, "uint32" },
            { ProtobufNativeType.TYPE_UINT64, "uint64" },
        };
        protected static new void WriteToJson(System.Text.StringBuilder sb, FieldSlot slot, int indent, HashSet<ProtobufMessage> alreadyHandledNodes)
        {
            if (!string.IsNullOrEmpty(slot.Desc.Name) || slot.Desc.Type.KnownType != 0)
            {
                sb.Append(",");
                { // key
                    if (indent >= 0)
                    {
                        sb.AppendLine();
                        sb.Append(' ', indent * 4 + 4);
                    }
                    sb.Append('"');
                    if (slot.Desc.Name != null)
                    {
                        sb.Append(slot.Desc.Name);
                    }
                    else
                    {
                        sb.Append(slot.Desc.Number);
                    }
                    sb.Append('"');
                    if (indent >= 0)
                    {
                        sb.Append(" ");
                    }
                    sb.Append(":");
                    if (indent >= 0)
                    {
                        sb.Append(" ");
                    }
                }
                { // value
                    if (slot.FirstValue.Parsed.Message != null)
                    { // ref to another message.
                        if (indent >= 0)
                        {
                            sb.AppendLine();
                            sb.Append(' ', indent * 4 + 4);
                        }
                        if (slot.Desc.Label == ProtobufFieldLabel.LABEL_REPEATED)
                        {
                            sb.Append("[");
                            if (indent >= 0)
                            {
                                sb.Append(" ");
                            }
                        }
                        var pos = sb.Length;

                        var message = slot.FirstValue.Parsed.Message;
                        message.ToJson(sb, indent < 0 ? indent : indent + 1, alreadyHandledNodes);
                        if (indent >= 0)
                        {
                            sb.Remove(pos, indent * 4 + 4);
                        }
                        if (slot.Desc.Label == ProtobufFieldLabel.LABEL_REPEATED)
                        {
                            if (indent >= 0)
                            {
                                sb.Append(" ");
                            }
                            sb.Append("]");
                        }
                    }
                    else
                    {
                        if (slot.Desc.Label == ProtobufFieldLabel.LABEL_REPEATED)
                        {
                            sb.Append("[");
                            if (indent >= 0)
                            {
                                sb.Append(" ");
                            }
                        }
                        if (slot.Desc.Type.KnownType == ProtobufNativeType.TYPE_ENUM)
                        {

                            sb.Append("\"enum");
                            if (slot.Desc.Type.CLRType != null)
                            {
                                sb.Append(" ");
                                sb.Append(slot.Desc.Type.CLRType.FullName);
                            }
                            else if (!string.IsNullOrEmpty(slot.Desc.Type.MessageName))
                            {
                                sb.Append(" ");
                                sb.Append(slot.Desc.Type.MessageName);
                            }
                            sb.Append("\"");
                        }
                        else
                        {
                            string friendly;
                            if (!_NativeType2FriendlyType.TryGetValue(slot.Desc.Type.KnownType, out friendly))
                            {
                                friendly = "?";
                            }
                            sb.Append("\"");
                            sb.Append(friendly);
                            sb.Append("\"");
                        }
                        if (slot.Desc.Label == ProtobufFieldLabel.LABEL_REPEATED)
                        {
                            if (indent >= 0)
                            {
                                sb.Append(" ");
                            }
                            sb.Append("]");
                        }
                    }
                }
            }
        }
        public override void ToJson(System.Text.StringBuilder sb, int indent, HashSet<ProtobufMessage> alreadyHandledNodes)
        {
            if (alreadyHandledNodes != null && !alreadyHandledNodes.Add(this))
            {
                if (indent >= 0)
                {
                    sb.Append(' ', indent * 4);
                }
                sb.Append("\"*Ref*");
                sb.Append(Name);
                sb.Append("\"");
                return;
            }
            if (alreadyHandledNodes == null && (indent > 100 || sb.Length > 1024 * 1024))
            {
                throw new TooLongToReanderToJsonException();
            }

            { // {
                if (indent >= 0)
                {
                    sb.Append(' ', indent * 4);
                }
                sb.Append('{');
            }
            if (indent >= 0)
            {
                sb.AppendLine();
                sb.Append(' ', indent * 4 + 4);
            }
            sb.Append("\"@name\"");
            if (indent >= 0)
            {
                sb.Append(" ");
            }
            sb.Append(":");
            if (indent >= 0)
            {
                sb.Append(" ");
            }
            sb.Append("\"");
            sb.Append(Name);
            sb.Append("\"");

            for (int i = 0; i < 16; ++i)
            {
                var slot = _LowFields[i];
                WriteToJson(sb, slot, indent, alreadyHandledNodes);
            }
            int[] highnums = new int[_HighFields.Count];
            _HighFields.Keys.CopyTo(highnums, 0);
            Array.Sort(highnums);
            for (int i = 0; i < highnums.Length; ++i)
            {
                var num = highnums[i];
                var slot = _HighFields[num];
                WriteToJson(sb, slot, indent, alreadyHandledNodes);
            }
            { // }
                if (indent >= 0)
                {
                    sb.AppendLine();
                    sb.Append(' ', indent * 4);
                }
                sb.Append('}');
            }
            if (alreadyHandledNodes != null)
            {
                alreadyHandledNodes.Remove(this);
            }
        }

        protected void CollectAll(List<TemplateProtobufMessage> list, HashSet<string> names)
        {
            if (names.Add(Name))
            {
                list.Add(this);
                for (int i = 0; i < 16; ++i)
                {
                    var slot = _LowFields[i];
                    if (slot.FirstValue.Parsed.Message is TemplateProtobufMessage)
                    {
                        ((TemplateProtobufMessage)slot.FirstValue.Parsed.Message).CollectAll(list, names);
                    }
                }
                int[] highnums = new int[_HighFields.Count];
                _HighFields.Keys.CopyTo(highnums, 0);
                Array.Sort(highnums);
                for (int i = 0; i < highnums.Length; ++i)
                {
                    var num = highnums[i];
                    var slot = _HighFields[num];
                    if (slot.FirstValue.Parsed.Message is TemplateProtobufMessage)
                    {
                        ((TemplateProtobufMessage)slot.FirstValue.Parsed.Message).CollectAll(list, names);
                    }
                }
            }
        }
        public List<TemplateProtobufMessage> CollectAll()
        {
            List<TemplateProtobufMessage> list = new List<TemplateProtobufMessage>();
            HashSet<string> names = new HashSet<string>();
            CollectAll(list, names);
            return list;
        }
        protected void ToReadable(System.Text.StringBuilder sb, int indent)
        {
            sb.Append(' ', indent * 4);
            sb.Append("message ");
            sb.Append(Name);
            sb.AppendLine();
            sb.Append(' ', indent * 4);
            sb.Append("{");
            List<FieldSlot> slots = new List<FieldSlot>();
            for (int i = 0; i < 16; ++i)
            {
                var slot = _LowFields[i];
                slots.Add(slot);
            }
            int[] highnums = new int[_HighFields.Count];
            _HighFields.Keys.CopyTo(highnums, 0);
            Array.Sort(highnums);
            for (int i = 0; i < highnums.Length; ++i)
            {
                var num = highnums[i];
                var slot = _HighFields[num];
                slots.Add(slot);
            }
            for (int i = 0; i < slots.Count; ++i)
            {
                var slot = slots[i];
                if (!string.IsNullOrEmpty(slot.Desc.Name) || slot.Desc.Type.KnownType != 0)
                {
                    sb.AppendLine();
                    sb.Append(' ', indent * 4 + 4);
                    if (slot.Desc.Label == ProtobufFieldLabel.LABEL_REPEATED)
                    {
                        sb.Append("repeated ");
                    }
                    if (slot.Desc.Type.KnownType == ProtobufNativeType.TYPE_MESSAGE || slot.FirstValue.Parsed.Message is TemplateProtobufMessage)
                    {
                        if (!string.IsNullOrEmpty(slot.Desc.Type.MessageName))
                        {
                            sb.Append(slot.Desc.Type.MessageName);
                        }
                        else if (slot.FirstValue.Parsed.Message is TemplateProtobufMessage && !string.IsNullOrEmpty(((TemplateProtobufMessage)slot.FirstValue.Parsed.Message).Name))
                        {
                            sb.Append(((TemplateProtobufMessage)slot.FirstValue.Parsed.Message).Name);
                        }
                        else
                        {
                            sb.Append("message");
                        }
                    }
                    else if (slot.Desc.Type.KnownType == ProtobufNativeType.TYPE_ENUM)
                    {
                        if (!string.IsNullOrEmpty(slot.Desc.Type.MessageName))
                        {
                            sb.Append(slot.Desc.Type.MessageName);
                        }
                        else if (slot.Desc.Type.CLRType != null)
                        {
                            sb.Append(slot.Desc.Type.CLRType.FullName);
                        }
                        else
                        {
                            sb.Append("enum");
                        }
                    }
                    else
                    {
                        string friendly;
                        if (!_NativeType2FriendlyType.TryGetValue(slot.Desc.Type.KnownType, out friendly))
                        {
                            friendly = "unknown";
                        }
                        sb.Append(friendly);
                    }

                    sb.Append(" ");
                    sb.Append(string.IsNullOrEmpty(slot.Desc.Name) ? "unknown" : slot.Desc.Name);
                    sb.Append(" = ");
                    sb.Append(slot.Desc.Number);
                    sb.Append(";");
                }
            }
            sb.AppendLine();
            sb.Append(' ', indent * 4);
            sb.Append("}");
        }
        public string ToReadable(int indent, bool withreference)
        {
            if (indent < 0) indent = 0;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            if (withreference)
            {
                var list = CollectAll();
                for (int i = 0; i < list.Count; ++i)
                {
                    if (i > 0)
                    {
                        sb.AppendLine();
                    }
                    list[i].ToReadable(sb, indent);
                }
            }
            else
            {
                ToReadable(sb, indent);
            }
            return sb.ToString();
        }
        public override string ToString()
        {
            return ToReadable(0, false);
        }

        public IEnumerator GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }

    public static class ProtobufEncoder
    {
        public static bool ReadFixed32(ListSegment<byte> data, out uint value, out int readbytecount)
        {
            value = 0;
            for (int i = 0; i < 4 && i < data.Count; ++i)
            {
                var part = (uint)data[i];
                value += part << (8 * i);
            }
            readbytecount = Math.Min(4, data.Count);
            return readbytecount == 4;
        }
        public static bool ReadFixed64(ListSegment<byte> data, out ulong value, out int readbytecount)
        {
            value = 0;
            for (int i = 0; i < 8 && i < data.Count; ++i)
            {
                var part = (ulong)data[i];
                value += part << (8 * i);
            }
            readbytecount = Math.Min(8, data.Count);
            return readbytecount == 8;
        }
        public static bool ReadVariant(ListSegment<byte> data, out ulong value, out int readbytecount)
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
        public static bool TryReadFixed32(System.IO.Stream stream, out uint value)
        {
            value = 0;
            for (int i = 0; i < 4; ++i)
            {
                var b = stream.ReadByte();
                if (b < 0)
                {
                    return false;
                }
                var part = (uint)b;
                value += part << (8 * i);
            }
            return true;
        }
        public static uint ReadFixed32(System.IO.Stream stream)
        {
            uint value = 0;
            if (!TryReadFixed32(stream, out value))
            {
                throw new System.IO.EndOfStreamException();
            }
            return value;
        }
        public static bool TryReadFixed64(System.IO.Stream stream, out ulong value)
        {
            value = 0;
            for (int i = 0; i < 8; ++i)
            {
                var b = stream.ReadByte();
                if (b < 0)
                {
                    return false;
                }
                var part = (ulong)b;
                value += part << (8 * i);
            }
            return true;
        }
        public static ulong ReadFixed64(System.IO.Stream stream)
        {
            ulong value = 0;
            if (!TryReadFixed64(stream, out value))
            {
                throw new System.IO.EndOfStreamException();
            }
            return value;
        }
        public static bool TryReadVariant(System.IO.Stream stream, out ulong value)
        {
            value = 0;
            for (int i = 0; ; ++i)
            {
                var b = stream.ReadByte();
                if (b < 0)
                {
                    return false;
                }
                ulong part = (ulong)b;
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
        }
        public static ulong ReadVariant(System.IO.Stream stream)
        {
            ulong value = 0;
            if (!TryReadVariant(stream, out value))
            {
                throw new System.IO.EndOfStreamException();
            }
            return value;
        }

        public static void DecodeTag(ulong tag, out int number, out ProtobufLowLevelType ltype)
        {
            number = (int)(tag >> 3);
            ltype = (ProtobufLowLevelType)(tag & 0x7);
        }
#if !UNITY_ENGINE && !UNITY_5_3_OR_NEWER || NET_4_6 || NET_STANDARD_2_0
        public static (int number, ProtobufLowLevelType ltype) DecodeTag(ulong tag)
        {
            int number;
            ProtobufLowLevelType ltype;
            DecodeTag(tag, out number, out ltype);
            return (number, ltype);
        }
#endif
        public static bool ReadTag(ListSegment<byte> data, out int number, out ProtobufLowLevelType ltype, out int readbytecount)
        {
            ulong tag;
            if (ReadVariant(data, out tag, out readbytecount))
            {
                DecodeTag(tag, out number, out ltype);
                return true;
            }
            number = 0;
            ltype = 0;
            return false;
        }

        public static bool ReadRaw(ListSegment<byte> data, out int number, out ProtobufLowLevelType ltype, out ProtobufValue value, out int readbytecount)
        {
            value = new ProtobufValue();
            if (!ReadTag(data, out number, out ltype, out readbytecount))
            {
                return false;
            }
            ListSegment<byte> rest = data.ConsumeHead(readbytecount);
            int restreadcnt = 0;
            if (ltype == ProtobufLowLevelType.Varint)
            {
                ulong vvalue;
                bool success = ReadVariant(rest, out vvalue, out restreadcnt);
                readbytecount += restreadcnt;
                if (!success)
                {
                    return false;
                }
                value.RawData = new ListSegment<byte>(rest.List, rest.Offset, restreadcnt);
                value.Parsed = vvalue;
                return true;
            }
            else if (ltype == ProtobufLowLevelType.Fixed32)
            {
                uint ivalue;
                bool success = ReadFixed32(rest, out ivalue, out restreadcnt);
                readbytecount += restreadcnt;
                if (!success)
                {
                    return false;
                }
                value.RawData = new ListSegment<byte>(rest.List, rest.Offset, restreadcnt);
                value.Parsed = ivalue;
                return true;
            }
            else if (ltype == ProtobufLowLevelType.Fixed64)
            {
                ulong ivalue;
                bool success = ReadFixed64(rest, out ivalue, out restreadcnt);
                readbytecount += restreadcnt;
                if (!success)
                {
                    return false;
                }
                value.RawData = new ListSegment<byte>(rest.List, rest.Offset, restreadcnt);
                value.Parsed = ivalue;
                return true;
            }
            else if (ltype == ProtobufLowLevelType.LengthDelimited)
            {
                ulong length;
                bool success = ReadVariant(rest, out length, out restreadcnt);
                readbytecount += restreadcnt;
                if (!success)
                {
                    return false;
                }
                rest = rest.ConsumeHead(restreadcnt);
                if (length > (ulong)rest.Count)
                {
                    // Too long.
                    readbytecount += rest.Count;
                    // value.RawData = rest; // we'd better not assign it.
                    return false;
                }
                readbytecount += (int)length;
                value.RawData = new ListSegment<byte>(rest.List, rest.Offset, (int)length);
                return true;
            }
            else
            {
                // unkwon type
                return false;
            }
        }

        private static HashSet<ProtobufLowLevelType> _ValidLowLevelTypes = new HashSet<ProtobufLowLevelType>()
        {
            ProtobufLowLevelType.Fixed32,
            ProtobufLowLevelType.Fixed64,
            ProtobufLowLevelType.LengthDelimited,
            ProtobufLowLevelType.Varint,
        };
        public static bool ReadRaw(ListSegment<byte> data, out ProtobufMessage message, out int readbytecount)
        {
            readbytecount = 0;
            if (data.Count <= 0)
            {
                message = null;
                return false;
            }
            int fieldno;
            ProtobufLowLevelType fieldtype;
            ProtobufValue fieldval;
            int readcnt;
            var rest = data;
            message = new ProtobufMessage();
            while (rest.Count > 0)
            {
                if (!ReadRaw(rest, out fieldno, out fieldtype, out fieldval, out readcnt))
                {
                    // readbytecount += readcnt; do not consume this.
                    return false;
                }
                else if (fieldno <= 0)
                {
                    // readbytecount += readcnt; do not consume this.
                    return false;
                }
                else if (!_ValidLowLevelTypes.Contains(fieldtype))
                {
                    // readbytecount += readcnt; do not consume this.
                    return false;
                }
                //else if (!fieldval.IsValid) // this should not happen
                //{
                //    // readbytecount += readcnt; do not consume this.
                //    return null;
                //}
                readbytecount += readcnt;
                if (fieldtype == ProtobufLowLevelType.LengthDelimited)
                {
                    // try parse sub messages.
                    ProtobufMessage sub;
                    int subreadcnt;
                    if (ReadRaw(fieldval.RawData, out sub, out subreadcnt))
                    {
                        fieldval.Parsed = sub;
                    }
                }
                var slot = message.Slots[fieldno];
                slot.Values.Add(fieldval);
                rest = rest.ConsumeHead(readcnt);
            }
            return true;
        }
        public static ProtobufMessage ReadRaw(ListSegment<byte> data, out int readbytecount)
        {
            ProtobufMessage message;
            ReadRaw(data, out message, out readbytecount);
            return message;
        }
        public static ProtobufMessage ReadRaw(ListSegment<byte> data)
        {
            int readcnt;
            return ReadRaw(data, out readcnt);
        }

        /// <summary>
        /// notice: in case of end of stream, this will not fail, but the real bytes skipped may be less than required.
        /// </summary>
        public static void SkipBytes(System.IO.Stream stream, int count)
        {
            if (stream.CanSeek)
            {
                stream.Seek(count, System.IO.SeekOrigin.Current);
            }
            else
            {
                var buffer = PlatDependant.CopyStreamBuffer;
                int readcnt = 0;
                while (readcnt < count)
                {
                    var pread = Math.Min(buffer.Length, count - readcnt);
                    pread = stream.Read(buffer, 0, pread);
                    if (pread == 0)
                    {
                        break;
                    }
                    readcnt += pread;
                }
            }
        }

        public static void CopyBytes(System.IO.Stream stream, System.IO.Stream tostream, int count)
        {
            var buffer = PlatDependant.CopyStreamBuffer;
            int readcnt = 0;
            while (readcnt < count)
            {
                var pread = Math.Min(buffer.Length, count - readcnt);
                pread = stream.Read(buffer, 0, pread);
                if (pread == 0)
                {
                    break;
                }
                tostream.Write(buffer, 0, pread);
                readcnt += pread;
            }
        }

        public static long DecodeZigZag64(ulong val)
        {
            var value = (long)(val >> 1) ^ -(long)(val & 1);
            return value;
        }
        public static int DecodeZigZag32(uint val)
        {
            var value = (int)(val >> 1) ^ -(int)(val & 1);
            return value;
        }

        private static HashSet<Type> _IntTypes = new HashSet<Type>()
        {
            typeof(byte),
            typeof(sbyte),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(IntPtr),
            typeof(UIntPtr),
        };
        private static HashSet<Type> _FloatTypes = new HashSet<Type>()
        {
            typeof(float),
            typeof(double),
            typeof(decimal),
        };
        private delegate bool DecodeFuncForNativeType(ProtobufValue raw, out ProtobufParsedValue value);
        private static Dictionary<ProtobufNativeType, DecodeFuncForNativeType> _DecodeForNativeTypeFuncs = new Dictionary<ProtobufNativeType, DecodeFuncForNativeType>()
        {
            { ProtobufNativeType.TYPE_BYTES, 
                (ProtobufValue raw, out ProtobufParsedValue value) =>
                {
                    var buffer = raw.RawData.ToArray();
                    value = buffer;
                    return true;
                }
            },
            { ProtobufNativeType.TYPE_STRING, 
                (ProtobufValue raw, out ProtobufParsedValue value) =>
                {
                    var buffer = raw.RawData.ToArray();
                    try
                    {
                        value = System.Text.Encoding.UTF8.GetString(buffer);
                        return true;
                    }
                    catch
                    {
                        value = default(ProtobufParsedValue);
                        return false;
                    }
                }
            },
            { ProtobufNativeType.TYPE_BOOL,
                (ProtobufValue raw, out ProtobufParsedValue value) =>
                {
                    if (!raw.Parsed.IsObject)
                    {
                        value = ((ulong)raw.Parsed) != 0;
                        return true;
                    }
                    value = default(ProtobufParsedValue);
                    return false;
                }
            },
            { ProtobufNativeType.TYPE_ENUM,
                (ProtobufValue raw, out ProtobufParsedValue value) =>
                {
                    if (!raw.Parsed.IsObject)
                    {
                        value = (ulong)raw.Parsed;
                        return true;
                    }
                    value = default(ProtobufParsedValue);
                    return false;
                }
            },
            { ProtobufNativeType.TYPE_DOUBLE,
                (ProtobufValue raw, out ProtobufParsedValue value) =>
                {
                    if (!raw.Parsed.IsObject)
                    {
                        value = (double)raw.Parsed;
                        return true;
                    }
                    value = default(ProtobufParsedValue);
                    return false;
                }
            },
            { ProtobufNativeType.TYPE_FLOAT,
                (ProtobufValue raw, out ProtobufParsedValue value) =>
                {
                    if (!raw.Parsed.IsObject)
                    {
                        value = (float)raw.Parsed;
                        return true;
                    }
                    value = default(ProtobufParsedValue);
                    return false;
                }
            },
            { ProtobufNativeType.TYPE_INT64,
                (ProtobufValue raw, out ProtobufParsedValue value) =>
                {
                    if (!raw.Parsed.IsObject)
                    {
                        value = (long)raw.Parsed;
                        return true;
                    }
                    value = default(ProtobufParsedValue);
                    return false;
                }
            },
            { ProtobufNativeType.TYPE_UINT64,
                (ProtobufValue raw, out ProtobufParsedValue value) =>
                {
                    if (!raw.Parsed.IsObject)
                    {
                        value = (ulong)raw.Parsed;
                        return true;
                    }
                    value = default(ProtobufParsedValue);
                    return false;
                }
            },
            { ProtobufNativeType.TYPE_SFIXED64,
                (ProtobufValue raw, out ProtobufParsedValue value) =>
                {
                    if (!raw.Parsed.IsObject)
                    {
                        value = (long)raw.Parsed;
                        return true;
                    }
                    value = default(ProtobufParsedValue);
                    return false;
                }
            },
            { ProtobufNativeType.TYPE_FIXED64,
                (ProtobufValue raw, out ProtobufParsedValue value) =>
                {
                    if (!raw.Parsed.IsObject)
                    {
                        value = (ulong)raw.Parsed;
                        return true;
                    }
                    value = default(ProtobufParsedValue);
                    return false;
                }
            },
            { ProtobufNativeType.TYPE_INT32,
                (ProtobufValue raw, out ProtobufParsedValue value) =>
                {
                    if (!raw.Parsed.IsObject)
                    {
                        value = (int)raw.Parsed;
                        return true;
                    }
                    value = default(ProtobufParsedValue);
                    return false;
                }
            },
            { ProtobufNativeType.TYPE_UINT32,
                (ProtobufValue raw, out ProtobufParsedValue value) =>
                {
                    if (!raw.Parsed.IsObject)
                    {
                        value = (uint)raw.Parsed;
                        return true;
                    }
                    value = default(ProtobufParsedValue);
                    return false;
                }
            },
            { ProtobufNativeType.TYPE_SFIXED32,
                (ProtobufValue raw, out ProtobufParsedValue value) =>
                {
                    if (!raw.Parsed.IsObject)
                    {
                        value = (int)raw.Parsed;
                        return true;
                    }
                    value = default(ProtobufParsedValue);
                    return false;
                }
            },
            { ProtobufNativeType.TYPE_FIXED32,
                (ProtobufValue raw, out ProtobufParsedValue value) =>
                {
                    if (!raw.Parsed.IsObject)
                    {
                        value = (uint)raw.Parsed;
                        return true;
                    }
                    value = default(ProtobufParsedValue);
                    return false;
                }
            },
            { ProtobufNativeType.TYPE_SINT64,
                (ProtobufValue raw, out ProtobufParsedValue value) =>
                {
                    if (!raw.Parsed.IsObject)
                    {
                        ulong r = (ulong)raw.Parsed;
                        value = DecodeZigZag64(r);
                        return true;
                    }
                    value = default(ProtobufParsedValue);
                    return false;
                }
            },
            { ProtobufNativeType.TYPE_SINT32,
                (ProtobufValue raw, out ProtobufParsedValue value) =>
                {
                    if (!raw.Parsed.IsObject)
                    {
                        uint r = (uint)raw.Parsed;
                        value = DecodeZigZag32(r);
                        return true;
                    }
                    value = default(ProtobufParsedValue);
                    return false;
                }
            },
        };
        public static bool Decode(ProtobufValue raw, ProtobufNativeType knownType, out ProtobufParsedValue value)
        {
            value = default(ProtobufParsedValue);
            if (raw.RawData.List == null)
            {
                return false;
            }
            DecodeFuncForNativeType decodeFunc;
            if (_DecodeForNativeTypeFuncs.TryGetValue(knownType, out decodeFunc))
            {
                if (decodeFunc(raw, out value))
                {
                    value._Type = knownType;
                    return true;
                }
            }
            return false;
        }
        private static void ApplyTemplate(ProtobufMessage.FieldSlot rslot, ProtobufMessage.FieldSlot tslot)
        {
            if (rslot != null && tslot != null)
            {
                rslot.Desc = tslot.Desc;
                if (rslot.Values.Count > 0)
                {
                    var knownType = tslot.Desc.Type.KnownType;
                    if (knownType != 0 && knownType != ProtobufNativeType.TYPE_GROUP && knownType != ProtobufNativeType.TYPE_MESSAGE)
                    {
                        for (int j = 0; j < rslot.Values.Count; ++j)
                        {
                            var subraw = rslot.Values[j];
                            ProtobufParsedValue newval;
                            if (Decode(subraw, knownType, out newval))
                            {
                                if (knownType == ProtobufNativeType.TYPE_ENUM)
                                {
                                    Type etype = tslot.FirstValue.Parsed._ObjectVal as Type;
                                    if (etype == null)
                                    {
                                        var eval = tslot.FirstValue.Parsed.Get();
                                        if (eval is Enum)
                                        {
                                            etype = eval.GetType();
                                        }
                                    }
                                    newval._ObjectVal = etype;
                                }
                                subraw.Parsed = newval;
                                rslot.Values[j] = subraw;
                            }
                        }
                    }
                    else if (tslot.FirstValue.IsValid)
                    {
                        var subtemplate = tslot.FirstValue.Parsed.Message;
                        if (subtemplate != null)
                        {
                            for (int j = 0; j < rslot.Values.Count; ++j)
                            {
                                var subraw = rslot.Values[j];
                                var submess = subraw.Parsed.Message;
                                if (submess != null)
                                {
                                    ApplyTemplate(submess, subtemplate);
                                }
                            }
                        }
                    }
                }
            }
        }
        public static ProtobufMessage ApplyTemplate(ProtobufMessage raw, ProtobufMessage template)
        {
            for (int i = 1; i <= 16; ++i)
            {
                var rslot = raw.GetSlot(i);
                var tslot = template.GetSlot(i);
                ApplyTemplate(rslot, tslot);
            }
            foreach (var tslotkvp in template._HighFields)
            {
                var tslot = tslotkvp.Value;
                var rslot = raw.GetOrCreateSlot(tslot.Desc.Number);
                ApplyTemplate(rslot, tslot);
            }
            raw.FinishBuild();
            return raw;
        }

        public static int WriteVariant(ulong value, IList<byte> buffer, int offset)
        {
            int cnt = 0;
            ulong rest = value;
            while (rest >= 128)
            {
                var part = (byte)((rest & 0x7F) + 0x80);
                buffer.Insert(offset + (cnt++), part);
                rest >>= 7;
            }
            buffer.Insert(offset + (cnt++), (byte)rest);
            return cnt;
        }
        public static int WriteFixed32(uint value, IList<byte> buffer, int offset)
        {
            for (int i = 0; i < 4; ++i)
            {
                var part = (byte)(value >> (i * 8));
                buffer.Insert(offset + i, part);
            }
            return 4;
        }
        public static int WriteFixed64(ulong value, IList<byte> buffer, int offset)
        {
            for (int i = 0; i < 8; ++i)
            {
                var part = (byte)(value >> (i * 8));
                buffer.Insert(offset + i, part);
            }
            return 8;
        }
        public static int WriteTag(int num, ProtobufLowLevelType ltype, IList<byte> buffer, int offset)
        {
            ulong tag = (ulong)num;
            tag <<= 3;
            tag += ((ulong)ltype) & 0x07;
            return WriteVariant(tag, buffer, offset);
        }
        public static int WriteSegment(ListSegment<byte> list, IList<byte> buffer, int offset)
        {
            for (int i = 0; i < list.Count; ++i)
            {
                buffer.Insert(offset + i, list[i]);
            }
            return list.Count;
        }
        public static int WriteString(string value, IList<byte> buffer, int offset)
        {
            var arr = System.Text.Encoding.UTF8.GetBytes(value);
            for (int i = 0; i < arr.Length; ++i)
            {
                buffer.Insert(offset + i, arr[i]);
            }
            return arr.Length;
        }
        public static int WriteRaw(ListSegment<byte> raw, IList<byte> buffer, int offset)
        {
            int cnt = 0;
            if (raw.List == null)
            {
                cnt += WriteVariant(0, buffer, offset + cnt);
            }
            else
            {
                cnt += WriteVariant((ulong)raw.Count, buffer, offset + cnt);
                cnt += WriteSegment(raw, buffer, offset + cnt);
            }
            return cnt;
        }
        public static int WriteVariant(ProtobufValue value, int fieldnum, ProtobufNativeType ntype, IList<byte> buffer, int offset)
        {
            int cnt = 0;
            var val = value.Parsed.UInt64;
            cnt += WriteTag(fieldnum, ProtobufLowLevelType.Varint, buffer, offset + cnt);
            cnt += WriteVariant(val, buffer, offset + cnt);
            return cnt;
        }
        public static int WriteFixed32(ProtobufValue value, int fieldnum, ProtobufNativeType ntype, IList<byte> buffer, int offset)
        {
            int cnt = 0;
            var val = value.Parsed.UInt32;
            cnt += WriteTag(fieldnum, ProtobufLowLevelType.Fixed32, buffer, offset + cnt);
            cnt += WriteFixed32(val, buffer, offset + cnt);
            return cnt;
        }
        public static int WriteFixed64(ProtobufValue value, int fieldnum, ProtobufNativeType ntype, IList<byte> buffer, int offset)
        {
            int cnt = 0;
            var val = value.Parsed.UInt64;
            cnt += WriteTag(fieldnum, ProtobufLowLevelType.Fixed64, buffer, offset + cnt);
            cnt += WriteFixed64(val, buffer, offset + cnt);
            return cnt;
        }

        public static ulong EncodeZigZag64(long val)
        {
            var zval = (ulong)((val << 1) ^ (val >> 63));
            return zval;
        }
        public static uint EncodeZigZag32(int val)
        {
            var zval = (uint)((val << 1) ^ (val >> 31));
            return zval;
        }

        private delegate int EncodeFuncForNativeType(ProtobufValue value, int fieldnum, ProtobufNativeType ntype, IList<byte> buffer, int offset);
        private static Dictionary<ProtobufNativeType, EncodeFuncForNativeType> _EncodeForNativeTypeFuncs = new Dictionary<ProtobufNativeType, EncodeFuncForNativeType>()
        {
            { ProtobufNativeType.TYPE_BOOL, WriteVariant },
            { ProtobufNativeType.TYPE_BYTES,
                (value, fieldnum, ntype, buffer, offset) =>
                {
                    int cnt = 0;
                    cnt += WriteTag(fieldnum, ProtobufLowLevelType.LengthDelimited, buffer, offset + cnt);
                    if (value.Parsed.IsObject)
                    {
                        var bytes = value.Parsed.Get<byte[]>();
                        if (bytes != null)
                        {
                            var raw = new ListSegment<byte>(bytes);
                            cnt += WriteRaw(raw, buffer, offset + cnt);
                            return cnt;
                        }
                        var unk = value.Parsed.Get<ProtobufUnknowValue>();
                        if (unk != null)
                        {
                            cnt += WriteRaw(unk.Raw, buffer, offset + cnt);
                            return cnt;
                        }
                    }
                    cnt += WriteRaw(value.RawData, buffer, offset + cnt);
                    return cnt;
                }
            },
            { ProtobufNativeType.TYPE_DOUBLE, WriteFixed64 },
            { ProtobufNativeType.TYPE_ENUM, WriteVariant },
            { ProtobufNativeType.TYPE_FIXED32, WriteFixed32 },
            { ProtobufNativeType.TYPE_FIXED64, WriteFixed64 },
            { ProtobufNativeType.TYPE_FLOAT, WriteFixed32 },
            { ProtobufNativeType.TYPE_INT32, WriteVariant },
            { ProtobufNativeType.TYPE_INT64, WriteVariant },
            { ProtobufNativeType.TYPE_MESSAGE,
                (value, fieldnum, ntype, buffer, offset) =>
                {
                    int cnt = 0;
                    cnt += WriteTag(fieldnum, ProtobufLowLevelType.LengthDelimited, buffer, offset + cnt);
                    var message = value.Parsed.Message;
                    if (message != null)
                    {
                        var ccnt = WriteRaw(message, buffer, offset + cnt);
                        cnt += WriteVariant((ulong)ccnt, buffer, offset + cnt);
                        cnt += ccnt;
                    }
                    else
                    {
                        cnt += WriteVariant(0, buffer, offset + cnt);
                    }
                    return cnt;
                }
            },
            { ProtobufNativeType.TYPE_SFIXED32, WriteFixed32 },
            { ProtobufNativeType.TYPE_SFIXED64, WriteFixed64 },
            { ProtobufNativeType.TYPE_SINT32,
                (value, fieldnum, ntype, buffer, offset) =>
                {
                    int cnt = 0;
                    var val = value.Parsed.Int32;
                    var zval = EncodeZigZag32(val);
                    cnt += WriteTag(fieldnum, ProtobufLowLevelType.Varint, buffer, offset + cnt);
                    cnt += WriteVariant(zval, buffer, offset + cnt);
                    return cnt;
                }
            },
            { ProtobufNativeType.TYPE_SINT64,
                (value, fieldnum, ntype, buffer, offset) =>
                {
                    int cnt = 0;
                    var val = value.Parsed.Int64;
                    var zval = EncodeZigZag64(val);
                    cnt += WriteTag(fieldnum, ProtobufLowLevelType.Varint, buffer, offset + cnt);
                    cnt += WriteVariant(zval, buffer, offset + cnt);
                    return cnt;
                }
            },
            { ProtobufNativeType.TYPE_STRING,
                (value, fieldnum, ntype, buffer, offset) =>
                {
                    int cnt = 0;
                    cnt += WriteTag(fieldnum, ProtobufLowLevelType.LengthDelimited, buffer, offset + cnt);
                    var str = value.Parsed.String;
                    if (str != null)
                    {
                        var ccnt = WriteString(str, buffer, offset + cnt);
                        cnt += WriteVariant((ulong)ccnt, buffer, offset + cnt);
                        cnt += ccnt;
                    }
                    else
                    {
                        cnt += WriteVariant(0, buffer, offset + cnt);
                    }
                    return cnt;
                }
            },
            { ProtobufNativeType.TYPE_UINT32, WriteVariant },
            { ProtobufNativeType.TYPE_UINT64, WriteVariant },
            { ProtobufNativeType.TYPE_UNKNOWN,
                (value, fieldnum, ntype, buffer, offset) =>
                {
                    int cnt = 0;
                    cnt += WriteTag(fieldnum, ProtobufLowLevelType.LengthDelimited, buffer, offset + cnt);
                    if (value.Parsed.IsObject)
                    {
                        var unk = value.Parsed.Get<ProtobufUnknowValue>();
                        if (unk != null)
                        {
                            cnt += WriteRaw(unk.Raw, buffer, offset + cnt);
                            return cnt;
                        }
                    }
                    cnt += WriteRaw(value.RawData, buffer, offset + cnt);
                    return cnt;
                }
            },
        };
        public static int WriteRaw(ProtobufValue value, int fieldnum, ProtobufNativeType ntype, IList<byte> buffer, int offset)
        {
            int cnt = 0;
            EncodeFuncForNativeType func;
            if (ntype == 0 || !_EncodeForNativeTypeFuncs.TryGetValue(ntype, out func))
            { // not templated. try default encode.
                if (value.Parsed.IsEmpty || value.Parsed.IsObject)
                {
                    cnt += WriteTag(fieldnum, ProtobufLowLevelType.LengthDelimited, buffer, offset + cnt);
                    if (value.Parsed.IsEmpty)
                    {
                        cnt += WriteRaw(value.RawData, buffer, offset + cnt);
                    }
                    else
                    {
                        var obj = value.Parsed.Get();
                        if (obj is string)
                        {
                            var ccnt = WriteString((string)obj, buffer, offset + cnt);
                            cnt += WriteVariant((ulong)ccnt, buffer, offset + cnt);
                            cnt += ccnt;
                        }
                        else if (obj is ProtobufUnknowValue)
                        {
                            var raw = ((ProtobufUnknowValue)obj).Raw;
                            cnt += WriteRaw(raw, buffer, offset + cnt);
                        }
                        else if (obj is byte[])
                        {
                            var raw = new ListSegment<byte>((byte[])obj);
                            cnt += WriteRaw(raw, buffer, offset + cnt);
                        }
                        else if (obj is ProtobufMessage)
                        {
                            var ccnt = WriteRaw((ProtobufMessage)obj, buffer, offset + cnt);
                            cnt += WriteVariant((ulong)ccnt, buffer, offset + cnt);
                            cnt += ccnt;
                        }
                        else
                        {
                            cnt += WriteRaw(value.RawData, buffer, offset + cnt);
                        }
                    }
                }
                else
                {
                    cnt += WriteVariant(value, fieldnum, ntype, buffer, offset + cnt);
                }
            }
            else
            {
                cnt += func(value, fieldnum, ntype, buffer, offset + cnt);
            }
            return cnt;
        }
        public static int WriteRaw(ProtobufMessage message, IList<byte> buffer, int offset)
        {
            int cnt = 0;
            for (int i = 0; i < 16; ++i)
            {
                var slot = message._LowFields[i];
                if (slot.Values.Count > 0)
                {
                    var fnum = slot.Desc.Number;
                    var ntype = slot.Desc.Type.KnownType;
                    for (int j = 0; j < slot.Values.Count; ++j)
                    {
                        cnt += WriteRaw(slot.Values[j], fnum, ntype, buffer, offset + cnt);
                    }
                }
            }
            int[] hnums = new int[message._HighFields.Count];
            message._HighFields.Keys.CopyTo(hnums, 0);
            Array.Sort(hnums);
            for (int i = 0; i < hnums.Length; ++i)
            {
                var slot = message._HighFields[hnums[i]];
                if (slot.Values.Count > 0)
                {
                    var fnum = slot.Desc.Number;
                    var ntype = slot.Desc.Type.KnownType;
                    for (int j = 0; j < slot.Values.Count; ++j)
                    {
                        cnt += WriteRaw(slot.Values[j], fnum, ntype, buffer, offset + cnt);
                    }
                }
            }
            return cnt;
        }
    }

    public static class ProtobufMessagePool
    {
        public readonly static TemplateProtobufMessage FieldOptionsTemplate = new TemplateProtobufMessage("google.protobuf.FieldOptions")
        {
            //optional CType ctype = 1 [default = STRING];
            //optional bool packed = 2;
            //optional JSType jstype = 6 [default = JS_NORMAL];
            //optional bool lazy = 5 [default=false];
            //optional bool deprecated = 3 [default=false];
            //optional bool weak = 10 [default=false];
            //repeated UninterpretedOption uninterpreted_option = 999;
        };
        public readonly static TemplateProtobufMessage EnumValueDescriptorTemplate = new TemplateProtobufMessage("google.protobuf.EnumValueDescriptorProto")
        {
            { 1, "name", ProtobufNativeType.TYPE_STRING },
            { 2, "number", ProtobufNativeType.TYPE_INT32 },
            //optional EnumValueOptions options = 3;
        };
        public readonly static TemplateProtobufMessage EnumDescriptorTemplate = new TemplateProtobufMessage("google.protobuf.EnumDescriptorProto")
        {
            { 1, "name", ProtobufNativeType.TYPE_STRING },
            { 2, "value", EnumValueDescriptorTemplate },
            //optional EnumOptions options = 3;
            //repeated EnumReservedRange reserved_range = 4;
            //repeated string reserved_name = 5;
        };
        public readonly static TemplateProtobufMessage FieldDescriptorTemplate = new TemplateProtobufMessage("google.protobuf.FieldDescriptorProto")
        {
            { 1, "name", ProtobufNativeType.TYPE_STRING },
            { 3, "number", ProtobufNativeType.TYPE_INT32 },
            { 4, "label", default(ProtobufFieldLabel) },
            { 5, "type", ProtobufNativeType.TYPE_ENUM, default(ProtobufNativeType) },
            { 6, "type_name", ProtobufNativeType.TYPE_STRING },
            { 2, "extendee", ProtobufNativeType.TYPE_STRING },
            { 7, "default_value", ProtobufNativeType.TYPE_STRING },
            { 9, "oneof_index", ProtobufNativeType.TYPE_INT32 },
            { 10, "json_name", ProtobufNativeType.TYPE_STRING },
            { 8, "options", FieldOptionsTemplate },
        };
        public readonly static TemplateProtobufMessage MessageDescriptorTemplate = new TemplateProtobufMessage("google.protobuf.DescriptorProto")
        {
            { 1, "name", ProtobufNativeType.TYPE_STRING },
            { 2, "field", FieldDescriptorTemplate },
            { 6, "extension", FieldDescriptorTemplate },
            //{ 3, "nested_type", MessageDescriptorTemplate },
            { 4, "enum_type", EnumDescriptorTemplate },
            //repeated ExtensionRange extension_range = 5;
            //repeated OneofDescriptorProto oneof_decl = 8;
            //optional MessageOptions options = 7;
            //repeated ReservedRange reserved_range = 9;
            { 10, "reserved_name", ProtobufNativeType.TYPE_STRING },
        };
        public readonly static TemplateProtobufMessage ServiceDescriptorTemplate = new TemplateProtobufMessage("google.protobuf.ServiceDescriptorProto")
        {
            { 1, "name", typeof(string) },
            //repeated MethodDescriptorProto method = 2;
            //optional ServiceOptions options = 3;
        };
        public readonly static TemplateProtobufMessage DescriptorFileTemplate = new TemplateProtobufMessage("google.protobuf.FileDescriptorProto")
        {
            { 1, "name", ProtobufNativeType.TYPE_STRING },
            { 2, "package", ProtobufNativeType.TYPE_STRING },
            { 3, "dependency", ProtobufNativeType.TYPE_STRING },
            { 10, "public_dependency", ProtobufNativeType.TYPE_INT32 },
            { 11, "weak_dependency", ProtobufNativeType.TYPE_INT32 },
            { 4, "message_type", MessageDescriptorTemplate },
            { 5, "enum_type", EnumDescriptorTemplate },
            { 6, "service", ServiceDescriptorTemplate},
            { 7, "extension", FieldDescriptorTemplate},
            // optional FileOptions options = 8;
            // optional SourceCodeInfo source_code_info = 9;
            { 12, "syntax", ProtobufNativeType.TYPE_STRING },
        };
        static ProtobufMessagePool()
        {
            MessageDescriptorTemplate.Add(3, "nested_type", MessageDescriptorTemplate);
            // TODO: add exsiting to a dict
            FieldOptionsTemplate.FinishBuild();
            EnumValueDescriptorTemplate.FinishBuild();
            EnumDescriptorTemplate.FinishBuild();
            FieldDescriptorTemplate.FinishBuild();
            MessageDescriptorTemplate.FinishBuild();
            ServiceDescriptorTemplate.FinishBuild();
            DescriptorFileTemplate.FinishBuild();
        }

        private static void GetMessages(ProtobufMessage parent, string pre, Dictionary<string, ProtobufMessage> messages)
        {
            var myname = parent["name"].String;
            var myfullname = pre + myname;
            var childpre = myfullname + ".";
            messages[myfullname] = parent;
            var subs = parent["nested_type"].Messages;
            for (int i = 0; i < subs.Count; ++i)
            {
                var sub = subs[i];
                GetMessages(sub, childpre, messages);
            }
        }
        public static Dictionary<string, TemplateProtobufMessage> ReadTemplates(ListSegment<byte> compiledFileData)
        {
            ProtobufMessage file = ProtobufEncoder.ReadRaw(compiledFileData);
            if (file != null)
            {
                ProtobufEncoder.ApplyTemplate(file, DescriptorFileTemplate);
                Dictionary<string, TemplateProtobufMessage> templates = new Dictionary<string, TemplateProtobufMessage>();
                var package = file["package"].String;
                Dictionary<string, ProtobufMessage> allmessages = new Dictionary<string, ProtobufMessage>();
                var messages = file["message_type"].Messages;
                var rootpre = package + ".";
                for (int i = 0; i < messages.Count; ++i)
                {
                    var message = messages[i];
                    GetMessages(message, rootpre, allmessages);
                }
                foreach (var kvp in allmessages)
                {
                    templates[kvp.Key] = new TemplateProtobufMessage(kvp.Key);
                }
                foreach (var kvp in allmessages)
                {
                    var message = kvp.Value;
                    var template = templates[kvp.Key];
                    var fields = message["field"].Messages;
                    for (int i = 0; i < fields.Count; ++i)
                    {
                        var field = fields[i];
                        var name = field["name"].String;
                        var num = field["number"].Int32;
                        var ntype = field["type"].AsEnum<ProtobufNativeType>();
                        var mtype = field["type_name"].String;
                        var label = field["label"].AsEnum<ProtobufFieldLabel>();
                        if (num > 0 && !string.IsNullOrEmpty(name))
                        {
                            var slot = template.GetOrCreateSlot(num);
                            slot.Desc.Name = name;
                            slot.Desc.Type.KnownType = ntype;
                            slot.Desc.Label = label;
                            if (ntype == ProtobufNativeType.TYPE_MESSAGE)
                            {
                                if (mtype != null)
                                {
                                    if (mtype.StartsWith("."))
                                    {
                                        mtype = mtype.Substring(1);
                                    }
                                    slot.Desc.Type.MessageName = mtype;
                                    TemplateProtobufMessage refmessage;
                                    if (templates.TryGetValue(mtype, out refmessage))
                                    {
                                        slot.FirstValue = new ProtobufValue() { Parsed = refmessage };
                                    }
                                    // TODO: search in alreay loaded protocols.
                                }
                            }
                            else if (ntype == ProtobufNativeType.TYPE_ENUM)
                            {
                                if (mtype != null)
                                {
                                    if (mtype.StartsWith("."))
                                    {
                                        mtype = mtype.Substring(1);
                                    }
                                    slot.Desc.Type.MessageName = mtype;
                                    // TODO: search in enum pool.
                                }
                            }
                        }
                    }
                }
                foreach (var kvp in allmessages)
                {
                    var template = templates[kvp.Key];
                    template.FinishBuild();
                }
                return templates;
            }
            return null;
        }

        // TODO: enum pool
        // TODO: predefined enum pool
        // TODO: predefined message pool
    }

#if UNITY_INCLUDE_TESTS
    #region TESTS
    public static class ProtobufDynamicMessageTest
    {
#if UNITY_EDITOR
        [UnityEditor.MenuItem("Test/Dynamic Protobuf Message/Test Encode", priority = 100010)]
        public static void TestEncode()
        {
            UnityEngine.Debug.Log(ProtobufMessagePool.MessageDescriptorTemplate.ToString());

            var message = new ProtobufMessage();
            var slot = message.GetOrCreateSlot(1);
            var sub = new ProtobufMessage();
            slot.Values.Add(new ProtobufValue() { Parsed = sub });
            var sslot = sub.GetOrCreateSlot(2);
            sslot.Values.Add(new ProtobufValue { Parsed = 1u });
            sslot.Values.Add(new ProtobufValue { Parsed = 2u });
            sslot.Values.Add(new ProtobufValue { Parsed = 3u });

            var tmessage = new ProtobufMessage();
            var tslot = tmessage.GetOrCreateSlot(1);
            tslot.Desc.Name = "submessage";
            var tsub = new ProtobufMessage();
            tslot.Values.Add(new ProtobufValue() { Parsed = tsub });
            var tsslot = tsub.GetOrCreateSlot(2);
            tsslot.Desc.Name = "floatval";
            tsslot.Desc.Type.KnownType = ProtobufNativeType.TYPE_FLOAT;

            message.ApplyTemplate(tmessage);

            UnityEngine.Debug.Log(message.ToString());
        }

        [UnityEditor.MenuItem("Test/Dynamic Protobuf Message/Test Decode", priority = 100020)]
        public static void TestDecode()
        {
            var templates = ProtobufMessagePool.ReadTemplates(new ListSegment<byte>(TestDescriptorFileData));
            foreach (var kvp in templates)
            {
                UnityEngine.Debug.Log(kvp.Value.ToString());
            }
        }
#endif

        #region Descriptor Data
        public static byte[] TestDescriptorFileData = global::System.Convert.FromBase64String(
          string.Concat(
            "ChJTcmMvQ29tYmluZWQucHJvdG8SCXByb3RvY29scyIQCg5TZXJ2ZXJTdGF0",
            "dXNPcCImChBTZXJ2ZXJTdGF0dXNSZXNwEhIKClJvb21TdGF0dXMYASADKA0i",
            "BQoDTm9wIgcKBVJlc2V0Ii4KEU9wcG9uZW50Q29ubmVjdGVkEgsKA3VpZBgB",
            "IAEoCRIMCgRuYW1lGAIgASgJIhYKFE9wcG9uZW50RGlzY29ubmVjdGVkIjAK",
            "DEdhbWVyc1N0YXR1cxIPCgdob21lUlRUGAEgASgNEg8KB2F3YXlSVFQYAiAB",
            "KA0iOgoPQ29ubmVjdFRvUm9vbU9wEgsKA3VpZBgBIAEoCRIMCgRuYW1lGAIg",
            "ASgJEgwKBHJvb20YAyABKAkiWAoRQ29ubmVjdFRvUm9vbVJlc3ASDwoHc3Vj",
            "Y2VzcxgBIAEoCBIhCgRzaWRlGAIgASgOMhMucHJvdG9jb2xzLlRlYW1TaWRl",
            "Eg8KB3N0YXJ0ZWQYAyABKAgiHgoMQ2hhbmdlU2lkZU9wEg4KBmFjY2VwdBgB",
            "IAEoCCIUChJDaGFuZ2VTaWRlUXVlc3Rpb24iMwoOQ2hhbmdlU2lkZVJlc3AS",
            "IQoEc2lkZRgBIAEoDjITLnByb3RvY29scy5UZWFtU2lkZSIeCgxTdGFydE1h",
            "dGNoT3ASDgoGYWNjZXB0GAEgASgIIhQKElN0YXJ0TWF0Y2hRdWVzdGlvbiJg",
            "Cg5TdGFydE1hdGNoUmVzcBIhCgRzaWRlGAEgASgOMhMucHJvdG9jb2xzLlRl",
            "YW1TaWRlEisKBGRhdGEYAiABKAsyHS5wcm90b2NvbHMuRnVsbE1hdGNoU2l0",
            "dWF0aW9uIjgKDk5leHRCYXR0ZXJJbmZvEhQKDGJhdHRpbmdPcmRlchgBIAEo",
            "DRIQCghiYXR0ZXJJZBgCIAEoDSIyCgtHYW1lck9wSW5mbxIRCglwaXRjaGVy",
            "T3AYASABKAgSEAoIYmF0dGVyT3AYAiABKAgioAIKFlBpdGNoU3RhcnRBdXRv",
            "T3BFdmVudHMSLwoJc3RlYWxCYXNlGAEgASgLMhwucHJvdG9jb2xzLlNldFN0",
            "ZWFsQmFzZUV2ZW50EjIKDGNoYW5nZVBsYXllchgCIAEoCzIcLnByb3RvY29s",
            "cy5DaGFuZ2VQbGF5ZXJFdmVudBIzCgtiYXR0aW5nTW9kZRgDIAEoCzIeLnBy",
            "b3RvY29scy5TZXRCYXR0aW5nTW9kZUV2ZW50Ej0KD3VwZGF0ZVNpdHVhdGlv",
            "bhgEIAEoCzIkLnByb3RvY29scy5VcGRhdGVNYXRjaFNpdHVhdGlvbkV2ZW50",
            "Ei0KCmNhc3RTa2lsbHMYBSABKAsyGS5wcm90b2NvbHMuQ2FzdFNraWxsRXZl",
            "bnQipwEKEkZ1bGxNYXRjaFNpdHVhdGlvbhIxCg5tYXRjaFNpdHVhdGlvbhgB",
            "IAEoCzIZLnByb3RvY29scy5NYXRjaFNpdHVhdGlvbhIrCgdwbGF5ZXJzGAIg",
            "ASgLMhoucHJvdG9jb2xzLlBsYXllclNpdHVhdGlvbhIxCg5waXRjaFNpdHVh",
            "dGlvbhgDIAEoCzIZLnByb3RvY29scy5QaXRjaFNpdHVhdGlvbiKzAgoOTWF0",
            "Y2hTaXR1YXRpb24SDgoGaW5uaW5nGAEgASgNEiMKBGhhbGYYAiABKA4yFS5w",
            "cm90b2NvbHMuSW5uaW5nSGFsZhISCgpwaXRjaENvdW50GAMgASgNEgsKA291",
            "dBgEIAEoDRIOCgZzdHJpa2UYBSABKA0SDAoEYmFsbBgGIAEoDRINCgVlbmRl",
            "ZBgHIAEoCBIpCgxob21lVGVhbUluZm8YCCABKAsyEy5wcm90b2NvbHMuVGVh",
            "bUluZm8SKQoMYXdheVRlYW1JbmZvGAkgASgLMhMucHJvdG9jb2xzLlRlYW1J",
            "bmZvEjUKEm5leHRUaHJlZUJhdHRlcklkcxgKIAMoCzIZLnByb3RvY29scy5O",
            "ZXh0QmF0dGVySW5mbxIRCgltYXRjaFR5cGUYCyABKAkiowUKDlBsYXllcklu",
            "Zm9MaXRlEgoKAmlkGAEgASgNEi0KCWFiaWxpdGllcxgCIAEoCzIaLnByb3Rv",
            "Y29scy5QbGF5ZXJBYmlsaXRpZXMSDQoFcG93ZXIYCCABKAISHQoEcm9sZRgJ",
            "IAEoDjIPLnByb3RvY29scy5Sb2xlEisKC29uRmllbGRSb2xlGAogASgOMhYu",
            "cHJvdG9jb2xzLk9uRmllbGRSb2xlEiwKCnBpdGNoVHlwZXMYDCADKAsyGC5w",
            "cm90b2NvbHMuUGl0Y2hUeXBlSW5mbxI5ChJiYXR0aW5nUHJvZmljaWVuY3kY",
            "DiABKAsyHS5wcm90b2NvbHMuQmF0dGluZ1Byb2ZpY2llbmN5EhcKD3BsYXRl",
            "QXBwZWFyYW5jZRgQIAEoDRIOCgZhdEJhdHMYESABKA0SDAoEcnVucxgSIAEo",
            "DRIMCgRoaXRzGBMgASgNEg4KBmVycm9ycxgUIAEoDRIQCghob21lUnVucxgV",
            "IAEoDRISCgpwaXRjaENvdW50GBYgASgNEhwKFGxlZnRFbmVyZ3lQZXJjZW50",
            "YWdlGBcgASgCEhEKCXBvc2l0aW9uWBgYIAEoAhIRCglwb3NpdGlvblkYGSAB",
            "KAISEQoJcm90YXRpb25YGBogASgCEhEKCXJvdGF0aW9uWRgbIAEoAhIRCgly",
            "b3RhdGlvbloYHCABKAISMAoMb3V0cHV0U2tpbGxzGB0gAygLMhoucHJvdG9j",
            "b2xzLk91dHB1dFNraWxsSW5mbxI0ChBvdXRwdXRTdGFydEJ1ZmZzGB8gAygL",
            "MhoucHJvdG9jb2xzLlBsYXllclNraWxsSW5mbxIyCg5vdXRwdXRFbmRCdWZm",
            "cxggIAMoCzIaLnByb3RvY29scy5QbGF5ZXJTa2lsbEluZm8iewoMVGVhbUlu",
            "Zm9MaXRlEiMKBXN0YXRzGAMgASgLMhQucHJvdG9jb2xzLlRlYW1TdGF0cxIZ",
            "ChFsZWZ0T3ZlckxvcmRUaW1lcxgIIAEoDRIQCghsaXZlbmVzcxgJIAEoAhIZ",
            "ChFsZWZ0U3RhcnRlckVuZXJneRgLIAEoAiKsAgoSTWF0Y2hTaXR1YXRpb25M",
            "aXRlEg4KBmlubmluZxgBIAEoDRIjCgRoYWxmGAIgASgOMhUucHJvdG9jb2xz",
            "LklubmluZ0hhbGYSEgoKcGl0Y2hDb3VudBgDIAEoDRILCgNvdXQYBCABKA0S",
            "DgoGc3RyaWtlGAUgASgNEgwKBGJhbGwYBiABKA0SDQoFZW5kZWQYByABKAgS",
            "LQoMaG9tZVRlYW1JbmZvGAggASgLMhcucHJvdG9jb2xzLlRlYW1JbmZvTGl0",
            "ZRItCgxhd2F5VGVhbUluZm8YCSABKAsyFy5wcm90b2NvbHMuVGVhbUluZm9M",
            "aXRlEjUKEm5leHRUaHJlZUJhdHRlcklkcxgKIAMoCzIZLnByb3RvY29scy5O",
            "ZXh0QmF0dGVySW5mbyJvChNQbGF5ZXJTaXR1YXRpb25MaXRlEisKCGhvbWVU",
            "ZWFtGAEgAygLMhkucHJvdG9jb2xzLlBsYXllckluZm9MaXRlEisKCGF3YXlU",
            "ZWFtGAIgAygLMhkucHJvdG9jb2xzLlBsYXllckluZm9MaXRlIrMBChZGdWxs",
            "TWF0Y2hTaXR1YXRpb25MaXRlEjUKDm1hdGNoU2l0dWF0aW9uGAEgASgLMh0u",
            "cHJvdG9jb2xzLk1hdGNoU2l0dWF0aW9uTGl0ZRIvCgdwbGF5ZXJzGAIgASgL",
            "Mh4ucHJvdG9jb2xzLlBsYXllclNpdHVhdGlvbkxpdGUSMQoOcGl0Y2hTaXR1",
            "YXRpb24YAyABKAsyGS5wcm90b2NvbHMuUGl0Y2hTaXR1YXRpb24iUgoXT25G",
            "aWVsZFJvbGVUb0lETWFwRW50cnkSKwoLb25GaWVsZFJvbGUYASABKA4yFi5w",
            "cm90b2NvbHMuT25GaWVsZFJvbGUSCgoCaWQYAiABKA0itgIKCFRlYW1JbmZv",
            "EgwKBG5hbWUYASABKAkSDgoGY2x1YklkGAIgASgJEiMKBXN0YXRzGAMgASgL",
            "MhQucHJvdG9jb2xzLlRlYW1TdGF0cxIlCgxvcmRlck9mUm9sZXMYBCADKA4y",
            "Dy5wcm90b2NvbHMuUm9sZRIWCg5vdmVyTG9yZEVuZXJneRgFIAEoAhITCgto",
            "b21lU2hpcnRJRBgGIAEoCRITCgthd2F5U2hpcnRJRBgHIAEoCRIZChFsZWZ0",
            "T3ZlckxvcmRUaW1lcxgIIAEoDRIQCghsaXZlbmVzcxgJIAEoAhI2Cg9zZWNy",
            "ZXRhcnlTa2lsbHMYCiADKAsyHS5wcm90b2NvbHMuU2VjcmV0YXJ5U2tpbGxJ",
            "bmZvEhkKEWxlZnRTdGFydGVyRW5lcmd5GAsgASgCIpYDCg5QaXRjaFNpdHVh",
            "dGlvbhIoCgtwaXRjaGVyU2lkZRgBIAEoDjITLnByb3RvY29scy5UZWFtU2lk",
            "ZRIPCgdwaXRjaGVyGAIgASgNEg8KB2NhdGNoZXIYAyABKA0SDgoGYmF0dGVy",
            "GAQgASgNEiIKBG1vZGUYBSABKAsyFC5wcm90b2NvbHMuUGl0Y2hNb2RlEjAK",
            "CmJhdHRlclByb2YYBiABKAsyHC5wcm90b2NvbHMuQmF0dGVyUHJvZmljaWVu",
            "Y3kSKwoLYmFzZVJ1bm5lcnMYByABKAsyFi5wcm90b2NvbHMuQmFzZVJ1bm5l",
            "cnMSKAoFZmllbGQYCCABKAsyGS5wcm90b2NvbHMuRmllbGRTaXR1YXRpb24S",
            "NwoLb25GaWVsZFRvSUQYCSADKAsyIi5wcm90b2NvbHMuT25GaWVsZFJvbGVU",
            "b0lETWFwRW50cnkSIAoYaW5pdFBpdGNoVGFyZ2V0UG9zaXRpb25YGAogASgC",
            "EiAKGGluaXRQaXRjaFRhcmdldFBvc2l0aW9uWRgLIAEoAiKGAQoMTGl2ZW5l",
            "c3NJbmZvEhIKCmV4dHJhQmFzZXMYASABKA0SEQoJZGVsdGFSdW5zGAIgASgN",
            "EhcKD2RlbHRhUnVubmVyT3V0cxgDIAEoDRIXCg9zdHJvbmdCYXR0ZXJPdXQY",
            "BCABKA0SHQoVcGl0Y2hUaW1lc0luQ3VycmVudFBBGAUgASgNImMKD1BsYXll",
            "clNpdHVhdGlvbhInCghob21lVGVhbRgBIAMoCzIVLnByb3RvY29scy5QbGF5",
            "ZXJJbmZvEicKCGF3YXlUZWFtGAIgAygLMhUucHJvdG9jb2xzLlBsYXllcklu",
            "Zm8iVwoPT3V0cHV0U2tpbGxJbmZvEgoKAmlkGAEgASgJEjgKD2Nhc3RpbmdU",
            "aW1lVHlwZRgCIAEoDjIfLnByb3RvY29scy5Ta2lsbENhc3RpbmdUaW1lVHlw",
            "ZSIdCg9QbGF5ZXJTa2lsbEluZm8SCgoCaWQYASABKAki4wgKClBsYXllcklu",
            "Zm8SCgoCaWQYASABKA0SLQoJYWJpbGl0aWVzGAIgASgLMhoucHJvdG9jb2xz",
            "LlBsYXllckFiaWxpdGllcxISCgphZGFwdFJvbGVzGAMgAygNEisKBXN0YXRz",
            "GAQgASgLMhwucHJvdG9jb2xzLlBsYXllclNlYXNvblN0YXRzEgsKA2NpZBgF",
            "IAEoCRIPCgdraXROYW1lGAYgASgJEg4KBm51bWJlchgHIAEoCRINCgVwb3dl",
            "chgIIAEoAhIdCgRyb2xlGAkgASgOMg8ucHJvdG9jb2xzLlJvbGUSKwoLb25G",
            "aWVsZFJvbGUYCiABKA4yFi5wcm90b2NvbHMuT25GaWVsZFJvbGUSJgoJcGl0",
            "Y2hIYW5kGAsgASgOMhMucHJvdG9jb2xzLkhhbmRUeXBlEiwKCnBpdGNoVHlw",
            "ZXMYDCADKAsyGC5wcm90b2NvbHMuUGl0Y2hUeXBlSW5mbxIoCgtiYXR0aW5n",
            "SGFuZBgNIAEoDjITLnByb3RvY29scy5IYW5kVHlwZRI5ChJiYXR0aW5nUHJv",
            "ZmljaWVuY3kYDiABKAsyHS5wcm90b2NvbHMuQmF0dGluZ1Byb2ZpY2llbmN5",
            "Ei4KDWRvbWluYXRlVHlwZXMYDyADKA4yFy5wcm90b2NvbHMuRG9taW5hdGVU",
            "eXBlEhcKD3BsYXRlQXBwZWFyYW5jZRgQIAEoDRIOCgZhdEJhdHMYESABKA0S",
            "DAoEcnVucxgSIAEoDRIMCgRoaXRzGBMgASgNEg4KBmVycm9ycxgUIAEoDRIQ",
            "Cghob21lUnVucxgVIAEoDRISCgpwaXRjaENvdW50GBYgASgNEhwKFGxlZnRF",
            "bmVyZ3lQZXJjZW50YWdlGBcgASgCEhEKCXBvc2l0aW9uWBgYIAEoAhIRCglw",
            "b3NpdGlvblkYGSABKAISEQoJcm90YXRpb25YGBogASgCEhEKCXJvdGF0aW9u",
            "WRgbIAEoAhIRCglyb3RhdGlvbloYHCABKAISMAoMb3V0cHV0U2tpbGxzGB0g",
            "AygLMhoucHJvdG9jb2xzLk91dHB1dFNraWxsSW5mbxIwCgxwbGF5ZXJTa2ls",
            "bHMYHiADKAsyGi5wcm90b2NvbHMuUGxheWVyU2tpbGxJbmZvEjQKEG91dHB1",
            "dFN0YXJ0QnVmZnMYHyADKAsyGi5wcm90b2NvbHMuUGxheWVyU2tpbGxJbmZv",
            "EjIKDm91dHB1dEVuZEJ1ZmZzGCAgAygLMhoucHJvdG9jb2xzLlBsYXllclNr",
            "aWxsSW5mbxIvCgxwbGF5UG9zaXRpb24YISABKA4yGS5wcm90b2NvbHMuUGxh",
            "eWVyUG9zaXRpb24SFAoMaXNHYW1lUGxheWVyGCIgASgIEjEKDmFwcGVhcmFu",
            "Y2VJbmZvGCMgASgLMhkucHJvdG9jb2xzLkFwcGVhcmFuY2VJbmZvEiUKCHJv",
            "bGVDYXJkGCQgASgLMhMucHJvdG9jb2xzLlJvbGVDYXJkIkIKDkFwcGVhcmFu",
            "Y2VJbmZvEg4KBmZhY2VJRBgBIAEoCRIOCgZza2luSUQYAiABKA0SEAoIc2Ft",
            "YXRvSUQYAyABKA0iWwoIUm9sZUNhcmQSEgoKcHJvdmluY2VJRBgBIAEoDRIO",
            "CgZjaXR5SUQYAiABKA0SCwoDYWdlGAMgASgNEg4KBmhlaWdodBgEIAEoDRIO",
            "CgZ3ZWlnaHQYBSABKA0ivQEKD1BsYXllckFiaWxpdGllcxIPCgdjb250YWN0",
            "GAEgASgCEhAKCHNsdWdnaW5nGAIgASgCEhMKC2Jhc2VSdW5uaW5nGAMgASgC",
            "EhAKCGZpZWxkaW5nGAQgASgCEhUKDXBsYXRlRGlzcGxpbmUYBSABKAISDwoH",
            "c3RhbWluYRgGIAEoAhIPCgdjb250cm9sGAcgASgCEhAKCGJyZWFraW5nGAgg",
            "ASgCEhUKDWV4cGxvc2l2ZW5lc3MYCSABKAIifAoRUGxheWVyU2Vhc29uU3Rh",
            "dHMSCwoDYXZnGAEgASgCEgoKAmhyGAIgASgCEgsKA3JiaRgDIAEoAhIKCgJz",
            "YhgEIAEoAhILCgN3aW4YBSABKAISDAoEbG9zZRgGIAEoAhILCgNlcmEYByAB",
            "KAISDQoFZ2FtZXMYCCABKA0iXQoNUGl0Y2hUeXBlSW5mbxIiCgR0eXBlGAEg",
            "ASgOMhQucHJvdG9jb2xzLlBpdGNoVHlwZRIoCgVncmFkZRgCIAEoDjIZLnBy",
            "b3RvY29scy5QaXRjaFR5cGVHcmFkZSI7ChJCYXR0aW5nUHJvZmljaWVuY3kS",
            "EgoKZ29vZEJsb2NrcxgBIAMoDRIRCgliYWRCbG9ja3MYAiADKA0iEAoORmll",
            "bGRTaXR1YXRpb24iOwoQRG9taW5hdGVPcFN0YXR1cxINCgVjb3VudBgBIAEo",
            "DRIYChBjb3VudEJ5UGl0Y2hUeXBlGAIgAygNIsUBCg5Eb21pbmF0ZVN0YXR1",
            "cxIdChVwaXRjaGVyT3ZlckxvcmRFbmVyZ3kYASABKAISHAoUYmF0dGVyT3Zl",
            "ckxvcmRFbmVyZ3kYAiABKAISIAoYcGl0Y2hlckxlZnRPdmVyTG9yZFRpbWVz",
            "GAMgASgNEh8KF2JhdHRlckxlZnRPdmVyTG9yZFRpbWVzGAQgASgNEjMKDmFj",
            "dGl2ZU9wU3RhdHVzGAUgAygLMhsucHJvdG9jb2xzLkRvbWluYXRlT3BTdGF0",
            "dXMipgEKCVBpdGNoTW9kZRIrCghkb21pbmF0ZRgBIAEoCzIZLnByb3RvY29s",
            "cy5Eb21pbmF0ZVN0YXR1cxIrCgtiYXR0aW5nTW9kZRgCIAEoDjIWLnByb3Rv",
            "Y29scy5CYXR0aW5nTW9kZRItCglzdGVhbEJhc2UYAyABKAsyGi5wcm90b2Nv",
            "bHMuU3RlYWxCYXNlU3RhdHVzEhAKCGF1dG9QbGF5GAQgASgIIi4KEUJhdHRl",
            "clByb2ZpY2llbmN5EgwKBGdvb2QYASADKA0SCwoDYmFkGAIgAygNIiIKClBv",
            "c1ZlY3RvcjISCQoBeBgBIAEoAhIJCgF5GAIgASgCIqEBCg5QaXRjaFNlbGVj",
            "dGlvbhIQCghiYWxsVHlwZRgBIAEoDRIPCgd0YXJnZXRYGAIgASgCEg8KB3Rh",
            "cmdldFkYAyABKAISEgoKcHV6emxlVHlwZRgEIAEoDRIMCgRldmFsGAUgASgN",
            "EhIKCnBpdGNoU3BlZWQYBiABKAISFAoMb2Zmc2V0TGVuZ3RoGAcgASgCEg8K",
            "B3BpY2tvZmYYCCABKA0i8wEKC0JhdHRpbmdJbmZvEgwKBGF1dG8YASABKAgS",
            "CwoDYmF0GAIgASgIEgwKBHRpbWUYAyABKA0SHgoDZGlyGAQgASgOMhEucHJv",
            "dG9jb2xzLkJhdERpchIvCgRldmFsGAUgASgOMiEucHJvdG9jb2xzLkJhdE9w",
            "ZXJhdGlvblJlc3VsdFR5cGUSJwoJYmF0T3BUeXBlGAYgASgOMhQucHJvdG9j",
            "b2xzLkJhdE9wVHlwZRIPCgd0YXJnZXRZGAcgASgCEhcKD3RhcmdldFhBZnRl",
            "ckJhdBgIIAEoAhIXCg90YXJnZXRZQWZ0ZXJCYXQYCSABKAIiOwoLQmFzZVJ1",
            "bm5lcnMSDQoFZmlyc3QYASABKA0SDgoGc2Vjb25kGAIgASgNEg0KBXRoaXJk",
            "GAMgASgNIv8CCghSdW5GcmFtZRIlCgR0eXBlGGQgASgOMhcucHJvdG9jb2xz",
            "LlJ1bkZyYW1lVHlwZRI5ChBmcmFtZURlZmVuc2VNb3ZlGAEgASgLMh8ucHJv",
            "dG9jb2xzLlJ1bkZyYW1lX0RlZmVuc2VNb3ZlEjEKDGZyYW1lUnVuQmFzZRgC",
            "IAEoCzIbLnByb3RvY29scy5SdW5GcmFtZV9SdW5CYXNlEi0KCmZyYW1lQ2F0",
            "Y2gYAyABKAsyGS5wcm90b2NvbHMuUnVuRnJhbWVfQ2F0Y2gSOwoRZnJhbWVI",
            "aXRCYWxsQ2F0Y2gYBCABKAsyIC5wcm90b2NvbHMuUnVuRnJhbWVfSGl0QmFs",
            "bENhdGNoEjEKDGZyYW1lUGlja29mZhgFIAEoCzIbLnByb3RvY29scy5SdW5G",
            "cmFtZV9QaWNrb2ZmEj8KE2ZyYW1lQ2F0Y2hlclBpY2tvZmYYBiABKAsyIi5w",
            "cm90b2NvbHMuUnVuRnJhbWVfQ2F0Y2hlclBpY2tvZmYiwgEKFFJ1bkZyYW1l",
            "X0RlZmVuc2VNb3ZlEhEKCXN0YXJ0VGltZRgBIAEoAhIPCgdlbmRUaW1lGAIg",
            "ASgCEiYKBnBsYXllchgDIAEoDjIWLnByb3RvY29scy5PbkZpZWxkUm9sZRIs",
            "Cgx0YXJnZXRQYXNzZXIYBCABKA4yFi5wcm90b2NvbHMuT25GaWVsZFJvbGUS",
            "DgoGdG9CYXNlGAUgASgNEg8KB3RhcmdldFgYBiABKAISDwoHdGFyZ2V0WRgH",
            "IAEoAiK2AQoQUnVuRnJhbWVfUnVuQmFzZRIRCglzdGFydFRpbWUYASABKAIS",
            "DwoHZW5kVGltZRgCIAEoAhImCgZydW5uZXIYAyABKA4yFi5wcm90b2NvbHMu",
            "T25GaWVsZFJvbGUSEAoIZnJvbUJhc2UYBCABKA0SDgoGdG9CYXNlGAUgASgN",
            "Eg8KB291dFRpbWUYBiABKAISIwoHb3V0VHlwZRgHIAEoDjISLnByb3RvY29s",
            "cy5PdXRUeXBlIpQCCg5SdW5GcmFtZV9DYXRjaBIPCgdlbmRUaW1lGAEgASgC",
            "EiYKBnBhc3NlchgCIAEoDjIWLnByb3RvY29scy5PbkZpZWxkUm9sZRInCgdj",
            "YXRjaGVyGAMgASgOMhYucHJvdG9jb2xzLk9uRmllbGRSb2xlEg4KBnRvQmFz",
            "ZRgEIAEoDRIWCg5oaXRHcm91bmRUaW1lcxgFIAEoDRI1ChVvdXRBdGhsZXRl",
            "T25GaWVsZFJvbGUYBiABKA4yFi5wcm90b2NvbHMuT25GaWVsZFJvbGUSQQoW",
            "YWZ0ZXJDYXRjaEJlaGF2aW9yVHlwZRgHIAEoDjIhLnByb3RvY29scy5BZnRl",
            "ckNhdGNoQmVoYXZpb3JUeXBlIv8BChVSdW5GcmFtZV9IaXRCYWxsQ2F0Y2gS",
            "EQoJc3RhcnRUaW1lGAEgASgCEg8KB2VuZFRpbWUYAiABKAISJwoHY2F0Y2hl",
            "chgDIAEoDjIWLnByb3RvY29scy5PbkZpZWxkUm9sZRIPCgd0YXJnZXRYGAQg",
            "ASgCEg8KB3RhcmdldFkYBSABKAISFgoOaGl0R3JvdW5kVGltZXMYBiABKA0S",
            "FQoNaXNSb2xsaW5nQmFsbBgHIAEoCBI1ChVvdXRBdGhsZXRlT25GaWVsZFJv",
            "bGUYCCABKA4yFi5wcm90b2NvbHMuT25GaWVsZFJvbGUSEQoJaXNIaXRXYWxs",
            "GAkgASgIIi8KEFJ1bkZyYW1lX1BpY2tvZmYSDAoEYmFzZRgBIAEoDRINCgVp",
            "c091dBgCIAEoCCKrAQoXUnVuRnJhbWVfQ2F0Y2hlclBpY2tvZmYSDwoHZW5k",
            "VGltZRgBIAEoAhIPCgdvdXRUaW1lGAIgASgCEicKB2NhdGNoZXIYAyABKA4y",
            "Fi5wcm90b2NvbHMuT25GaWVsZFJvbGUSDgoGdG9CYXNlGAQgASgNEjUKFW91",
            "dEF0aGxldGVPbkZpZWxkUm9sZRgFIAEoDjIWLnByb3RvY29scy5PbkZpZWxk",
            "Um9sZSLZAwoJQmF0UmVzdWx0EiYKBnJlc3VsdBgBIAEoDjIWLnByb3RvY29s",
            "cy5QaXRjaFJlc3VsdBIjCgZmcmFtZXMYAiADKAsyEy5wcm90b2NvbHMuUnVu",
            "RnJhbWUSFAoMcGl0Y2hFbmRUaW1lGAMgASgCEhkKEW91dEZpZWxkUG9zaXRp",
            "b25YGAQgASgCEhkKEW91dEZpZWxkUG9zaXRpb25aGAUgASgCEhcKD291dEZp",
            "ZWxkRmx5VGltZRgGIAEoAhIhChlvdXRGaWVsZEZseUhpdEdyb3VuZFRpbWVz",
            "GAcgASgNEhEKCWlzRmFzdE91dBgIIAEoCBITCgtpc0F1dG9Td2luZxgJIAEo",
            "CBI5ChloaXRCYWxsQ2F0Y2hlck9uRmllbGRSb2xlGAogASgOMhYucHJvdG9j",
            "b2xzLk9uRmllbGRSb2xlEjEKDnRyYWplY3RvcnlUeXBlGAsgASgOMhkucHJv",
            "dG9jb2xzLlRyYWplY3RvcnlUeXBlEjIKEm11bHRpUGxheURlZmVuZGVycxgM",
            "IAMoDjIWLnByb3RvY29scy5PbkZpZWxkUm9sZRItCgxsaXZlbmVzc0luZm8Y",
            "DSABKAsyFy5wcm90b2NvbHMuTGl2ZW5lc3NJbmZvInAKEVBvc3NpYmxlQmF0",
            "UmVzdWx0EjUKCnJlc3VsdFR5cGUYASABKA4yIS5wcm90b2NvbHMuQmF0T3Bl",
            "cmF0aW9uUmVzdWx0VHlwZRIkCgZyZXN1bHQYAiABKAsyFC5wcm90b2NvbHMu",
            "QmF0UmVzdWx0Ik0KCVRlYW1TdGF0cxIMCgRydW5zGAEgASgNEgwKBGhpdHMY",
            "AiABKA0SDgoGZXJyb3JzGAMgASgNEhQKDGlubmluZ1Njb3JlcxgEIAMoDSKg",
            "AQoPQ2hpZWZNYXRjaFN0YXRzEhEKCXdpbm5lckNpZBgBIAEoCRIQCghsb3Nl",
            "ckNpZBgCIAEoCRIQCghzYXZlckNpZBgDIAEoCRIqCg9ob21lVGVhbUhySW5m",
            "b3MYBCADKAsyES5wcm90b2NvbHMuSHJJbmZvEioKD2F3YXlUZWFtSHJJbmZv",
            "cxgFIAMoCzIRLnByb3RvY29scy5IckluZm8iXgoGSHJJbmZvEgsKA2NpZBgB",
            "IAEoCRIOCgZpbm5pbmcYAiABKA0SKQoKaW5uaW5nSGFsZhgDIAEoDjIVLnBy",
            "b3RvY29scy5Jbm5pbmdIYWxmEgwKBHJ1bnMYBCABKA0idAoSTWF0Y2hBdGhs",
            "ZXRlc1N0YXRzEi4KDWhvbWVUZWFtU3RhdHMYASADKAsyFy5wcm90b2NvbHMu",
            "QXRobGV0ZVN0YXRzEi4KDWF3YXlUZWFtU3RhdHMYAiADKAsyFy5wcm90b2Nv",
            "bHMuQXRobGV0ZVN0YXRzIqABCgxBdGhsZXRlU3RhdHMSCwoDY2lkGAEgASgJ",
            "EisKC2NvbW1vblN0YXRzGAIgASgLMhYucHJvdG9jb2xzLkNvbW1vblN0YXRz",
            "EisKC2F0dGFja1N0YXRzGAMgASgLMhYucHJvdG9jb2xzLkF0dGFja1N0YXRz",
            "EikKCnBpdGNoU3RhdHMYBCABKAsyFS5wcm90b2NvbHMuUGl0Y2hTdGF0cyJ/",
            "CgtNYW51YWxTdGF0cxI3ChNob21lVGVhbU1hbnVhbFN0YXRzGAEgASgLMhou",
            "cHJvdG9jb2xzLlRlYW1NYW51YWxTdGF0cxI3ChNhd2F5VGVhbU1hbnVhbFN0",
            "YXRzGAIgASgLMhoucHJvdG9jb2xzLlRlYW1NYW51YWxTdGF0cyJmCg9UZWFt",
            "TWFudWFsU3RhdHMSDAoEaGl0cxgBIAEoDRILCgNocnMYAiABKA0SCgoCc28Y",
            "AyABKA0SCgoCc2IYBCABKA0SDgoGY2hlZXJzGAUgASgNEhAKCGxpdmVuZXNz",
            "GAYgASgNIhsKC0NvbW1vblN0YXRzEgwKBGdhbWUYASABKA0ipAIKC0F0dGFj",
            "a1N0YXRzEhgKEHBsYXRlQXBwZWFyYW5jZXMYASABKA0SDgoGYXRCYXRzGAIg",
            "ASgNEgwKBHJ1bnMYAyABKA0SDAoEaGl0cxgEIAEoDRISCgpkb3VibGVIaXRz",
            "GAUgASgNEhIKCnRyaXBsZUhpdHMYBiABKA0SEAoIaG9tZXJ1bnMYByABKA0S",
            "CwoDcmJpGAggASgNEgoKAnNiGAkgASgNEgoKAmNzGAogASgNEgsKA3NhYxgL",
            "IAEoDRIKCgJzZhgMIAEoDRIKCgJiYhgNIAEoDRILCgNoYnAYDiABKA0SCgoC",
            "c28YDyABKA0SCwoDYXZnGBAgASgCEgsKA3NsZxgRIAEoAhILCgNvYnAYEiAB",
            "KAISCwoDb3BzGBMgASgCIoACCgpQaXRjaFN0YXRzEgoKAmlwGAEgASgCEgkK",
            "AXAYAiABKA0SCgoCcGEYAyABKA0SCQoBaBgEIAEoDRIKCgJochgFIAEoDRIK",
            "CgJzbxgGIAEoDRIKCgJrORgHIAEoAhIKCgJiYhgIIAEoDRILCgNoYnAYCSAB",
            "KA0SCQoBchgKIAEoDRIKCgJlchgLIAEoDRILCgNhdmcYDCABKAISCwoDa2Ji",
            "GA0gASgCEgwKBHdoaXAYDiABKAISCQoBcxgPIAEoDRIJCgFiGBAgASgNEgsK",
            "A3dpbhgRIAEoDRIMCgRsb3NlGBIgASgNEgsKA2hsZBgTIAEoDRIKCgJzdhgU",
            "IAEoDSJTChZDaGFuZ2VkUGxheWVyQWJpbGl0aWVzEgoKAmlkGAEgASgNEi0K",
            "CWFiaWxpdGllcxgCIAEoCzIaLnByb3RvY29scy5QbGF5ZXJBYmlsaXRpZXMi",
            "agoNU2VsZWN0UGl0Y2hPcBIsCglzZWxlY3Rpb24YASABKAsyGS5wcm90b2Nv",
            "bHMuUGl0Y2hTZWxlY3Rpb24SKwoMcHJlU2VsVGFyZ2V0GAIgASgLMhUucHJv",
            "dG9jb2xzLlBvc1ZlY3RvcjIi4AEKD1NlbGVjdFBpdGNoUmVzcBIsCglzZWxl",
            "Y3Rpb24YASABKAsyGS5wcm90b2NvbHMuUGl0Y2hTZWxlY3Rpb24SNQoPcG9z",
            "c2libGVSZXN1bHRzGAIgAygLMhwucHJvdG9jb2xzLlBvc3NpYmxlQmF0UmVz",
            "dWx0EjsKEGNoYW5nZWRBYmlsaXRpZXMYAyADKAsyIS5wcm90b2NvbHMuQ2hh",
            "bmdlZFBsYXllckFiaWxpdGllcxIrCgxwcmVTZWxUYXJnZXQYBCABKAsyFS5w",
            "cm90b2NvbHMuUG9zVmVjdG9yMiJFCg9Eb21pbmF0ZVBpdGNoT3ASJAoCb3AY",
            "ASABKAsyGC5wcm90b2NvbHMuU2VsZWN0UGl0Y2hPcBIMCgRldmFsGAIgASgN",
            "IksKEURvbWluYXRlUGl0Y2hSZXNwEigKBHJlc3AYASABKAsyGi5wcm90b2Nv",
            "bHMuU2VsZWN0UGl0Y2hSZXNwEgwKBGV2YWwYAiABKA0iLAoFQmF0T3ASIwoD",
            "YmF0GAEgASgLMhYucHJvdG9jb2xzLkJhdHRpbmdJbmZvIo4BCgdCYXRSZXNw",
            "EiMKA2JhdBgBIAEoCzIWLnByb3RvY29scy5CYXR0aW5nSW5mbxIkCgZyZXN1",
            "bHQYAiABKAsyFC5wcm90b2NvbHMuQmF0UmVzdWx0EjgKDW5leHRTaXR1YXRp",
            "b24YAyABKAsyIS5wcm90b2NvbHMuRnVsbE1hdGNoU2l0dWF0aW9uTGl0ZSI7",
            "Cg1Eb21pbmF0ZUJhdE9wEhwKAm9wGAEgASgLMhAucHJvdG9jb2xzLkJhdE9w",
            "EgwKBGV2YWwYAiABKA0iSwoPRG9taW5hdGVCYXRSZXNwEjgKDW5leHRTaXR1",
            "YXRpb24YAyABKAsyIS5wcm90b2NvbHMuRnVsbE1hdGNoU2l0dWF0aW9uTGl0",
            "ZSJCCg1TZXREb21pbmF0ZU9wEg4KBmFjdGl2ZRgBIAEoCBIhCgRzaWRlGAIg",
            "ASgOMhMucHJvdG9jb2xzLlRlYW1TaWRlImAKEFNldERvbWluYXRlRXZlbnQS",
            "KQoGc3RhdHVzGAEgASgLMhkucHJvdG9jb2xzLkRvbWluYXRlU3RhdHVzEiEK",
            "BHNpZGUYAiABKA4yEy5wcm90b2NvbHMuVGVhbVNpZGUijAEKD1N0ZWFsQmFz",
            "ZVN0YXR1cxINCgViYXNlMRgBIAEoCBINCgViYXNlMhgCIAEoCBINCgViYXNl",
            "MxgDIAEoCBIYChBiYXNlMUFkdmFuY2VEaXN0GAQgASgCEhgKEGJhc2UyQWR2",
            "YW5jZURpc3QYBSABKAISGAoQYmFzZTNBZHZhbmNlRGlzdBgGIAEoAiI8Cg5T",
            "ZXRTdGVhbEJhc2VPcBIqCgZzdGF0dXMYASABKAsyGi5wcm90b2NvbHMuU3Rl",
            "YWxCYXNlU3RhdHVzIj8KEVNldFN0ZWFsQmFzZUV2ZW50EioKBnN0YXR1cxgB",
            "IAEoCzIaLnByb3RvY29scy5TdGVhbEJhc2VTdGF0dXMiOAoQU2V0QmF0dGlu",
            "Z01vZGVPcBIkCgRtb2RlGAEgASgOMhYucHJvdG9jb2xzLkJhdHRpbmdNb2Rl",
            "IjsKE1NldEJhdHRpbmdNb2RlRXZlbnQSJAoEbW9kZRgBIAEoDjIWLnByb3Rv",
            "Y29scy5CYXR0aW5nTW9kZSIgChBNb3ZlVG9OZXh0U3RlcE9wEgwKBHN0ZXAY",
            "ASABKA0iIgoSTW92ZVRvTmV4dFN0ZXBSZXNwEgwKBHN0ZXAYASABKA0iEAoO",
            "UGl0Y2hQcmVwYXJlT3AiEAoOUGl0Y2hSZWFkeVJlc3AiCwoJQmF0RG9uZU9w",
            "IhMKEVBpdGNoU3RhcnRlZEV2ZW50Ig0KC1ZhaW5Td2luZ09wIhAKDlZhaW5T",
            "d2luZ0V2ZW50IhIKEFBpdGNoZXJVcmdlRXZlbnQiEQoPQmVnaW5CYXRTd2lu",
            "Z09wIhQKEkJlZ2luQmF0U3dpbmdFdmVudCIzChtTZWxlY3RpbmdCYXR0aW5n",
            "VGFyZ2V0RXZlbnQSCQoBeBgBIAEoAhIJCgF5GAIgASgCIkQKEUNoYW5nZVBs",
            "YXllckV2ZW50Ei8KB3BsYXllcnMYASABKAsyHi5wcm90b2NvbHMuUGxheWVy",
            "U2l0dWF0aW9uTGl0ZSJRChlVcGRhdGVNYXRjaFNpdHVhdGlvbkV2ZW50EjQK",
            "CXNpdHVhdGlvbhgBIAEoCzIhLnByb3RvY29scy5GdWxsTWF0Y2hTaXR1YXRp",
            "b25MaXRlIkEKDkNhc3RTa2lsbEV2ZW50Ei8KB3BsYXllcnMYASABKAsyHi5w",
            "cm90b2NvbHMuUGxheWVyU2l0dWF0aW9uTGl0ZSI/Cg5GcmFtZVN5bmNCZWdp",
            "bhIQCghpbnRlcnZhbBgBIAEoDRINCgVpbmRleBgCIAEoDRIMCgR0aW1lGAMg",
            "ASgNIj4KDUZyYW1lU3luY1RpY2sSEAoIaW50ZXJ2YWwYASABKA0SDQoFaW5k",
            "ZXgYAiABKA0SDAoEdGltZRgDIAEoDSIOCgxGcmFtZVN5bmNFbmQiTQoMUnVu",
            "VG9CYXNlUmVxEg4KBnRvYmFzZRgBIAEoDRIPCgdjdXJiYXNlGAIgASgNEgwK",
            "BHRpbWUYAyABKA0SDgoGb2Zmc2V0GAQgASgCIm8KDVJ1blRvQmFzZVJlc3AS",
            "JAoGcmVzdWx0GAEgASgLMhQucHJvdG9jb2xzLkJhdFJlc3VsdBI4Cg1uZXh0",
            "U2l0dWF0aW9uGAIgASgLMiEucHJvdG9jb2xzLkZ1bGxNYXRjaFNpdHVhdGlv",
            "bkxpdGUiOgoPU3RhdGljU2tpbGxJbmZvEgoKAmlkGAEgASgJEgwKBG5hbWUY",
            "AiABKAkSDQoFYWxpYXMYAyABKAkiOgoMU3RhdGljU2tpbGxzEioKBnNraWxs",
            "cxgBIAMoCzIaLnByb3RvY29scy5TdGF0aWNTa2lsbEluZm8iLwoSU2VjcmV0",
            "YXJ5U2tpbGxJbmZvEgoKAmlkGAEgASgJEg0KBWxldmVsGAIgASgNKisKCFRl",
            "YW1TaWRlEgsKB05ldXRyYWwQABIICgRIb21lEAESCAoEQXdheRACKjIKCklu",
            "bmluZ0hhbGYSDwoLVW5rbm93bkhhbGYQABIHCgNUb3AQARIKCgZCb3R0b20Q",
            "AipDCgtCYXR0aW5nTW9kZRIPCgtVbmtub3duTW9kZRAAEgsKB0NvbnRhY3QQ",
            "ARIMCghTbHVnZ2luZxACEggKBEJ1bnQQAypoCg5UcmFqZWN0b3J5VHlwZRIZ",
            "ChVVbmtub3duVHJhamVjdG9yeVR5cGUQABIRCg1Mb3dUcmFqZWN0b3J5EAES",
            "FAoQTWlkZGxlVHJhamVjdG9yeRACEhIKDkhpZ2hUcmFqZWN0b3J5EAMq8gEK",
            "CVBpdGNoVHlwZRIUChBVbmtub3duUGl0Y2hUeXBlEAASDAoIRmFzdEJhbGwQ",
            "ARIKCgZTaW5rZXIQAhIKCgZTbGlkZXIQAxIJCgVDdXJ2ZRAEEg0KCVNjcmV3",
            "QmFsbBAFEgwKCENoYW5nZVVwEAYSCgoGQ3V0dGVyEAcSCwoHVHdvU2VhbRAI",
            "EgcKA1NmZhAJEhAKDEtudWNrbGVDdXJ2ZRAKEgwKCEZvcmtCYWxsEAsSCgoG",
            "U2x1cnZlEAwSDAoIUGFsbUJhbGwQDRIJCgVTaG9vdBAOEgsKB1ZzbGlkZXIQ",
            "DxINCglTbG93Q3VydmUQECpOCg5QaXRjaFR5cGVHcmFkZRIZChVVbmtub3du",
            "UGl0Y2hUeXBlR3JhZGUQABIFCgFEEAESBQoBQxACEgUKAUIQAxIFCgFBEAQS",
            "BQoBUxAFKoIBChVNYW51YWxQaXRjaFB1enpsZVR5cGUSFQoRVW5rbm93blB1",
            "enpsZVR5cGUQABIQCgxQdXp6bGVTdHJpa2UQARIOCgpQdXp6bGVCYWxsEAIS",
            "GAoUUHV6emxlU3RyaWtlU3RyZW5ndGgQAxIWChJQdXp6bGVCYWxsU3RyZW5n",
            "dGgQBCq4AQoLUGl0Y2hSZXN1bHQSEQoNVW5rbm93blJlc3VsdBAAEggKBEJh",
            "bGwQARIKCgZTdHJpa2UQAhIICgRGb3VsEAMSCgoGU2luZ2xlEAQSCgoGRG91",
            "YmxlEAUSCgoGVHJpcGxlEAYSCwoHSG9tZVJ1bhAHEgsKB1BpY2tvZmYQCBIH",
            "CgNJQkIQCRIKCgZQdXRPdXQQChIMCghGb3JjZU91dBALEgwKCFRvdWNoT3V0",
            "EAwSBwoDSEJQEA0qMwoGQmF0RGlyEggKBE5vbmUQABIICgRMZWZ0EAESCgoG",
            "Q2VudGVyEAISCQoFUmlnaHQQAyqIAQoMUnVuRnJhbWVUeXBlEhcKE1Vua25v",
            "d25SdW5GcmFtZVR5cGUQABIPCgtEZWZlbnNlTW92ZRABEgsKB1J1bkJhc2UQ",
            "AhIJCgVDYXRjaBADEhAKDEhpdEJhbGxDYXRjaBAEEhAKDFBpY2tvZmZGcmFt",
            "ZRAFEhIKDkNhdGNoZXJQaWNrb2ZmEAYq6wEKBFJvbGUSDwoLVW5rbm93blJv",
            "bGUQABIPCgtQaXRjaGVyUm9sZRABEg8KC0NhdGNoZXJSb2xlEAISFAoQRmly",
            "c3RCYXNlTWFuUm9sZRADEhUKEVNlY29uZEJhc2VNYW5Sb2xlEAQSFAoQVGhp",
            "cmRCYXNlTWFuUm9sZRAFEhEKDVNob3J0c3RvcFJvbGUQBhITCg9MZWZ0Rmll",
            "bGRlclJvbGUQBxIVChFDZW50ZXJGaWVsZGVyUm9sZRAIEhQKEFJpZ2h0Rmll",
            "bGRlclJvbGUQCRIYChREZXNpZ25hdGVkSGl0dGVyUm9sZRAKKocCCgtPbkZp",
            "ZWxkUm9sZRIWChJVbmtub3duT25GaWVsZFJvbGUQABILCgdQaXRjaGVyEAES",
            "CwoHQ2F0Y2hlchACEhAKDEZpcnN0QmFzZU1hbhADEhEKDVNlY29uZEJhc2VN",
            "YW4QBBIQCgxUaGlyZEJhc2VNYW4QBRINCglTaG9ydHN0b3AQBhIPCgtMZWZ0",
            "RmllbGRlchAHEhEKDUNlbnRlckZpZWxkZXIQCBIQCgxSaWdodEZpZWxkZXIQ",
            "CRIKCgZCYXR0ZXIQChITCg9GaXJzdEJhc2VSdW5uZXIQCxIUChBTZWNvbmRC",
            "YXNlUnVubmVyEAwSEwoPVGhpcmRCYXNlUnVubmVyEA0qRgoISGFuZFR5cGUS",
            "EwoPVW5rbm93bkhhbmRUeXBlEAASDAoITGVmdEhhbmQQARINCglSaWdodEhh",
            "bmQQAhIICgRCb3RoEAMqlgIKDlBsYXllclBvc2l0aW9uEhkKFVVua25vd25Q",
            "bGF5ZXJQb3NpdGlvbhAAEhUKEVN0YXJ0aW5nUGl0Y2hlclBQEAESEwoPUmVs",
            "aWVmUGl0Y2hlclBQEAISDAoIQ2xvc2VyUFAQAxINCglDYXRjaGVyUFAQBBIS",
            "Cg5GaXJzdEJhc2VNYW5QUBAFEhMKD1NlY29uZEJhc2VNYW5QUBAGEhIKDlRo",
            "aXJkQmFzZU1hblBQEAcSDwoLU2hvcnRzdG9wUFAQCBIRCg1MZWZ0RmllbGRl",
            "clBQEAkSEwoPQ2VudGVyRmllbGRlclBQEAoSEgoOUmlnaHRGaWVsZGVyUFAQ",
            "CxIWChJEZXNpZ25hdGVkSGl0dGVyUFAQDCqkAQoWQmF0T3BlcmF0aW9uUmVz",
            "dWx0VHlwZRIhCh1Vbmtub3duQmF0T3BlcmF0aW9uUmVzdWx0VHlwZRAAEggK",
            "BEF1dG8QARIKCgZOb3RCYXQQAhIJCgVFYXJseRADEg0KCUZvdWxFYXJseRAE",
            "EggKBFB1bGwQBRILCgdGb3J3YXJkEAYSCAoEUHVzaBAHEgwKCEZvdWxMYXRl",
            "EAgSCAoETGF0ZRAJKlMKCUJhdE9wVHlwZRIUChBVbmtub3duQmF0T3BUeXBl",
            "EAASCwoHRGVmYXVsdBABEhEKDUd1ZXNzUGl0Y2hQb3MQAhIQCgxTZWxlY3RI",
            "aXRQb3MQAypRCgdPdXRUeXBlEhIKDlVua25vd25PdXRUeXBlEAASDgoKUHV0",
            "T3V0VHlwZRABEhAKDEZvcmNlT3V0VHlwZRACEhAKDFRvdWNoT3V0VHlwZRAD",
            "KlsKFkFmdGVyQ2F0Y2hCZWhhdmlvclR5cGUSIQodVW5rbm93bkFmdGVyQ2F0",
            "Y2hCZWhhdmlvclR5cGUQABIICgRQYXNzEAESCQoFVG91Y2gQAhIJCgVSZWxh",
            "eBADKpQBChRTa2lsbENhc3RpbmdUaW1lVHlwZRIfChtVbmtub3duU2tpbGxD",
            "YXN0aW5nVGltZVR5cGUQABIPCgtCZWZvcmVQaXRjaBABEhQKEE9uUGl0Y2hC",
            "YWxsTGVhdmUQAhIXChNPblN3aW5nRGVjaXNpb25NYWRlEAMSDwoLT25DYXRj",
            "aEJhbGwQBBIKCgZPblBhc3MQBSr3BAoMRG9taW5hdGVUeXBlEhMKD1Vua25v",
            "d25Eb21pbmF0ZRAAEhMKD0JhdHRlckRvbWluYXRlMRABEhUKEVNsb3dNb3Rp",
            "b25CYXR0aW5nEAESEwoPQmF0dGVyRG9taW5hdGUyEAISDgoKQmF0dGluZ0V5",
            "ZRACEhMKD0JhdHRlckRvbWluYXRlMxADEhYKEkhpdFRhcmdldFNlbGVjdGlv",
            "bhADEhMKD0JhdHRlckRvbWluYXRlNBAEEhMKD0JhdHRlckRvbWluYXRlNRAF",
            "EhMKD0JhdHRlckRvbWluYXRlNhAGEhMKD0JhdHRlckRvbWluYXRlNxAHEhMK",
            "D0JhdHRlckRvbWluYXRlOBAIEhMKD0JhdHRlckRvbWluYXRlORAJEhQKEEJh",
            "dHRlckRvbWluYXRlMTAQChIUChBQaXRjaGVyRG9taW5hdGUxEAsSEwoPTGln",
            "aHRTcGVlZFBpdGNoEAsSFAoQUGl0Y2hlckRvbWluYXRlMhAMEg4KClN1cGVy",
            "Q3VydmUQDBIUChBQaXRjaGVyRG9taW5hdGUzEA0SFQoRVW5kZXJDb250cm9s",
            "UGl0Y2gQDRIUChBQaXRjaGVyRG9taW5hdGU0EA4SEAoMU2xpZGVyTWFzdGVy",
            "EA4SFAoQUGl0Y2hlckRvbWluYXRlNRAPEhEKDVR3b1NlYW1NYXN0ZXIQDxIU",
            "ChBQaXRjaGVyRG9taW5hdGU2EBASFAoQUGl0Y2hlckRvbWluYXRlNxAREhQK",
            "EFBpdGNoZXJEb21pbmF0ZTgQEhIUChBQaXRjaGVyRG9taW5hdGU5EBMSFQoR",
            "UGl0Y2hlckRvbWluYXRlMTAQFBoCEAFiBnByb3RvMw=="));
        #endregion
    }
    #endregion
#endif
}