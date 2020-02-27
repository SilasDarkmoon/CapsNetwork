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

    public struct ProtobufValue
    {
        public object Parsed;
        public ListSegment<byte> RawData;

        public ProtobufValue(object parsed)
        {
            Parsed = parsed;
            RawData = default(ListSegment<byte>);
        }

        public bool IsValid
        {
            get
            {
                return Parsed != null || RawData.List != null;
            }
        }

        public override string ToString()
        {
            if (Parsed != null)
            {
                return Parsed.ToString();
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
        public byte[] Raw;
        public override string ToString()
        {
            return string.Format("*Unknown[{0}]*", Raw == null ? 0 : Raw.Length);
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
    public enum ProtobufNativeType
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
    };
    public struct ProtobufHighLevelType
    {
        public ProtobufNativeType KnownType;
        public string MessageName; // when it is a dynamic message.
    }
    public struct ProtobufFieldDesc
    {
        public int Number;
        public string Name;
        public ProtobufHighLevelType Type;
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
            else if (value.Parsed == null)
            {
                sb.Append("\"*RawData(");
                sb.Append(value.RawData.Count);
                sb.Append(")*\"");
            }
            else
            {
                var val = value.Parsed;
                if (val is ProtobufMessage)
                {
                    if (indent >= 0)
                    {
                        sb.AppendLine();
                    }
                    var message = (ProtobufMessage)val;
                    if (alreadyHandledNodes == null || alreadyHandledNodes.Add(message))
                    {
                        message.ToJson(sb, indent, alreadyHandledNodes);
                    }
                    else
                    {
                        sb.Append("\"*Ref*\"");
                    }
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
        public void ToJson(System.Text.StringBuilder sb, int indent, HashSet<ProtobufMessage> alreadyHandledNodes)
        {
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
        }
        public string ToJson(int indent, HashSet<ProtobufMessage> alreadyHandledNodes)
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
        public string ToJson(int indent)
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
        protected bool _BuildFinished;
        public void FinishBuild()
        {
            if (_BuildFinished)
            {
                return;
            }
            _BuildFinished = true;
            _FieldMap.Clear();
            foreach (var slot in Slots)
            {
                for (int i = 0; i < slot.Values.Count; ++i)
                {
                    var val = slot.Values[i];
                    if (val.Parsed == null)
                    {
                        val.Parsed = new ProtobufUnknowValue() { Raw = val.RawData.ToArray() };
                    }
                    val.RawData = default(ListSegment<byte>);
                    slot.Values[i] = val;
                    if (val.Parsed is ProtobufMessage)
                    {
                        ((ProtobufMessage)val.Parsed).FinishBuild();
                    }
                }
                var name = slot.Desc.Name;
                if (name != null)
                {
                    _FieldMap[name] = slot;
                }
            }
        }

        public T GetValue<T>(string fieldName)
        {
            FieldSlot slot;
            if (_FieldMap.TryGetValue(fieldName, out slot))
            {
                var val = slot.FirstValue.Parsed;
                if (val is T)
                {
                    return (T)val;
                }
            }
            return default(T);
        }
        public void GetValues<T>(string fieldName, IList<T> list)
        {
            FieldSlot slot;
            if (_FieldMap.TryGetValue(fieldName, out slot))
            {
                for (int i = 0; i < slot.Values.Count; ++i)
                {
                    var val = slot.Values[i].Parsed;
                    if (val is T)
                    {
                        list.Add((T)val);
                    }
                    //else
                    //{
                    //    list.Add(default(T));
                    //}
                }
            }
        }
        public List<T> GetValues<T>(string fieldName)
        {
            List<T> rv = new List<T>();
            GetValues(fieldName, rv);
            return rv;
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
            slot.FirstValue = new ProtobufValue() { Parsed = subtemplate };
        }
        public void Add<T>(int fieldno, string fieldname, T templateValue) // currently only used for enums
        {
            var slot = GetOrCreateSlot(fieldno);
            if (typeof(T).IsSubclassOf(typeof(Enum)))
            {
                slot.Desc.Type.KnownType = ProtobufNativeType.TYPE_ENUM;
            }
            slot.Desc.Name = fieldname;
            slot.FirstValue = new ProtobufValue() { Parsed = templateValue };
        }
        internal void Add<T>(int fieldno, string fieldname, ProtobufNativeType knownType, T templateValue) // currently only used for enums
        {
            var slot = GetOrCreateSlot(fieldno);
            slot.Desc.Name = fieldname;
            slot.Desc.Type.KnownType = knownType;
            slot.FirstValue = new ProtobufValue() { Parsed = templateValue };
        }

        public IEnumerator GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }

    public static class ProtobufMessageReader
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
        public static bool ReadTag(ListSegment<byte> data, out int number, out ProtobufLowLevelType ltype, out int readbytecount)
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
        private delegate bool DecodeFuncForNativeType(ProtobufValue raw, out object value);
        private static Dictionary<ProtobufNativeType, DecodeFuncForNativeType> _DecodeForNativeTypeFuncs = new Dictionary<ProtobufNativeType, DecodeFuncForNativeType>()
        {
            { ProtobufNativeType.TYPE_BYTES, 
                (ProtobufValue raw, out object value) =>
                {
                    var buffer = raw.RawData.ToArray();
                    value = buffer;
                    return true;
                }
            },
            { ProtobufNativeType.TYPE_STRING, 
                (ProtobufValue raw, out object value) =>
                {
                    var buffer = raw.RawData.ToArray();
                    try
                    {
                        value = System.Text.Encoding.UTF8.GetString(buffer);
                        return true;
                    }
                    catch
                    {
                        value = null;
                        return false;
                    }
                }
            },
            { ProtobufNativeType.TYPE_BOOL,
                (ProtobufValue raw, out object value) =>
                {
                    if (raw.Parsed is uint)
                    {
                        value = ((uint)raw.Parsed) != 0;
                        return true;
                    }
                    else if (raw.Parsed is ulong)
                    {
                        value = ((ulong)raw.Parsed) != 0;
                        return true;
                    }
                    value = null;
                    return false;
                }
            },
            { ProtobufNativeType.TYPE_ENUM,
                (ProtobufValue raw, out object value) =>
                {
                    if (raw.Parsed is uint || raw.Parsed is ulong)
                    {
                        value = raw.Parsed;
                        return true;
                    }
                    value = null;
                    return false;
                }
            },
            { ProtobufNativeType.TYPE_DOUBLE,
                (ProtobufValue raw, out object value) =>
                {
                    if (raw.Parsed is ulong || raw.Parsed is uint)
                    {
                        ulong r;
                        if (raw.Parsed is ulong)
                        {
                            r = (ulong)raw.Parsed;
                        }
                        else
                        {
                            r = (ulong)(uint)raw.Parsed;
                        }
                        value = BitConverter.Int64BitsToDouble((long)r);
                        return true;
                    }
                    value = null;
                    return false;
                }
            },
            { ProtobufNativeType.TYPE_FLOAT,
                (ProtobufValue raw, out object value) =>
                {
                    if (raw.Parsed is ulong || raw.Parsed is uint)
                    {
                        uint r;
                        if (raw.Parsed is ulong)
                        {
                            r = (uint)(ulong)raw.Parsed;
                        }
                        else
                        {
                            r = (uint)raw.Parsed;
                        }
                        value = BitConverter.ToSingle(BitConverter.GetBytes(r), 0);
                        return true;
                    }
                    value = null;
                    return false;
                }
            },
            { ProtobufNativeType.TYPE_INT64,
                (ProtobufValue raw, out object value) =>
                {
                    if (raw.Parsed is ulong || raw.Parsed is uint)
                    {
                        ulong r;
                        if (raw.Parsed is ulong)
                        {
                            r = (ulong)raw.Parsed;
                        }
                        else
                        {
                            r = (ulong)(uint)raw.Parsed;
                        }
                        value = (long)r;
                        return true;
                    }
                    value = null;
                    return false;
                }
            },
            { ProtobufNativeType.TYPE_UINT64,
                (ProtobufValue raw, out object value) =>
                {
                    if (raw.Parsed is ulong || raw.Parsed is uint)
                    {
                        ulong r;
                        if (raw.Parsed is ulong)
                        {
                            r = (ulong)raw.Parsed;
                        }
                        else
                        {
                            r = (ulong)(uint)raw.Parsed;
                        }
                        value = r;
                        return true;
                    }
                    value = null;
                    return false;
                }
            },
            { ProtobufNativeType.TYPE_SFIXED64,
                (ProtobufValue raw, out object value) =>
                {
                    if (raw.Parsed is ulong || raw.Parsed is uint)
                    {
                        ulong r;
                        if (raw.Parsed is ulong)
                        {
                            r = (ulong)raw.Parsed;
                        }
                        else
                        {
                            r = (ulong)(uint)raw.Parsed;
                        }
                        value = (long)r;
                        return true;
                    }
                    value = null;
                    return false;
                }
            },
            { ProtobufNativeType.TYPE_FIXED64,
                (ProtobufValue raw, out object value) =>
                {
                    if (raw.Parsed is ulong || raw.Parsed is uint)
                    {
                        ulong r;
                        if (raw.Parsed is ulong)
                        {
                            r = (ulong)raw.Parsed;
                        }
                        else
                        {
                            r = (ulong)(uint)raw.Parsed;
                        }
                        value = r;
                        return true;
                    }
                    value = null;
                    return false;
                }
            },
            { ProtobufNativeType.TYPE_INT32,
                (ProtobufValue raw, out object value) =>
                {
                    if (raw.Parsed is ulong || raw.Parsed is uint)
                    {
                        uint r;
                        if (raw.Parsed is ulong)
                        {
                            r = (uint)(ulong)raw.Parsed;
                        }
                        else
                        {
                            r = (uint)raw.Parsed;
                        }
                        value = (int)r;
                        return true;
                    }
                    value = null;
                    return false;
                }
            },
            { ProtobufNativeType.TYPE_UINT32,
                (ProtobufValue raw, out object value) =>
                {
                    if (raw.Parsed is ulong || raw.Parsed is uint)
                    {
                        uint r;
                        if (raw.Parsed is ulong)
                        {
                            r = (uint)(ulong)raw.Parsed;
                        }
                        else
                        {
                            r = (uint)raw.Parsed;
                        }
                        value = r;
                        return true;
                    }
                    value = null;
                    return false;
                }
            },
            { ProtobufNativeType.TYPE_SFIXED32,
                (ProtobufValue raw, out object value) =>
                {
                    if (raw.Parsed is ulong || raw.Parsed is uint)
                    {
                        uint r;
                        if (raw.Parsed is ulong)
                        {
                            r = (uint)(ulong)raw.Parsed;
                        }
                        else
                        {
                            r = (uint)raw.Parsed;
                        }
                        value = (int)r;
                        return true;
                    }
                    value = null;
                    return false;
                }
            },
            { ProtobufNativeType.TYPE_FIXED32,
                (ProtobufValue raw, out object value) =>
                {
                    if (raw.Parsed is ulong || raw.Parsed is uint)
                    {
                        uint r;
                        if (raw.Parsed is ulong)
                        {
                            r = (uint)(ulong)raw.Parsed;
                        }
                        else
                        {
                            r = (uint)raw.Parsed;
                        }
                        value = r;
                        return true;
                    }
                    value = null;
                    return false;
                }
            },
            { ProtobufNativeType.TYPE_SINT64,
                (ProtobufValue raw, out object value) =>
                {
                    if (raw.Parsed is ulong || raw.Parsed is uint)
                    {
                        ulong r;
                        if (raw.Parsed is ulong)
                        {
                            r = (ulong)raw.Parsed;
                        }
                        else
                        {
                            r = (ulong)(uint)raw.Parsed;
                        }
                        value = (long)(r >> 1) ^ -(long)(r & 1);
                        return true;
                    }
                    value = null;
                    return false;
                }
            },
            { ProtobufNativeType.TYPE_SINT32,
                (ProtobufValue raw, out object value) =>
                {
                    if (raw.Parsed is ulong || raw.Parsed is uint)
                    {
                        uint r;
                        if (raw.Parsed is ulong)
                        {
                            r = (uint)(ulong)raw.Parsed;
                        }
                        else
                        {
                            r = (uint)raw.Parsed;
                        }
                        value = (int)(r >> 1) ^ -(int)(r & 1);
                        return true;
                    }
                    value = null;
                    return false;
                }
            },
        };
        public static bool Decode(ProtobufValue raw, ProtobufNativeType knownType, out object value)
        {
            value = null;
            if (raw.RawData.List == null)
            {
                return false;
            }
            DecodeFuncForNativeType decodeFunc;
            if (_DecodeForNativeTypeFuncs.TryGetValue(knownType, out decodeFunc))
            {
                return decodeFunc(raw, out value);
            }
            return false;
        }
        private static void ApplyTemplate(ProtobufMessage.FieldSlot rslot, ProtobufMessage.FieldSlot tslot)
        {
            if (rslot != null && tslot != null)
            {
                rslot.Desc.Name = tslot.Desc.Name;
                if (rslot.Values.Count > 0)
                {
                    if (tslot.Desc.Type.KnownType != 0 && tslot.Desc.Type.KnownType != ProtobufNativeType.TYPE_GROUP && tslot.Desc.Type.KnownType != ProtobufNativeType.TYPE_MESSAGE)
                    {
                        var knownType = tslot.Desc.Type.KnownType;
                        for (int j = 0; j < rslot.Values.Count; ++j)
                        {
                            var subraw = rslot.Values[j];
                            object val;
                            if (Decode(subraw, knownType, out val))
                            {
                                if (knownType == ProtobufNativeType.TYPE_ENUM && tslot.FirstValue.Parsed is Enum)
                                {
                                    var etype = tslot.FirstValue.Parsed.GetType();
                                    val = Enum.ToObject(etype, val);
                                }
                                subraw.Parsed = val;
                                rslot.Values[j] = subraw;
                            }
                        }
                    }
                    else if (tslot.FirstValue.IsValid)
                    {
                        var subtemplate = tslot.FirstValue.Parsed as ProtobufMessage;
                        if (subtemplate != null)
                        {
                            for (int j = 0; j < rslot.Values.Count; ++j)
                            {
                                var subraw = rslot.Values[j];
                                var submess = subraw.Parsed as ProtobufMessage;
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
            foreach (var rawslotkvp in raw._HighFields)
            {
                var rslot = rawslotkvp.Value;
                var tslot = template.GetSlot(rslot.Desc.Number);
                ApplyTemplate(rslot, tslot);
            }
            raw.FinishBuild();
            return raw;
        }

        //public static Dictionary<string, ProtobufMessage> ReadTemplates(string content)
        //{

        //}
    }

    public static class ProtobufMessagePool
    {
        public readonly static TemplateProtobufMessage FieldOptionsTemplate = new TemplateProtobufMessage()
        {
            //optional CType ctype = 1 [default = STRING];
            //optional bool packed = 2;
            //optional JSType jstype = 6 [default = JS_NORMAL];
            //optional bool lazy = 5 [default=false];
            //optional bool deprecated = 3 [default=false];
            //optional bool weak = 10 [default=false];
            //repeated UninterpretedOption uninterpreted_option = 999;
        };
        public readonly static TemplateProtobufMessage EnumValueDescriptorTemplate = new TemplateProtobufMessage()
        {
            { 1, "name", ProtobufNativeType.TYPE_STRING },
            { 2, "number", ProtobufNativeType.TYPE_INT32 },
            //optional EnumValueOptions options = 3;
        };
        public readonly static TemplateProtobufMessage EnumDescriptorTemplate = new TemplateProtobufMessage()
        {
            { 1, "name", ProtobufNativeType.TYPE_STRING },
            //repeated EnumValueDescriptorProto value = 2;
            //optional EnumOptions options = 3;
            //repeated EnumReservedRange reserved_range = 4;
            //repeated string reserved_name = 5;
        };
        public enum FieldDescriptor_Label
        {
            LABEL_OPTIONAL = 1,
            LABEL_REQUIRED = 2,
            LABEL_REPEATED = 3,
        }
        public readonly static TemplateProtobufMessage FieldDescriptorTemplate = new TemplateProtobufMessage()
        {
            { 1, "name", ProtobufNativeType.TYPE_STRING },
            { 3, "number", ProtobufNativeType.TYPE_INT32 },
            { 4, "label", default(FieldDescriptor_Label) },
            { 5, "type", ProtobufNativeType.TYPE_ENUM, default(ProtobufNativeType) },
            { 6, "type_name", ProtobufNativeType.TYPE_STRING },
            { 2, "extendee", ProtobufNativeType.TYPE_STRING },
            { 7, "default_value", ProtobufNativeType.TYPE_STRING },
            { 9, "oneof_index", ProtobufNativeType.TYPE_INT32 },
            { 10, "json_name", ProtobufNativeType.TYPE_STRING },
            { 8, "options", FieldOptionsTemplate },
        };
        public readonly static TemplateProtobufMessage MessageDescriptorTemplate = new TemplateProtobufMessage()
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
        public readonly static TemplateProtobufMessage ServiceDescriptorTemplate = new TemplateProtobufMessage()
        {
            { 1, "name", typeof(string) },
            //repeated MethodDescriptorProto method = 2;
            //optional ServiceOptions options = 3;
        };
        public readonly static TemplateProtobufMessage DescriptorFileTemplate = new TemplateProtobufMessage()
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
            var myname = parent.GetValue<string>("name");
            var myfullname = pre + myname;
            var childpre = myfullname + ".";
            messages[myfullname] = parent;
            var subs = parent.GetValues<ProtobufMessage>("nested_type");
            for (int i = 0; i < subs.Count; ++i)
            {
                var sub = subs[i];
                GetMessages(sub, childpre, messages);
            }
        }
        public static Dictionary<string, TemplateProtobufMessage> ReadTemplates(ListSegment<byte> compiledFileData)
        {
            ProtobufMessage file = ProtobufMessageReader.ReadRaw(compiledFileData);
            if (file != null)
            {
                ProtobufMessageReader.ApplyTemplate(file, DescriptorFileTemplate);
                Dictionary<string, TemplateProtobufMessage> templates = new Dictionary<string, TemplateProtobufMessage>();
                var package = file.GetValue<string>("package");
                Dictionary<string, ProtobufMessage> allmessages = new Dictionary<string, ProtobufMessage>();
                var messages = file.GetValues<ProtobufMessage>("message_type");
                var rootpre = package + ".";
                for (int i = 0; i < messages.Count; ++i)
                {
                    var message = messages[i];
                    GetMessages(message, rootpre, allmessages);
                }
                foreach (var kvp in allmessages)
                {
                    templates[kvp.Key] = new TemplateProtobufMessage();
                }
                foreach (var kvp in allmessages)
                {
                    var message = kvp.Value;
                    var template = templates[kvp.Key];
                    var fields = message.GetValues<ProtobufMessage>("field");
                    for (int i = 0; i < fields.Count; ++i)
                    {
                        var field = fields[i];
                        var name = field.GetValue<string>("name");
                        var num = field.GetValue<int>("number");
                        var ntype = field.GetValue<ProtobufNativeType>("type");
                        var mtype = field.GetValue<string>("type_name");
                        if (num > 0 && !string.IsNullOrEmpty(name))
                        {
                            var slot = template.GetOrCreateSlot(num);
                            slot.Desc.Name = name;
                            slot.Desc.Type.KnownType = ntype;
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
            UnityEngine.Debug.Log(ProtobufMessagePool.MessageDescriptorTemplate.ToJson(0, null));

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

            ProtobufMessageReader.ApplyTemplate(message, tmessage);

            UnityEngine.Debug.Log(message.ToString());
        }

        [UnityEditor.MenuItem("Test/Dynamic Protobuf Message/Test Decode", priority = 100020)]
        public static void TestDecode()
        {

            var templates = ProtobufMessagePool.ReadTemplates(new ListSegment<byte>(TestDescriptorFileData));
            foreach (var kvp in templates)
            {
                UnityEngine.Debug.Log(kvp.Key);
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