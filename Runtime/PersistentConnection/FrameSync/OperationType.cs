using System;
using System.Collections.Generic;

namespace Capstones.Net.FrameSync
{
    public static partial class OperationType
    {
        private static readonly HashSet<Type> _FrameSyncProtocols = new HashSet<Type>();
        private static readonly HashSet<Type> _FrameSyncReqProtocols = new HashSet<Type>();
        private static Type _FrameSyncBeginProtocol;
        private static Type _FrameSyncEndProtocol;
        private static Type _FrameSyncTickProtocol;
        public static HashSet<Type> FrameSyncProtocols { get { return _FrameSyncProtocols; } }
        public static HashSet<Type> FrameSyncReqProtocols { get { return _FrameSyncReqProtocols; } }
        public static Type FrameSyncBeginProtocol { get { return _FrameSyncBeginProtocol; } set { _FrameSyncBeginProtocol = value; } }
        public static Type FrameSyncEndProtocol { get { return _FrameSyncEndProtocol; } set { _FrameSyncEndProtocol = value; } }
        public static Type FrameSyncTickProtocol { get { return _FrameSyncTickProtocol; } set { _FrameSyncTickProtocol = value; } }

        public static Func<object, int> FuncGetFrameSyncBeginInterval;
        public static Func<object, int> FuncGetFrameSyncBeginIndex;
        public static Func<object, int> FuncGetFrameSyncBeginTime;
        public static Func<object, int> FuncGetFrameSyncTickTime;
        public static Func<object, int> FuncGetFrameSyncTickInterval;

        public static bool IsFrameSyncProtocol(Type type)
        {
            return _FrameSyncProtocols.Contains(type);
        }
        public static bool IsFrameSyncProtocol(object obj)
        {
            return IsFrameSyncProtocol(obj == null ? null : obj.GetType());
        }
        public static bool IsFrameSyncReqProtocol(Type type)
        {
            return _FrameSyncReqProtocols.Contains(type);
        }
        public static bool IsFrameSyncReqProtocol(object obj)
        {
            return IsFrameSyncReqProtocol(obj == null ? null : obj.GetType());
        }
        public static bool IsFrameSyncBegin(object obj)
        {
            return obj != null && obj.GetType() == _FrameSyncBeginProtocol;
        }
        public static bool IsFrameSyncEnd(object obj)
        {
            return obj != null && obj.GetType() == _FrameSyncEndProtocol;
        }
        public static bool IsFrameSyncTick(object obj)
        {
            return obj != null && obj.GetType() == _FrameSyncTickProtocol;
        }
        public static int GetFrameSyncBeginInterval(object obj)
        {
            if (obj != null && FuncGetFrameSyncBeginInterval != null)
            {
                return FuncGetFrameSyncBeginInterval(obj);
            }
            return 0;
        }
        public static int GetFrameSyncBeginIndex(object obj)
        {
            if (obj != null && FuncGetFrameSyncBeginIndex != null)
            {
                return FuncGetFrameSyncBeginIndex(obj);
            }
            return 0;
        }
        public static int GetFrameSyncBeginTime(object obj)
        {
            if (obj != null && FuncGetFrameSyncBeginTime != null)
            {
                return FuncGetFrameSyncBeginTime(obj);
            }
            return 0;
        }
        public static int GetFrameSyncTickTime(object obj)
        {
            if (obj != null && FuncGetFrameSyncTickTime != null)
            {
                return FuncGetFrameSyncTickTime(obj);
            }
            return 0;
        }
        public static int GetFrameSyncTickInterval(object obj)
        {
            if (obj != null && FuncGetFrameSyncTickInterval != null)
            {
                return FuncGetFrameSyncTickInterval(obj);
            }
            return 0;
        }
    }
}