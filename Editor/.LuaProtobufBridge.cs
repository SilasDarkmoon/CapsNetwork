using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Capstones.UnityEngineEx;

using Capstones.LuaExt;
using Capstones.LuaLib;
using Capstones.LuaWrap;

namespace Capstones.LuaExt
{
    public static partial class LuaProtobufBridge
    {
        public delegate void SyncDataFunc(IntPtr l, object data);
        public delegate object CreateFunc();
        public class TypedDataBridge
        {
            public string Name { get; protected set; }
            public Type Type { get; protected set; }
            public SyncDataFunc PushFunc { get; protected set; }
            public SyncDataFunc ReadFunc { get; protected set; }
            public CreateFunc Create { get; protected set; }

            protected TypedDataBridge() { }
        }
        private class TypedDataBridgeReg : TypedDataBridge
        {
            public TypedDataBridgeReg(Type type, string name, SyncDataFunc pushFunc, SyncDataFunc readFunc, CreateFunc create)
            {
                Name = name;
                Type = type;
                PushFunc = pushFunc;
                ReadFunc = readFunc;
                Create = create;
                TypedSyncFuncs[type] = this;
                NamedSyncFuncs[name] = this;
                NameToType[name] = type;
                TypeToName[type] = name;
            }
        }

        private readonly static byte[] EmptyBuffer = new byte[0];

        private static Dictionary<Type, TypedDataBridge> _TypedSyncFuncs;
        public static Dictionary<Type, TypedDataBridge> TypedSyncFuncs
        {
            get
            {
                if (_TypedSyncFuncs == null)
                {
                    _TypedSyncFuncs = new Dictionary<Type, TypedDataBridge>();
                }
                return _TypedSyncFuncs;
            }
        }
        private static Dictionary<string, TypedDataBridge> _NamedSyncFuncs;
        public static Dictionary<string, TypedDataBridge> NamedSyncFuncs
        {
            get
            {
                if (_NamedSyncFuncs == null)
                {
                    _NamedSyncFuncs = new Dictionary<string, TypedDataBridge>();
                }
                return _NamedSyncFuncs;
            }
        }
        private static Dictionary<string, Type> _NameToType;
        public static Dictionary<string, Type> NameToType
        {
            get
            {
                if (_NameToType == null)
                {
                    _NameToType = new Dictionary<string, Type>();
                }
                return _NameToType;
            }
        }
        private static Dictionary<Type, string> _TypeToName;
        public static Dictionary<Type, string> TypeToName
        {
            get
            {
                if (_TypeToName == null)
                {
                    _TypeToName = new Dictionary<Type, string>();
                }
                return _TypeToName;
            }
        }

        public static void WriteProtocolData(this IntPtr l, object data)
        {
            if (data != null)
            {
                TypedDataBridge reg;
                if (TypedSyncFuncs.TryGetValue(data.GetType(), out reg))
                {
                    reg.PushFunc(l, data);
                }
            }
        }
        public static void ReadProtocolData(this IntPtr l, object data)
        {
            if (data != null)
            {
                TypedDataBridge reg;
                if (TypedSyncFuncs.TryGetValue(data.GetType(), out reg))
                {
                    reg.ReadFunc(l, data);
                }
            }
        }
        public static void PushProtocol(this IntPtr l, object data)
        {
            l.newtable();
            l.WriteProtocolData(data);
        }
        public static object GetProtocol(this IntPtr l, int index)
        {
            return ProtobufTrans.GetLuaRaw(l, index);
        }
        public static T GetProtocol<T>(this IntPtr l, int index) where T : new()
        {
            var rv = new T();
            l.pushvalue(index);
            l.ReadProtocolData(rv);
            l.pop(1);
            return rv;
        }

        private sealed class ProtobufTrans : SelfHandled, Capstones.LuaLib.ILuaTrans
        {
            public bool ShouldCache { get { return false; } }

            private static string GetName(IntPtr l, int index)
            {
                l.GetField(index, LS_messageName);
                string str = l.GetString(-1);
                l.pop(1);
                return str;
            }

            public static object GetLuaRaw(IntPtr l, int index)
            {
                var name = GetName(l, index);
                TypedDataBridge bridge;
                if (NamedSyncFuncs.TryGetValue(name, out bridge))
                {
                    var obj = bridge.Create();
                    l.pushvalue(index);
                    bridge.ReadFunc(l, obj);
                    l.pop(1);
                    return obj;
                }
                return null;
            }
            public object GetLua(IntPtr l, int index)
            {
                return GetLuaRaw(l, index);
            }

            public static Type GetTypeRaw(IntPtr l, int index)
            {
                var name = GetName(l, index);
                Type type;
                if (NameToType.TryGetValue(name, out type))
                {
                    return type;
                }
                return null;
            }
            public Type GetType(IntPtr l, int index)
            {
                return GetTypeRaw(l, index);
            }

            public static void SetDataRaw(IntPtr l, int index, object val)
            {
                var name = GetName(l, index);
                TypedDataBridge bridge;
                if (NamedSyncFuncs.TryGetValue(name, out bridge))
                {
                    l.pushvalue(index);
                    bridge.PushFunc(l, val);
                    l.pop(1);
                }
            }
            public void SetData(IntPtr l, int index, object val)
            {
                SetDataRaw(l, index, val);
            }
        }
        private static ProtobufTrans _ProtobufTrans = new ProtobufTrans();

        public class TypeHubProtocolPrecompiled<T> : Capstones.LuaLib.LuaTypeHub.TypeHubValueTypePrecompiled<T> where T : new()
        {
            public override IntPtr PushLua(IntPtr l, object val)
            {
                PushLua(l, (T)val);
                return IntPtr.Zero;
            }
            public override void SetData(IntPtr l, int index, object val)
            {
                SetDataRaw(l, index, (T)val);
            }
            public override object GetLuaObject(IntPtr l, int index)
            {
                return GetLuaRaw(l, index);
            }
            public static T GetLuaChecked(IntPtr l, int index)
            {
                if (l.istable(index))
                {
                    return GetLuaRaw(l, index);
                }
                return default(T);
            }

            public override IntPtr PushLua(IntPtr l, T val)
            {
                l.checkstack(3);
                l.newtable(); // ud
                SetDataRaw(l, -1, val);
                l.pushlightuserdata(LuaConst.LRKEY_TYPE_TRANS); // #trans
                l.pushnil();
                l.settable(-3);
                PushToLuaCached(l); // ud type
                l.pushlightuserdata(LuaConst.LRKEY_OBJ_META); // ud type #meta
                l.rawget(-2); // ud type meta
                l.setmetatable(-3); // ud type
                l.pop(1); // ud
                return IntPtr.Zero;
            }
            public override void SetData(IntPtr l, int index, T val)
            {
                SetDataRaw(l, index, val);
            }
            public override T GetLua(IntPtr l, int index)
            {
                return GetLuaRaw(l, index);
            }

            public static void SetDataRaw(IntPtr l, int index, T val)
            {
                TypedDataBridge bridge;
                if (TypedSyncFuncs.TryGetValue(typeof(T), out bridge))
                {
                    l.pushvalue(index);
                    bridge.PushFunc(l, val);
                    l.pop(1);
                }
            }
            public static T GetLuaRaw(IntPtr l, int index)
            {
                TypedDataBridge bridge;
                if (TypedSyncFuncs.TryGetValue(typeof(T), out bridge))
                {
                    var obj = new T();
                    l.pushvalue(index);
                    bridge.ReadFunc(l, obj);
                    l.pop(1);
                    return obj;
                }
                return default(T);
            }
        }
    }

#if UNITY_INCLUDE_TESTS
    #region TESTS
    public static class LuaBridgeGeneratorTest
    {
#if UNITY_EDITOR
        //[UnityEditor.MenuItem("Test/Protobuf Converter/Test Lua", priority = 200010)]
        //public static void TestLua()
        //{
        //    UnityEditor.AssetDatabase.OpenAsset(UnityEditor.AssetDatabase.LoadMainAssetAtPath(ResManager.__ASSET__), ResManager.__LINE__);

        //    var l = GlobalLua.L.L;
        //    using (var lr = l.CreateStackRecover())
        //    {
        //        l.CallGlobal<LuaPack<Protocols.Test.test3>, LuaPack>("dump", new Protocols.Test.test3() { TEST = 100, TEstTestTEST = 200 });

        //        l.PushProtocol(new Protocols.Test.test3());
        //        l.pushnumber(100);
        //        l.SetField(-2, "tEST");
        //        l.pushnumber(200);
        //        l.SetField(-2, "TEstTestTEST");
        //        var obj = l.GetLua(-1);
        //        PlatDependant.LogError(obj);
        //    }
        //}
#endif
    }
    #endregion
#endif
}
