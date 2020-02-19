using System.Collections.Generic;
using Capstones.UnityEngineEx;

namespace Capstones.Net
{
    public class ProtobufMessage
    {
        public enum LowLevelValueType
        {
            Varint = 0,
            Fixed64 = 1,
            LengthDelimited = 2,
            Fixed32 = 5,
        }
        public enum HighLevelValueType
        {

        }
        public struct FieldDesc
        {
            public int Seq;
            public string Name;
            public HighLevelValueType Type;
        }
        public struct FieldSlot
        {
            public FieldDesc Desc;
            public ValueList<ProtobufValue> Value;
        }

        protected internal FieldSlot[] _LowFields = new FieldSlot[16];
        protected internal Dictionary<int, FieldSlot> _HighFields = new Dictionary<int, FieldSlot>();
        public ProtobufMessage()
        {
            for (int i = 0; i < 16; ++i)
            {
                _LowFields[i].Desc.Seq = i + 1;
            }
        }

        public class ProtobufValue
        {
            public RawType Type;
            public ulong Number;
            public ProtobufMessage Message;
            public byte[] RawData;

            public object Value
            {
                get
                {
                    if (Type == RawType.LengthDelimited)
                    {
                        if (Message != null)
                        {
                            return Message;
                        }
                        else
                        {
                            return RawData;
                        }
                    }
                    else
                    {
                        return Number;
                    }
                }
                set
                {
                    if (value is ProtobufMessage)
                    {
                        Type = RawType.LengthDelimited;
                        Message = value as ProtobufMessage;
                        RawData = null;
                        Number = 0;
                    }
                    else if (value is byte[])
                    {

                    }
                    else
                    {
                        Message = null;
                        if (Type == RawType.LengthDelimited)
                        {
                            Type = RawType.Varint;
                        }
                        if (value is byte)
                        {

                        }
                    }
                }
            }
        }
        public enum RawType
        {
            Varint = 0,
            Fixed64 = 1,
            LengthDelimited = 2,
            Fixed32 = 5,
        }

        public object this[int seq]
        {
            get
            {
                if (seq <= 0)
                {
                    return null;
                }
                FieldSlot slot;
                if (seq > 16)
                {
                    if (!_HighFields.TryGetValue(seq, out slot))
                    {
                        return null;
                    }
                }
                else
                {
                    slot = _LowFields[seq - 1];
                }
                if (slot.Value.Count <= 0)
                {
                    return null;
                }
                else if (slot.Value.Count == 1)
                {
                    return slot.Value[0].Value;
                }
                else
                {
                    return slot.Value;
                }
            }
            set
            {

            }
        }

        protected internal void Freeze()
        {

        }
    }

    public class RawMessageReader
    {
        //public 
    }
}