using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

#if MOD_CAPSLUA_V2
namespace Capstones.UnityEditorEx.Net
{
    using UnityEngine;
    using UnityEditor;
    using Capstones.UnityEngineEx;
    using Capstones.Net;

    public static class LuaBridgeGenerator
    {
        public static string ToCSharpEnumName(string ename)
        {
            StringBuilder csname = new StringBuilder();
            bool canStartNewGroup = true;
            int groupIndex = 0;
            
            for (int i = 0; i < ename.Length; ++i)
            {
                var ch = ename[i];
                if (canStartNewGroup && char.IsUpper(ch))
                {
                    canStartNewGroup = false;
                    groupIndex = 0;
                }
                else if (char.IsLower(ch))
                {
                    canStartNewGroup = true;
                }
                if (groupIndex == 0)
                {
                    csname.Append(char.ToUpper(ch));
                }
                else
                {
                    csname.Append(char.ToLower(ch));
                }
                ++groupIndex;
            }
            return csname.ToString();
        }

        [MenuItem("Net/Generate Lua-Protobuf Bridge", priority = 100030)]
        public static void Generate_Lua_Data_Bridge()
        {
            var mod = CapsEditorUtils.__MOD__;
            var outputdir = "Assets/Mods/" + mod + "/";
            var hubdir = outputdir + "LuaHubSub/";
            if (System.IO.Directory.Exists(hubdir))
            {
                System.IO.Directory.Delete(hubdir, true);
            }

            var curfile = CapsEditorUtils.__FILE__;
            var bridgesrc = System.IO.Path.GetDirectoryName(curfile) + "/.LuaProtobufBridge.cs";
            if (PlatDependant.IsFileExist(bridgesrc))
            {
                PlatDependant.CopyFile(bridgesrc, hubdir + "LuaProtobufBridge.cs");
            }

            Dictionary<string, CapsNetworkEditor.ProtocolInfo> allmessagesinallfiles = new Dictionary<string, CapsNetworkEditor.ProtocolInfo>();
            Func<string, string> GetCSharpMessageName = mname =>
            {
                if (mname.StartsWith("."))
                {
                    mname = mname.Substring(1);
                }
                CapsNetworkEditor.ProtocolInfo info;
                if (allmessagesinallfiles.TryGetValue(mname, out info))
                {
                    return info.FullCSharpName;
                }
                else
                {
                    return CapsNetworkEditor.ToCSharpName(mname);
                }
            };

            // Message and Enum Reg
            var protosrcs = CapsModEditor.FindAssetsInMods("Protocols/Src/Combined.proto");
            foreach (var srcfile in protosrcs)
            {
                var srcdir = System.IO.Path.GetDirectoryName(srcfile); // XXX/Protocols/Src
                var protodir = System.IO.Path.GetDirectoryName(srcdir); // XXX/Protocols
                var compdir = protodir + "/Compiled"; // XXX/Protocols/Compiled

                var files = PlatDependant.GetAllFiles(srcdir);
                foreach (var file in files)
                {
                    if (file.EndsWith(".proto"))
                    {
                        var part = file.Substring(srcdir.Length, file.Length - srcdir.Length - ".proto".Length);
                        var binfile = compdir + part + ".pb";
                        if (PlatDependant.IsFileExist(binfile))
                        {
                            var bincontent = PlatDependant.ReadAllBytes(binfile);
                            var txtcontent = PlatDependant.ReadAllLines(file);

                            var mess_set = ProtobufEncoder.ReadRaw(new ListSegment<byte>(bincontent));
                            mess_set.ApplyTemplate(ProtobufMessagePool.FileDescriptorSetTemplate);
                            var allmessages = CapsNetworkEditor.GetAllMessages(mess_set);
                            var sorted = CapsNetworkEditor.ParseExInfo(allmessages, txtcontent);
                            allmessagesinallfiles.Merge(allmessages);
                        }
                    }
                }
            }


            HashSet<string> fieldKeys = new HashSet<string>() { "messageName" };
            // Enumerate All Messages
            foreach (var srcfile in protosrcs)
            {
                var srcdir = System.IO.Path.GetDirectoryName(srcfile); // XXX/Protocols/Src
                var protodir = System.IO.Path.GetDirectoryName(srcdir); // XXX/Protocols
                var compdir = protodir + "/Compiled"; // XXX/Protocols/Compiled

                var files = PlatDependant.GetAllFiles(srcdir);
                foreach (var file in files)
                {
                    if (file.EndsWith(".proto"))
                    {
                        var part = file.Substring(srcdir.Length, file.Length - srcdir.Length - ".proto".Length);
                        var binfile = compdir + part + ".pb";
                        if (PlatDependant.IsFileExist(binfile))
                        {
                            var bincontent = PlatDependant.ReadAllBytes(binfile);
                            var txtcontent = PlatDependant.ReadAllLines(file);

                            var mess_set = ProtobufEncoder.ReadRaw(new ListSegment<byte>(bincontent));
                            mess_set.ApplyTemplate(ProtobufMessagePool.FileDescriptorSetTemplate);
                            var allmessages = CapsNetworkEditor.GetAllMessages(mess_set);
                            var sorted = CapsNetworkEditor.ParseExInfo(allmessages, txtcontent);
                            if (sorted.Length > 0)
                            {
                                var sbFileNamePart = new StringBuilder();
                                sbFileNamePart.Append(part.Replace('\\', '_').Replace('/', '_').Replace('.', '_'));
                                sbFileNamePart.Append("_");
                                sbFileNamePart.Append(sorted[0].FullCSharpName.Replace('.', '_'));

                                Func<string, string> GetPackageName = name =>
                                {
                                    while (allmessages.ContainsKey(name))
                                    {
                                        var index = name.LastIndexOf(".");
                                        if (index <= 0)
                                        {
                                            return "";
                                        }
                                        name = name.Substring(0, index);
                                    }
                                    return name;
                                };

                                // bridge
                                {
                                    StringBuilder sbfile = new StringBuilder();
                                    bool firstMessage = true;
                                    Action<ProtobufMessage, string, string> AppendPushValue = (field, prefix, suffix) =>
                                    {
                                        prefix = prefix ?? "";
                                        suffix = suffix ?? "";
                                        var fname = field["name"].String;
                                        if (char.IsLower(fname[0]))
                                        {
                                            fname = char.ToUpper(fname[0]) + fname.Substring(1);
                                        }
                                        var ftype = field["type"].AsEnum<ProtobufNativeType>();
                                        fname += suffix;
                                        switch (ftype)
                                        {
                                            case ProtobufNativeType.TYPE_BOOL:
                                                sbfile.AppendLine(prefix + "            l.pushboolean(data." + fname + ");");
                                                break;
                                            case ProtobufNativeType.TYPE_BYTES:
                                                sbfile.AppendLine(prefix + "            if (data." + fname + " == null)");
                                                sbfile.AppendLine(prefix + "            {");
                                                sbfile.AppendLine(prefix + "                l.pushnil();");
                                                sbfile.AppendLine(prefix + "            }");
                                                sbfile.AppendLine(prefix + "            else");
                                                sbfile.AppendLine(prefix + "            {");
                                                sbfile.AppendLine(prefix + "                l.pushbuffer(data." + fname + ");");
                                                sbfile.AppendLine(prefix + "            }");
                                                break;
                                            case ProtobufNativeType.TYPE_DOUBLE:
                                            case ProtobufNativeType.TYPE_FIXED32:
                                            case ProtobufNativeType.TYPE_FIXED64:
                                            case ProtobufNativeType.TYPE_FLOAT:
                                            case ProtobufNativeType.TYPE_INT32:
                                            case ProtobufNativeType.TYPE_INT64:
                                            case ProtobufNativeType.TYPE_SFIXED32:
                                            case ProtobufNativeType.TYPE_SFIXED64:
                                            case ProtobufNativeType.TYPE_SINT32:
                                            case ProtobufNativeType.TYPE_SINT64:
                                            case ProtobufNativeType.TYPE_UINT32:
                                            case ProtobufNativeType.TYPE_UINT64:
                                                sbfile.AppendLine(prefix + "            l.pushnumber(data." + fname + ");");
                                                break;
                                            case ProtobufNativeType.TYPE_ENUM:
                                                sbfile.AppendLine(prefix + "            l.pushnumber((uint)data." + fname + ");");
                                                break;
                                            case ProtobufNativeType.TYPE_MESSAGE:
                                                sbfile.AppendLine(prefix + "            if (data." + fname + " == null)");
                                                sbfile.AppendLine(prefix + "            {");
                                                sbfile.AppendLine(prefix + "                l.pushnil();");
                                                sbfile.AppendLine(prefix + "            }");
                                                sbfile.AppendLine(prefix + "            else");
                                                sbfile.AppendLine(prefix + "            {");
                                                sbfile.AppendLine(prefix + "                l.newtable();");
                                                sbfile.AppendLine(prefix + "                l.WriteProtocolData(data." + fname + ");");
                                                sbfile.AppendLine(prefix + "            }");
                                                break;
                                            case ProtobufNativeType.TYPE_STRING:
                                                sbfile.AppendLine(prefix + "            if (data." + fname + " == null)");
                                                sbfile.AppendLine(prefix + "            {");
                                                sbfile.AppendLine(prefix + "                l.pushnil();");
                                                sbfile.AppendLine(prefix + "            }");
                                                sbfile.AppendLine(prefix + "            else");
                                                sbfile.AppendLine(prefix + "            {");
                                                //fieldKeys.Add(fname);
                                                sbfile.AppendLine(prefix + "                l.PushString(data." + fname + ");");
                                                sbfile.AppendLine(prefix + "            }");
                                                break;
                                            case ProtobufNativeType.TYPE_GROUP: // currently unhandled.
                                            default:
                                                sbfile.AppendLine(prefix + "            l.pushnil();");
                                                break;
                                        }
                                    };
                                    Action<ProtobufMessage> AppendPushRepeated = field =>
                                    {
                                        var fname = field["name"].String;
                                        if (char.IsLower(fname[0]))
                                        {
                                            fname = char.ToUpper(fname[0]) + fname.Substring(1);
                                        }
                                        sbfile.AppendLine("            if (data." + fname + " == null)");
                                        sbfile.AppendLine("            {");
                                        sbfile.AppendLine("                l.pushnil();");
                                        sbfile.AppendLine("            }");
                                        sbfile.AppendLine("            else");
                                        sbfile.AppendLine("            {");
                                        sbfile.AppendLine("                l.newtable();");
                                        sbfile.AppendLine("                for (int i = 0; i < data." + fname + ".Count; ++i)");
                                        sbfile.AppendLine("                {");
                                        sbfile.AppendLine("                    l.pushnumber(i + 1);");
                                        AppendPushValue(field, "        ", "[i]");
                                        sbfile.AppendLine("                    l.settable(-3);");
                                        sbfile.AppendLine("                }");
                                        sbfile.AppendLine("            }");
                                    };
                                    Dictionary<ProtobufNativeType, string> fieldType2CSName = new Dictionary<ProtobufNativeType, string>()
                                    {
                                        { ProtobufNativeType.TYPE_BOOL, "bool" },
                                        { ProtobufNativeType.TYPE_BYTES, "byte[]" },
                                        { ProtobufNativeType.TYPE_DOUBLE, "double" },
                                        { ProtobufNativeType.TYPE_ENUM, null },
                                        { ProtobufNativeType.TYPE_FIXED32, "uint" },
                                        { ProtobufNativeType.TYPE_FIXED64, "ulong" },
                                        { ProtobufNativeType.TYPE_FLOAT, "float" },
                                        //{ Google.Protobuf.Reflection.FieldType.Group, null }, // currently unhandled.
                                        { ProtobufNativeType.TYPE_INT32, "int" },
                                        { ProtobufNativeType.TYPE_INT64, "long" },
                                        { ProtobufNativeType.TYPE_MESSAGE, null },
                                        { ProtobufNativeType.TYPE_SFIXED32, "int" },
                                        { ProtobufNativeType.TYPE_SFIXED64, "long" },
                                        { ProtobufNativeType.TYPE_SINT32, "int" },
                                        { ProtobufNativeType.TYPE_SINT64, "long" },
                                        { ProtobufNativeType.TYPE_STRING, "string" },
                                        { ProtobufNativeType.TYPE_UINT32, "uint" },
                                        { ProtobufNativeType.TYPE_UINT64, "ulong" },
                                    };
                                    Func<ProtobufMessage, string> GetFieldDefaultValueString = field =>
                                    {
                                        string typename;
                                        var ftype = field["type"].AsEnum<ProtobufNativeType>();
                                        if (ftype == ProtobufNativeType.TYPE_STRING)
                                        {
                                            return "\"\"";
                                        }
                                        if (fieldType2CSName.TryGetValue(ftype, out typename))
                                        {
                                            if (typename != null)
                                            {
                                                return "default(" + typename + ")";
                                            }
                                            else if (ftype == ProtobufNativeType.TYPE_ENUM)
                                            {
                                                var mtype = field["type_name"].String;
                                                return "default(" + GetCSharpMessageName(mtype) + ")";
                                            }
                                            else if (ftype == ProtobufNativeType.TYPE_MESSAGE)
                                            {
                                                return "null";
                                            }
                                        }
                                        return null;
                                    };
                                    Action<ProtobufMessage, string, string> AppendReadValue = (field, prefix, suffix) =>
                                    {
                                        prefix = prefix ?? "";
                                        suffix = suffix ?? "";
                                        var fname = field["name"].String;
                                        var ftype = field["type"].AsEnum<ProtobufNativeType>();
                                        if (char.IsLower(fname[0]))
                                        {
                                            fname = char.ToUpper(fname[0]) + fname.Substring(1);
                                        }
                                        fname += suffix;

                                        string defaultstr = GetFieldDefaultValueString(field);
                                        if (defaultstr != null)
                                        {
                                            var cstypename = fieldType2CSName[ftype];
                                            sbfile.AppendLine(prefix + "            if (l.isnoneornil(-1))");
                                            sbfile.AppendLine(prefix + "            {");
                                            sbfile.AppendLine(prefix + "                data." + fname + " = " + defaultstr + ";");
                                            sbfile.AppendLine(prefix + "            }");
                                            sbfile.AppendLine(prefix + "            else");
                                            sbfile.AppendLine(prefix + "            {");
                                            int curpos = sbfile.Length;
                                            sbfile.Append(prefix + "                data." + fname + " = ");
                                            switch (ftype)
                                            {
                                                case ProtobufNativeType.TYPE_BOOL:
                                                    sbfile.AppendLine("l.toboolean(-1);");
                                                    break;
                                                case ProtobufNativeType.TYPE_BYTES:
                                                    sbfile.AppendLine("l.tolstring(-1);");
                                                    break;
                                                case ProtobufNativeType.TYPE_DOUBLE:
                                                case ProtobufNativeType.TYPE_FIXED32:
                                                case ProtobufNativeType.TYPE_FIXED64:
                                                case ProtobufNativeType.TYPE_FLOAT:
                                                case ProtobufNativeType.TYPE_INT32:
                                                case ProtobufNativeType.TYPE_INT64:
                                                case ProtobufNativeType.TYPE_SFIXED32:
                                                case ProtobufNativeType.TYPE_SFIXED64:
                                                case ProtobufNativeType.TYPE_SINT32:
                                                case ProtobufNativeType.TYPE_SINT64:
                                                case ProtobufNativeType.TYPE_UINT32:
                                                case ProtobufNativeType.TYPE_UINT64:
                                                    sbfile.AppendLine("(" + cstypename + ")l.tonumber(-1);");
                                                    break;
                                                case ProtobufNativeType.TYPE_ENUM:
                                                    {
                                                        var mtype = field["type_name"].String;
                                                        sbfile.AppendLine("(" + GetCSharpMessageName(mtype) + ")(uint)l.tonumber(-1);");
                                                        break;
                                                    }
                                                case ProtobufNativeType.TYPE_MESSAGE:
                                                    {
                                                        var mtype = field["type_name"].String;
                                                        sbfile.AppendLine("new " + GetCSharpMessageName(mtype) + "();");
                                                        sbfile.AppendLine(prefix + "                l.ReadProtocolData(data." + fname + ");");
                                                        break;
                                                    }
                                                case ProtobufNativeType.TYPE_STRING:
                                                    sbfile.AppendLine("l.GetString(-1) ?? \"\";");
                                                    break;
                                                case ProtobufNativeType.TYPE_GROUP: // currently unhandled.
                                                default:
                                                    sbfile.Remove(curpos, sbfile.Length - curpos);
                                                    break;
                                            }
                                            sbfile.AppendLine(prefix + "            }");
                                        }
                                    };
                                    Action<ProtobufMessage> AppendReadRepeated = field =>
                                    {
                                        var fname = field["name"].String;
                                        var ftype = field["type"].AsEnum<ProtobufNativeType>();
                                        if (char.IsLower(fname[0]))
                                        {
                                            fname = char.ToUpper(fname[0]) + fname.Substring(1);
                                        }
                                        var defaultStr = GetFieldDefaultValueString(field);
                                        sbfile.AppendLine("            data." + fname + ".Clear();");
                                        sbfile.AppendLine("            if (l.istable(-1))");
                                        sbfile.AppendLine("            {");
                                        sbfile.AppendLine("                int maxkey = 0;");
                                        sbfile.AppendLine("                l.pushnil();");
                                        sbfile.AppendLine("                while (l.next(-2))");
                                        sbfile.AppendLine("                {");
                                        sbfile.AppendLine("                    if (l.IsNumber(-2))");
                                        sbfile.AppendLine("                    {");
                                        sbfile.AppendLine("                        var key = (int)l.tonumber(-2);");
                                        sbfile.AppendLine("                        if (key > maxkey)");
                                        sbfile.AppendLine("                        {");
                                        sbfile.AppendLine("                            maxkey = key;");
                                        sbfile.AppendLine("                        }");
                                        sbfile.AppendLine("                    }");
                                        sbfile.AppendLine("                    l.pop(1);");
                                        sbfile.AppendLine("                }");
                                        sbfile.AppendLine("                for (int i = 0; i < maxkey; ++i)");
                                        sbfile.AppendLine("                {");
                                        if (ftype == ProtobufNativeType.TYPE_MESSAGE)
                                        {
                                            var mtype = field["type_name"].String;
                                            sbfile.AppendLine("                    data." + fname + ".Add(new " + GetCSharpMessageName(mtype) + "());");
                                        }
                                        //else if (ftype == ProtobufNativeType.TYPE_STRING)
                                        //{
                                        //    sbfile.AppendLine("                    data." + fname + ".Add(\"\");");
                                        //}
                                        else if (ftype == ProtobufNativeType.TYPE_BYTES)
                                        {
                                            sbfile.AppendLine("                    data." + fname + ".Add(EmptyBuffer);");
                                        }
                                        else
                                        {
                                            sbfile.AppendLine("                    data." + fname + ".Add(" + defaultStr + ");");
                                        }
                                        sbfile.AppendLine("                    l.pushnumber(i + 1);");
                                        sbfile.AppendLine("                    l.gettable(-2);");
                                        if (ftype == ProtobufNativeType.TYPE_MESSAGE)
                                        {
                                            sbfile.AppendLine("                    l.ReadProtocolData(data." + fname + "[i]);");
                                        }
                                        else
                                        {
                                            AppendReadValue(field, "        ", "[i]");
                                        }
                                        sbfile.AppendLine("                    l.pop(1);");
                                        sbfile.AppendLine("                }");
                                        sbfile.AppendLine("            }");
                                    };
                                    Action<CapsNetworkEditor.ProtocolInfo> AppendProtocol = minfo =>
                                    {
                                        if (!firstMessage)
                                        {
                                            sbfile.AppendLine("");
                                        }
                                        firstMessage = false;

                                        var fname = minfo.FullName;
                                        var csname = minfo.FullCSharpName;
                                        var desc = minfo.Desc;
                                        var pname = GetPackageName(fname);
                                        var typename = csname.Substring(pname.Length).Replace(".", "");

                                        sbfile.AppendLine("        public static void WriteProtocolData(this IntPtr l, " + csname + " data)");
                                        sbfile.AppendLine("        {");
                                        fieldKeys.Add(fname);
                                        sbfile.AppendLine("            l.PushString(LS_" + fname.Replace('.', '_') + ");");
                                        sbfile.AppendLine("            l.RawSet(-2, LS_messageName);");
                                        sbfile.AppendLine("            l.pushlightuserdata(LuaConst.LRKEY_TYPE_TRANS); // #trans");
                                        sbfile.AppendLine("            l.pushlightuserdata(_ProtobufTrans.r);");
                                        sbfile.AppendLine("            l.settable(-3);");
                                        foreach (var field in desc["field"].Messages)
                                        {
                                            if (field["label"].AsEnum<ProtobufFieldLabel>() == ProtobufFieldLabel.LABEL_REPEATED)
                                            {
                                                AppendPushRepeated(field);
                                            }
                                            else
                                            {
                                                AppendPushValue(field, null, null);
                                            }
                                            var fieldname = field["name"].String;
                                            fieldKeys.Add(fieldname);
                                            sbfile.AppendLine("            l.RawSet(-2, LS_" + fieldname + ");");
                                        }
                                        sbfile.AppendLine("        }");
                                        sbfile.AppendLine("        public static void ReadProtocolData(this IntPtr l, " + csname + " data)");
                                        sbfile.AppendLine("        {");
                                        foreach (var field in desc["field"].Messages)
                                        {
                                            var fieldname = field["name"].String;
                                            fieldKeys.Add(fieldname);
                                            sbfile.AppendLine("            l.RawGet(-1, LS_" + fieldname + ");");
                                            if (field["label"].AsEnum<ProtobufFieldLabel>() == ProtobufFieldLabel.LABEL_REPEATED)
                                            {
                                                AppendReadRepeated(field);
                                            }
                                            else
                                            {
                                                AppendReadValue(field, null, null);
                                            }
                                            sbfile.AppendLine("            l.pop(1);");
                                        }
                                        sbfile.AppendLine("        }");
                                        sbfile.AppendLine("        private static object Create" + typename + "()");
                                        sbfile.AppendLine("        {");
                                        sbfile.AppendLine("            return new " + csname + "();");
                                        sbfile.AppendLine("        }");
                                        sbfile.AppendLine("        private static void Push" + typename + "(IntPtr l, object data)");
                                        sbfile.AppendLine("        {");
                                        sbfile.AppendLine("            WriteProtocolData(l, (" + csname + ")data);");
                                        sbfile.AppendLine("        }");
                                        sbfile.AppendLine("        private static void Read" + typename + "(IntPtr l, object data)");
                                        sbfile.AppendLine("        {");
                                        sbfile.AppendLine("            ReadProtocolData(l, (" + csname + ")data);");
                                        sbfile.AppendLine("        }");
                                        sbfile.AppendLine("        private static TypedDataBridgeReg _Reg_" + typename + " = new TypedDataBridgeReg(typeof(" + csname + "), \"" + fname + "\", Push" + typename + ", Read" + typename + ", Create" + typename + ");");
                                    };
                                    sbfile.AppendLine("using System;");
                                    sbfile.AppendLine("using Capstones.LuaExt;");
                                    sbfile.AppendLine("using Capstones.LuaLib;");
                                    sbfile.AppendLine("using Capstones.LuaWrap;");
                                    sbfile.AppendLine("");
                                    sbfile.AppendLine("namespace Capstones.LuaExt");
                                    sbfile.AppendLine("{");
                                    sbfile.AppendLine("    public static partial class LuaProtobufBridge");
                                    sbfile.AppendLine("    {");
                                    foreach (var minfo in sorted)
                                    {
                                        if (!minfo.IsEnum)
                                        {
                                            AppendProtocol(minfo);
                                        }
                                    }
                                    sbfile.AppendLine("    }");
                                    sbfile.AppendLine("}");

                                    System.IO.File.WriteAllText(hubdir + "LuaProtobufBridge" + sbFileNamePart + ".cs", sbfile.ToString());
                                }

                                // LuaHubSub
                                using (var sw = PlatDependant.OpenWriteText(hubdir + "LuaHubSub" + sbFileNamePart + ".cs"))
                                {
                                    sw.WriteLine("using System;");
                                    sw.WriteLine("using Capstones.LuaExt;");
                                    sw.WriteLine("using Capstones.LuaLib;");
                                    sw.WriteLine("using Capstones.LuaWrap;");
                                    sw.WriteLine("using lua = Capstones.LuaLib.LuaCoreLib;");
                                    sw.WriteLine("using lual = Capstones.LuaLib.LuaAuxLib;");
                                    sw.WriteLine("using luae = Capstones.LuaLib.LuaLibEx;");
                                    sw.WriteLine();
                                    sw.WriteLine("namespace Capstones.LuaLib");
                                    sw.WriteLine("{");
                                    sw.WriteLine("    public static partial class LuaHubEx");
                                    sw.WriteLine("    {");
                                    foreach (var minfo in sorted)
                                    {
                                        var typepart = minfo.FullCSharpName.Replace(".", "");
                                        if (minfo.IsEnum)
                                        {
                                            sw.Write("        public class TypeHubPrecompiled_");
                                            sw.Write(typepart);
                                            sw.Write(" : Capstones.LuaLib.LuaTypeHub.TypeHubEnumPrecompiled<");
                                            sw.Write(minfo.FullCSharpName);
                                            sw.Write(">");
                                            sw.WriteLine();
                                            sw.WriteLine("        {");
                                            sw.WriteLine("            public override void RegPrecompiledStatic()");
                                            sw.WriteLine("            {");
                                            foreach (var ev in minfo.Desc["value"].Messages)
                                            {
                                                var frawname = ev["name"].String;
                                                var fname = ToCSharpEnumName(frawname);
                                                sw.Write("                _StaticFieldsIndex[\"");
                                                sw.Write(fname);
                                                sw.Write("\"] = new LuaMetaCallWithPrecompiled() { _Method = _StaticFieldsIndex[\"");
                                                sw.Write(fname);
                                                sw.Write("\"]._Method, _Precompiled = ___sgf_");
                                                sw.Write(fname);
                                                sw.Write(" };");
                                                sw.WriteLine();
                                                if (frawname != fname)
                                                {
                                                    sw.Write("                _StaticFieldsIndex[\"");
                                                    sw.Write(frawname);
                                                    sw.Write("\"] = new LuaMetaCallWithPrecompiled() { _Method = _StaticFieldsIndex[\"");
                                                    sw.Write(fname);
                                                    sw.Write("\"]._Method, _Precompiled = ___sgf_");
                                                    sw.Write(fname);
                                                    sw.Write(" };");
                                                    sw.WriteLine();
                                                }
                                            }
                                            sw.WriteLine("            }");
                                            foreach (var ev in minfo.Desc["value"].Messages)
                                            {
                                                var fname = ev["name"].String;
                                                fname = ToCSharpEnumName(fname);
                                                sw.Write("            private static readonly lua.CFunction ___sgf_");
                                                sw.Write(fname);
                                                sw.Write(" = new lua.CFunction(___sgm_");
                                                sw.Write(fname);
                                                sw.Write(");");
                                                sw.WriteLine();
                                                sw.WriteLine("            [AOT.MonoPInvokeCallback(typeof(lua.CFunction))]");
                                                sw.Write("            private static int ___sgm_");
                                                sw.Write(fname);
                                                sw.Write("(IntPtr l)");
                                                sw.WriteLine();
                                                sw.WriteLine("            {");
                                                sw.WriteLine("                try");
                                                sw.WriteLine("                {");
                                                sw.Write("                    var rv = ");
                                                sw.Write(minfo.FullCSharpName);
                                                sw.Write(".");
                                                sw.Write(fname);
                                                sw.Write(";");
                                                sw.WriteLine();
                                                sw.WriteLine("                    l.PushLua(rv);");
                                                sw.WriteLine("                    return 1;");
                                                sw.WriteLine("                }");
                                                sw.WriteLine("                catch (Exception exception)");
                                                sw.WriteLine("                {");
                                                sw.WriteLine("                    l.LogError(exception);");
                                                sw.WriteLine("                    return 0;");
                                                sw.WriteLine("                }");
                                                sw.WriteLine("            }");
                                            }
                                            sw.Write("            public override ");
                                            sw.Write(minfo.FullCSharpName);
                                            sw.Write(" ConvertFromNum(double val)");
                                            sw.WriteLine();
                                            sw.WriteLine("            {");
                                            sw.Write("                return (");
                                            sw.Write(minfo.FullCSharpName);
                                            sw.Write(")val;");
                                            sw.WriteLine();
                                            sw.WriteLine("            }");
                                            sw.Write("            public override double ConvertToNum(");
                                            sw.Write(minfo.FullCSharpName);
                                            sw.Write(" val)");
                                            sw.WriteLine();
                                            sw.WriteLine("            {");
                                            sw.WriteLine("                return (double)val;");
                                            sw.WriteLine("            }");
                                            sw.WriteLine("        }");

                                            sw.Write("        private static Capstones.LuaLib.LuaTypeHub.TypeHubCreator<TypeHubPrecompiled_");
                                            sw.Write(typepart);
                                            sw.Write("> ___tp_");
                                            sw.Write(typepart);
                                            sw.Write(" = new Capstones.LuaLib.LuaTypeHub.TypeHubCreator<TypeHubPrecompiled_");
                                            sw.Write(typepart);
                                            sw.Write(">(typeof(");
                                            sw.Write(minfo.FullCSharpName);
                                            sw.Write("));");
                                            sw.WriteLine();
                                            sw.Write("        public static void PushLua(this IntPtr l, ");
                                            sw.Write(minfo.FullCSharpName);
                                            sw.Write(" val)");
                                            sw.WriteLine();
                                            sw.WriteLine("        {");
                                            sw.Write("            ___tp_");
                                            sw.Write(typepart);
                                            sw.Write(".TypeHubSub.PushLua(l, val);");
                                            sw.WriteLine();
                                            sw.WriteLine("        }");
                                            sw.Write("        public static void GetLua(this IntPtr l, int index, out ");
                                            sw.Write(minfo.FullCSharpName);
                                            sw.Write(" val)");
                                            sw.WriteLine();
                                            sw.WriteLine("        {");
                                            sw.Write("            val = ___tp_");
                                            sw.Write(typepart);
                                            sw.Write(".TypeHubSub.GetLuaChecked(l, index);");
                                            sw.WriteLine();
                                            sw.WriteLine("        }");
                                        }
                                        else
                                        {
                                            sw.Write("        private static Capstones.LuaLib.LuaTypeHub.TypeHubCreator<Capstones.LuaExt.LuaProtobufBridge.TypeHubProtocolPrecompiled<");
                                            sw.Write(minfo.FullCSharpName);
                                            sw.Write(">> ___tp_");
                                            sw.Write(typepart);
                                            sw.Write(" = new LuaTypeHub.TypeHubCreator<Capstones.LuaExt.LuaProtobufBridge.TypeHubProtocolPrecompiled<");
                                            sw.Write(minfo.FullCSharpName);
                                            sw.Write(">>(typeof(");
                                            sw.Write(minfo.FullCSharpName);
                                            sw.Write("));");
                                            sw.WriteLine();
                                            sw.Write("        public static void PushLua(this IntPtr l, ");
                                            sw.Write(minfo.FullCSharpName);
                                            sw.Write(" val)");
                                            sw.WriteLine();
                                            sw.WriteLine("        {");
                                            sw.Write("            ___tp_");
                                            sw.Write(typepart);
                                            sw.Write(".TypeHubSub.PushLua(l, val);");
                                            sw.WriteLine();
                                            sw.WriteLine("        }");
                                            sw.Write("        public static void GetLua(this IntPtr l, int index, out ");
                                            sw.Write(minfo.FullCSharpName);
                                            sw.Write(" val)");
                                            sw.WriteLine();
                                            sw.WriteLine("        {");
                                            sw.Write("            val = Capstones.LuaExt.LuaProtobufBridge.TypeHubProtocolPrecompiled<");
                                            sw.Write(minfo.FullCSharpName);
                                            sw.Write(">.GetLuaChecked(l, index);");
                                            sw.WriteLine();
                                            sw.WriteLine("        }");
                                        }
                                    }
                                    sw.WriteLine("    }");
                                    sw.WriteLine("}");
                                }
                            }
                        }
                    }
                }
            }

            // Lua Strings
            using (var sw = PlatDependant.OpenWriteText(hubdir + "LuaProtobufBridge.LuaString.cs"))
            {
                sw.WriteLine("using System;");
                sw.WriteLine("using Capstones.LuaExt;");
                sw.WriteLine("using Capstones.LuaLib;");
                sw.WriteLine("using Capstones.LuaWrap;");
                sw.WriteLine("");
                sw.WriteLine("namespace Capstones.LuaExt");
                sw.WriteLine("{");
                sw.WriteLine("    public static partial class LuaProtobufBridge");
                sw.WriteLine("    {");
                foreach (var fieldKey in fieldKeys)
                {
                    sw.WriteLine("        public static readonly LuaString LS_" + fieldKey.Replace('.', '_') + " = new LuaString(\"" + fieldKey + "\");");
                }
                sw.WriteLine("    }");
                sw.WriteLine("}");
            }

            // Write LuaPrecompile Blacklist
            Func<string, string> GetMemberListName = null;
            GetMemberListName = mname =>
            {
                if (mname.StartsWith("."))
                {
                    mname = mname.Substring(1);
                }
                CapsNetworkEditor.ProtocolInfo info;
                if (!allmessagesinallfiles.TryGetValue(mname, out info))
                {
                    return CapsNetworkEditor.ToCSharpName(mname);
                }
                else
                {
                    if (!info.IsNested)
                    {
                        return info.FullCSharpName;
                    }
                    var sindex = info.FullCSharpName.LastIndexOf('.');
                    if (sindex < 0)
                    {
                        return info.FullCSharpName;
                    }
                    var parent = info.FullCSharpName.Substring(0, sindex);
                    var child = info.FullCSharpName.Substring(sindex + 1);
                    return GetMemberListName(parent) + "+" + child;
                }
            };
            using (var sw = PlatDependant.OpenWriteText(outputdir + "LuaPrecompile/MemberList.txt"))
            {
                foreach (var kvp in allmessagesinallfiles)
                {
                    sw.Write("--type ");
                    sw.WriteLine(GetMemberListName(kvp.Key));
                }
            }
        }
    }
}
#endif
