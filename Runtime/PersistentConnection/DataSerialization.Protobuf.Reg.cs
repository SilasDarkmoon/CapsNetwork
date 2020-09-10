using System;
using System.Collections.Generic;

namespace Capstones.Net
{
    public static partial class ProtobufReg
    {
        private static Dictionary<uint, Google.Protobuf.MessageParser> _DataParsers;
        public static Dictionary<uint, Google.Protobuf.MessageParser> DataParsers
        {
            get
            {
                if (_DataParsers == null)
                {
                    _DataParsers = new Dictionary<uint, Google.Protobuf.MessageParser>();
                }
                return _DataParsers;
            }
        }
        private static Dictionary<Type, uint> _RegisteredTypes;
        public static Dictionary<Type, uint> RegisteredTypes
        {
            get
            {
                if (_RegisteredTypes == null)
                {
                    _RegisteredTypes = new Dictionary<Type, uint>();
                }
                return _RegisteredTypes;
            }
        }

        public class RegisteredType
        {
            public RegisteredType(uint id, Type messageType, Google.Protobuf.MessageParser parser)
            {
                DataParsers[id] = parser;
                RegisteredTypes[messageType] = id;
            }
        }
    }
}