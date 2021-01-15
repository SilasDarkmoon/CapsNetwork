using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Capstones.UnityEngineEx;

using Capstones.LuaExt;
using Capstones.LuaLib;
using Capstones.LuaWrap;
using lua = Capstones.LuaLib.LuaCoreLib;
using lual = Capstones.LuaLib.LuaAuxLib;
using luae = Capstones.LuaLib.LuaLibEx;

namespace Capstones.LuaLib
{
    public static partial class LuaHubEx
    {
        private static void InitLuaProtobufBridge(IntPtr l)
        {
            l.newtable();
            l.pushcfunction(ProtoDelCreateMessage);
            l.SetField(-2, "new");
            l.SetGlobal("proto");
        }

        public static readonly lua.CFunction ProtoDelCreateMessage = new lua.CFunction(ProtoFuncCreateMessage);
        [AOT.MonoPInvokeCallback(typeof(lua.CFunction))]
        public static int ProtoFuncCreateMessage(IntPtr l)
        {
            var argcnt = l.gettop();
            string name = null;
            if (argcnt >= 1)
            {
                name = l.GetString(1);
            }
            if (argcnt >= 2 && l.istable(2))
            {
                l.pushvalue(2);
            }
            else
            {
                l.newtable();
            }
            l.pushlightuserdata(LuaConst.LRKEY_TYPE_TRANS); // #trans
            l.pushlightuserdata(Capstones.LuaExt.LuaProtobufBridge._ProtobufTrans.r);
            l.settable(-3);
            if (!string.IsNullOrEmpty(name))
            {
                l.PushString(name);
                l.SetField(-2, Capstones.LuaExt.LuaProtobufBridge.LS_messageName);
            }
            return 1;
        }

        private static LuaFramework.FurtherInit _InitLuaProtobufBridge = new LuaFramework.FurtherInit(InitLuaProtobufBridge);
    }
}

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

        public static bool WriteProtocolData(this IntPtr l, object data)
        {
            if (data != null)
            {
                TypedDataBridge reg;
                if (TypedSyncFuncs.TryGetValue(data.GetType(), out reg))
                {
                    reg.PushFunc(l, data);
                    return true;
                }
            }
            return false;
        }
        public static bool ReadProtocolData(this IntPtr l, object data)
        {
            if (data != null)
            {
                TypedDataBridge reg;
                if (TypedSyncFuncs.TryGetValue(data.GetType(), out reg))
                {
                    reg.ReadFunc(l, data);
                    return true;
                }
            }
            return false;
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

        internal sealed class ProtobufTrans : SelfHandled, Capstones.LuaLib.ILuaTrans
        {
            public bool ShouldCache { get { return false; } }
            public bool Nonexclusive { get { return true; } }

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
        internal static ProtobufTrans _ProtobufTrans = new ProtobufTrans();

        public class TypeHubProtocolPrecompiled<T> : Capstones.LuaLib.LuaTypeHub.TypeHubValueTypePrecompiled<T>, ILuaNative where T : new()
        {
            public override bool Nonexclusive { get { return true; } }
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
                l.pushlightuserdata(LuaConst.LRKEY_TYPE_TRANS); // ud #trans
                l.pushvalue(-1); // ud #trans #trans
                l.gettable(-3); // ud #trans trans
                l.rawset(-3); // ud
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
            public void Wrap(IntPtr l, int index)
            {
                T val = GetLua(l, index);
                PushLua(l, val);
            }
            public void Unwrap(IntPtr l, int index)
            {
                var val = GetLuaRaw(l, index);
                l.newtable(); // ud
                SetDataRaw(l, -1, val);
            }

            public static readonly LuaNativeProtocol<T> LuaHubNative = new LuaNativeProtocol<T>();
        }
        public class LuaNativeProtocol<T> : Capstones.LuaLib.LuaHub.LuaPushNativeBase<T> where T : new()
        {
            public override T GetLua(IntPtr l, int index)
            {
                return TypeHubProtocolPrecompiled<T>.GetLuaRaw(l, index);
            }
            public override IntPtr PushLua(IntPtr l, T val)
            {
                l.newtable(); // ud
                TypeHubProtocolPrecompiled<T>.SetDataRaw(l, -1, val);
                return IntPtr.Zero;
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

namespace LuaProto
{
    using pb = global::Google.Protobuf;
    using pbc = global::Google.Protobuf.Collections;
    using pbr = global::Google.Protobuf.Reflection;
    using scg = global::System.Collections.Generic;

    public interface IBidirectionConvertible<T>
    {
        void CopyFrom(T message);
        void CopyTo(T message);
    }
    public interface IProtoConvertible<T> : IBidirectionConvertible<T>
    {
        T Convert();
    }
    public interface IWrapperConvertible<T> : IBidirectionConvertible<T>
    {
        T Convert(IntPtr l);
    }

    public abstract class BaseLuaProtoWrapper<TWrapper, TProto> : BaseLuaWrapper<TWrapper>, pb::IMessage<TWrapper>, IProtoConvertible<TProto>
        where TWrapper : BaseLuaWrapper, pb::IMessage<TWrapper>, IProtoConvertible<TProto>, new()
        where TProto : pb::IMessage<TProto>, new()
    {
        public BaseLuaProtoWrapper() { }
        public BaseLuaProtoWrapper(IntPtr l) : base(l) { }

        public override BaseLua Binding
        {
            get { return base.Binding; }
            set
            {
                if (!ReferenceEquals(value, null))
                {
                    var l = value.L;
                    using (var lr = l.CreateStackRecover())
                    {
                        l.PushLua(value);
                        l.PushString("messageName");
                        l.PushString(ProtoTemplate.Descriptor.FullName);
                        l.settable(-3);
                    }
                }
                base.Binding = value;
            }
        }

        protected static readonly TProto ProtoTemplate = new TProto();
        public pbr.MessageDescriptor Descriptor { get { return ProtoTemplate.Descriptor; } }
        // read data to raw proto obj from stream. And then read data from raw proto obj.
        public void MergeFrom(pb.CodedInputStream input)
        {
            var template = new TProto();
            template.MergeFrom(input);
            CopyFrom(template);
        }
        // write data to raw proto obj. And then write raw proto obj to stream.
        public void WriteTo(pb.CodedOutputStream output)
        {
            Convert().WriteTo(output);
        }
        // convert to raw proto obj and calculate size.
        public int CalculateSize()
        {
            return Convert().CalculateSize();
        }
        // not really clone. the cloned wrapper points to the same lua-table.
        public TWrapper Clone()
        {
            return new TWrapper() { Binding = Binding };
        }
        // compares whether they point to the same lua-table.
        public bool Equals(TWrapper other)
        {
            return Equals(Binding, other == null ? null : other.Binding);
        }
        // just point to the same lua-table. not really copy data.
        public void MergeFrom(TWrapper other)
        {
            Binding = other == null ? null : other.Binding;
        }

        public static TProto Convert(TWrapper wrapper)
        {
            if (wrapper == null)
            {
                return default(TProto);
            }
            var result = new TProto();
            wrapper.CopyTo(result);
            return result;
        }
        public TProto Convert()
        {
            var result = new TProto();
            CopyTo(result);
            return result;
        }

        public abstract void CopyFrom(TProto message);
        public abstract void CopyTo(TProto message);
    }

    public static class LuaProtoWrapperExtensions
    {
        public static void ConvertField<TDest, TSrc>(this IntPtr l, out TDest dest, TSrc src)
        {
            if (src is ILuaWrapper)
            {
                var convertible = src as IProtoConvertible<TDest>;
                dest = convertible.Convert();
            }
            else
            {
                var convertible = src as IWrapperConvertible<TDest>;
                dest = convertible.Convert(l);
            }
        }
        public static void ConvertField<TDest, TSrc>(this ILuaWrapper thiz, out TDest dest, TSrc src)
        {
            ConvertField(thiz.Binding.L, out dest, src);
        }
        public static void ConvertField<T>(this ILuaWrapper thiz, out T dest, T src)
        {
            dest = src;
        }
        public static void ConvertField<TDest, TSrc>(this ILuaWrapper thiz, pbc.RepeatedField<TDest> dest, LuaList<TSrc> src)
        {
            dest.Clear();
            src.ForEach(item =>
            {
                TDest ditem;
                ConvertField(thiz, out ditem, item);
                dest.Add(ditem);
            });
        }
        public static void ConvertField<T>(this pbc.RepeatedField<T> dest, LuaList<T> src)
        {
            dest.Clear();
            src.ForEach(item =>
            {
                dest.Add(item);
            });
        }
        public static void ConvertField<TDest, TSrc>(this ILuaWrapper thiz, LuaList<TDest> dest, pbc.RepeatedField<TSrc> src)
        {
            dest.Clear();
            for (int i = 0; i < src.Count; ++i)
            {
                var item = src[i];
                TDest ditem;
                ConvertField(thiz, out ditem, item);
                dest.Add(ditem);
            }
        }
        public static void ConvertField<T>(this LuaList<T> dest, pbc.RepeatedField<T> src)
        {
            dest.Clear();
            for (int i = 0; i < src.Count; ++i)
            {
                var item = src[i];
                dest.Add(item);
            }
        }
        //public static LuaList<TDest> ConvertField<TDest, TSrc>(this pbc.RepeatedField<TSrc> src, IntPtr l)
        //{
        //    var dest = new LuaList<TDest>(l);
        //    for (int i = 0; i < src.Count; ++i)
        //    {
        //        var item = src[i];
        //        TDest ditem;
        //        ConvertField(l, out ditem, item);
        //        dest.Add(ditem);
        //    }
        //    return dest;
        //}
        //public static LuaList<T> ConvertField<T>(this pbc.RepeatedField<T> src, IntPtr l)
        //{
        //    var dest = new LuaList<T>(l);
        //    for (int i = 0; i < src.Count; ++i)
        //    {
        //        var item = src[i];
        //        dest.Add(item);
        //    }
        //    return dest;
        //}
        public static T ConvertField<T>(this IWrapperConvertible<T> src, IntPtr l)
        {
            if (src == null)
            {
                return default(T);
            }
            else
            {
                return src.Convert(l);
            }
        }
        public static T ConvertField<T>(this IProtoConvertible<T> src)
        {
            if (src == null)
            {
                return default(T);
            }
            else
            {
                return src.Convert();
            }
        }
    }
}
