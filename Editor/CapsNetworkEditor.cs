using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using Capstones.UnityEngineEx;
using Capstones.UnityEditorEx;
using Capstones.Net;

namespace Capstones.UnityEditorEx.Net
{
    public static class CapsNetworkEditor
    {
        [MenuItem("Net/Generate C# from Protobuf", priority = 100010)]
        public static void Generate_CS_From_Protobuf()
        {
            if (EditorUtility.DisplayDialog("提示", "该功能需要:\nLinux subsystem on Windows 10.\n&& Ubuntu distribute installed.\n&& installed make .etc and make and installed protoc", "准备好了", "还没有准备好"))
            {
                var workingdir = System.IO.Path.GetFullPath(".");
                var protosrcs = CapsModEditor.FindAssetsInMods("Protocols/Src/Combined.proto");
                foreach (var srcfile in protosrcs)
                {
                    var srcdir = System.IO.Path.GetDirectoryName(srcfile);
                    var protodir = System.IO.Path.GetDirectoryName(srcdir);
                    if (System.IO.Directory.Exists(protodir + "/Compiled"))
                    {
                        System.IO.Directory.Delete(protodir + "/Compiled", true);
                    }
                    var files = PlatDependant.GetAllFiles(srcdir);
                    foreach (var file in files)
                    {
                        if (file.EndsWith(".proto"))
                        {
                            var part = file.Substring(srcdir.Length, file.Length - srcdir.Length - ".proto".Length);
                            var dest = protodir + "/Compiled" + part + ".pb";
                            var destdir = System.IO.Path.GetDirectoryName(dest);
                            System.IO.Directory.CreateDirectory(destdir);
                            System.Diagnostics.ProcessStartInfo si = new System.Diagnostics.ProcessStartInfo("bash", "-c \"protoc -I./"+ srcdir.Replace('\\', '/') + " --csharp_out=./" + destdir.Replace('\\', '/') + " ./" + file.Replace('\\', '/') + " -o./" + dest.Replace('\\', '/') + "\"");
                            si.WorkingDirectory = workingdir;
                            CapsEditorUtils.ExecuteProcess(si);
                        }
                    }
                }
            }
        }

        public class AttributeInfo
        {
            public string Name;
            public List<string> Args;
        }
        public class ProtocolInfo
        {
            public int Index;
            public string Name;
            public string FullName;
            public string FullCSharpName;
            public bool IsEnum;
            public bool IsNested;
            public ProtobufMessage Desc;

            public int Line;
            public bool IgnoreReg;
            public int RegID;
            public List<AttributeInfo> Attributes;
        }
        public static AttributeInfo GetAttribute(this List<AttributeInfo> attrs, string attr)
        {
            if (attrs != null)
            {
                for (int i = 0; i < attrs.Count; ++i)
                {
                    if (attrs[i].Name == attr)
                    {
                        return attrs[i];
                    }
                }
            }
            return null;
        }
        public static bool ContainsAttribute(this List<AttributeInfo> attrs, string attr)
        {
            return GetAttribute(attrs, attr) != null;
        }
        public static string GetAttributeArg(this List<AttributeInfo> attrs, string attr, int index)
        {
            var info = GetAttribute(attrs, attr);
            if (info != null)
            {
                if (info.Args != null)
                {
                    if (index >= 0 && info.Args.Count > index)
                    {
                        return info.Args[index];
                    }
                }
            }
            return null;
        }
        public static void GetMessages(ProtobufMessage parent, bool isroot, bool isenum, string pre, Dictionary<string, ProtocolInfo> messages, string packageName)
        {
            var myname = parent["name"].String;
            var myfullname = pre + myname;
            var info = new ProtocolInfo()
            {
                Index = messages.Count,
                Name = myname,
                FullName = myfullname,
                FullCSharpName = ToCSharpName(packageName) + myfullname.Substring(packageName.Length),
                IsEnum = isenum,
                IsNested = !isroot,
                Desc = parent,
            };
            messages[myfullname] = info;

            if (!isenum)
            {
                var childpre = myfullname + ".";
                var subs = parent["nested_type"].Messages;
                for (int i = 0; i < subs.Count; ++i)
                {
                    var sub = subs[i];
                    GetMessages(sub, false, false, childpre, messages, packageName);
                }
                var enums = parent["enum_type"].Messages;
                for (int i = 0; i < enums.Count; ++i)
                {
                    var sub = enums[i];
                    GetMessages(sub, false, true, childpre, messages, packageName);
                }
            }
        }
        public static Dictionary<string, ProtocolInfo> GetAllMessages(ProtobufMessage mess_set)
        {
            Dictionary<string, ProtocolInfo> allmessages = new Dictionary<string, ProtocolInfo>();
            var files = mess_set["file"].Messages;
            for (int j = 0; j < files.Count; ++j)
            {
                var mess_file = files[j];
                var package = mess_file["package"].String;
                var rootpre = package + ".";
                var enums = mess_file["enum_type"].Messages;
                for (int i = 0; i < enums.Count; ++i)
                {
                    var sub = enums[i];
                    GetMessages(sub, true, true, rootpre, allmessages, package);
                }
                var messes = mess_file["message_type"].Messages;
                for (int i = 0; i < messes.Count; ++i)
                {
                    var sub = messes[i];
                    GetMessages(sub, true, false, rootpre, allmessages, package);
                }
            }
            return allmessages;
        }
        public static ProtocolInfo[] ParseExInfo(Dictionary<string, ProtocolInfo> allmessages, IList<string> srclines)
        {
            ProtocolInfo[] sortedMessages = new ProtocolInfo[allmessages.Count];
            allmessages.Values.CopyTo(sortedMessages, 0);
            Array.Sort(sortedMessages, (a, b) => a.Index - b.Index);
            for (int i = 0; i < sortedMessages.Length; ++i)
            {
                sortedMessages[i].Index = i;
            }

            // Match the messages to the line num.
            int lastIndex = 0;
            for (int j = 0; j < sortedMessages.Length; ++j)
            {
                var minfo = sortedMessages[j];
                if (!minfo.IsNested)
                {
                    minfo.Line = -1;
                    for (int i = lastIndex; i < srclines.Count; ++i)
                    {
                        var line = srclines[i];
                        if (!string.IsNullOrEmpty(line))
                        {
                            line = line.Trim();
                            if (minfo.IsEnum && line.StartsWith("enum"))
                            {
                                line = line.Substring("enum".Length).TrimStart();
                            }
                            else if (!minfo.IsEnum && line.StartsWith("message"))
                            {
                                line = line.Substring("message".Length).TrimStart();
                            }
                            else
                            {
                                continue;
                            }
                            if (line.StartsWith(minfo.Name))
                            {
                                if (line.Length == minfo.Name.Length || !char.IsLetterOrDigit(line[minfo.Name.Length]))
                                {
                                    minfo.Line = i;
                                    break;
                                }
                            }
                        }
                    }
                    if (minfo.Line >= 0)
                    {
                        lastIndex = minfo.Line + 1;
                    }
                }
            }
            for (int j = 0; j < sortedMessages.Length; ++j)
            {
                var minfo = sortedMessages[j];
                if (minfo.IsNested)
                {
                    minfo.Line = -1;
                    var parentName = minfo.FullName.Substring(0, minfo.FullName.Length - minfo.Name.Length - 1);
                    if (allmessages.ContainsKey(parentName))
                    {
                        var parent = allmessages[parentName];
                        var startIndex = 0;
                        var endIndex = srclines.Count - 1;
                        for (int i = parent.Index; i >= 0; --i)
                        {
                            var preinfo = sortedMessages[i];
                            if (preinfo.Line >= 0)
                            {
                                startIndex = preinfo.Line + 1;
                                break;
                            }
                        }
                        for (int i = parent.Index + 1; i < sortedMessages.Length; ++i)
                        {
                            var nxtinfo = sortedMessages[i];
                            if (nxtinfo.Line >= 0)
                            {
                                endIndex = nxtinfo.Line - 1;
                                break;
                            }
                        }
                        for (int i = startIndex; i <= endIndex; ++i)
                        {
                            var line = srclines[i];
                            if (!string.IsNullOrEmpty(line))
                            {
                                line = line.Trim();
                                if (minfo.IsEnum && line.StartsWith("enum"))
                                {
                                    line = line.Substring("enum".Length).TrimStart();
                                }
                                else if (!minfo.IsEnum && line.StartsWith("message"))
                                {
                                    line = line.Substring("message".Length).TrimStart();
                                }
                                else
                                {
                                    continue;
                                }
                                if (line.StartsWith(minfo.Name))
                                {
                                    if (line.Length == minfo.Name.Length || !char.IsLetterOrDigit(line[minfo.Name.Length]))
                                    {
                                        minfo.Line = i;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // find comment and Add attributes
            for (int j = 0; j < sortedMessages.Length; ++j)
            {
                var minfo = sortedMessages[j];
                if (minfo.Line >= 0)
                {
                    for (int i = minfo.Line - 1; i >= 0; --i)
                    {
                        var line = srclines[i];
                        if (line != null)
                        {
                            line = line.Trim();
                            if (line != "")
                            {
                                if (line.StartsWith("//"))
                                {
                                    line = line.Substring("//".Length);
                                    while (line.Length > 0 && line[0] == '/')
                                    {
                                        line = line.Substring(1);
                                    }
                                    line = line.TrimStart();
                                    if (line.StartsWith("["))
                                    {
                                        line = line.Substring(1);
                                        var endIndex = line.LastIndexOf("]");
                                        if (endIndex >= 0)
                                        {
                                            line = line.Substring(0, endIndex);
                                            // attibute found.
                                            var attrInfo = new AttributeInfo();
                                            var argstart = line.IndexOf("(");
                                            if (argstart >= 0)
                                            {
                                                attrInfo.Name = line.Substring(0, argstart);
                                                var argsend = line.LastIndexOf(")");
                                                if (argsend <= argstart)
                                                {
                                                    argsend = line.Length;
                                                }
                                                var argstxt = line.Substring(argstart + 1, argsend - argstart - 1);
                                                var args = argstxt.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                                                if (args != null && args.Length > 0)
                                                {
                                                    attrInfo.Args = new List<string>(args);
                                                }
                                            }
                                            else
                                            {
                                                attrInfo.Name = line;
                                            }
                                            if (minfo.Attributes == null)
                                            {
                                                minfo.Attributes = new List<AttributeInfo>();
                                            }
                                            minfo.Attributes.Add(attrInfo);
                                        }
                                    }
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            // check IgnoreReg and RegID
            int lastRegID = 0;
            HashSet<int> usedRegID = new HashSet<int>();
            for (int j = 0; j < sortedMessages.Length; ++j)
            {
                var minfo = sortedMessages[j];
                if (minfo.Attributes.ContainsAttribute("NoReg"))
                {
                    minfo.IgnoreReg = true;
                }
                else if (!minfo.IsEnum)
                {
                    var regid = minfo.Attributes.GetAttributeArg("RegID", 0).Convert<int>();
                    if (regid == 0)
                    {
                        regid = ++lastRegID;
                    }
                    else
                    {
                        lastRegID = regid;
                    }
                    if (regid <= 0)
                    {
                        PlatDependant.LogError("RegID of " + minfo.FullName + " is " + regid + " <= 0");
                    }
                    else
                    {
                        if (usedRegID.Add(regid))
                        {
                            minfo.RegID = regid;
                        }
                        else
                        {
                            PlatDependant.LogError("RegID of " + minfo.FullName + " is " + regid + " duplicated");
                        }
                    }
                }
            }
            return sortedMessages;
        }
        public static string ToCSharpName(string name)
        {
            if (name != null)
            {
                StringBuilder sb = new StringBuilder();
                int cur = 0;
                int split = -1;
                while (true)
                {
                    if (cur >= name.Length)
                    {
                        break;
                    }
                    sb.Append(char.ToUpper(name[cur++]));
                    if ((split = name.IndexOf('.', cur)) >= 0)
                    {
                        sb.Append(name, cur, split - cur + 1);
                        cur = split + 1;
                    }
                    else
                    {
                        sb.Append(name, cur, name.Length - cur);
                        break;
                    }
                }
                return sb.ToString();
            }
            return null;
        }
        [MenuItem("Net/Generate Protocols' Reg", priority = 100020)]
        public static void Generate_Protocols_Reg()
        {
            Dictionary<string, ProtocolInfo> allmessagesinallfiles = new Dictionary<string, ProtocolInfo>();

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
                            var allmessages = GetAllMessages(mess_set);
                            var sorted = ParseExInfo(allmessages, txtcontent);
                            allmessagesinallfiles.Merge(allmessages);
                            if (sorted.Length > 0)
                            {
                                using (var sw = PlatDependant.OpenWriteText(compdir + part + ".reg.cs"))
                                {
                                    var sbFileNamePart = new StringBuilder();
                                    sbFileNamePart.Append(part.Replace('\\', '_').Replace('/', '_').Replace('.', '_'));
                                    sbFileNamePart.Append("_");
                                    sbFileNamePart.Append(sorted[0].FullCSharpName.Replace('.', '_'));

                                    sw.WriteLine("namespace Capstones.Net");
                                    sw.WriteLine("{");
                                    sw.WriteLine("#if UNITY_ENGINE || UNITY_5_3_OR_NEWER");
                                    sw.Write("    public static class ProtobufReaderAndWriterReg");
                                    sw.WriteLine(sbFileNamePart);
                                    sw.WriteLine("#else");
                                    sw.WriteLine("    public partial class ProtobufReaderAndWriter");
                                    sw.WriteLine("#endif");
                                    sw.WriteLine("    {");
                                    for (int i = 0; i < sorted.Length; ++i)
                                    {
                                        var minfo = sorted[i];
                                        if (!minfo.IsEnum && minfo.RegID > 0)
                                        {
                                            sw.Write("        private static ProtobufReaderAndWriter.RegisteredType _Reg_");
                                            sw.Write(minfo.FullCSharpName.Replace('.', '_'));
                                            sw.Write(" = new ProtobufReaderAndWriter.RegisteredType(");
                                            sw.Write(minfo.RegID);
                                            sw.Write(", typeof(");
                                            sw.Write(minfo.FullCSharpName);
                                            sw.Write("), ");
                                            sw.Write(minfo.FullCSharpName);
                                            sw.Write(".Parser);");
                                            sw.WriteLine();
                                        }
                                    }
                                    sw.WriteLine("");
                                    sw.Write("        private static void AOT_ProtocEnums");
                                    sw.Write(sbFileNamePart);
                                    sw.WriteLine("()");
                                    sw.WriteLine("        {");
                                    for (int i = 0; i < sorted.Length; ++i)
                                    {
                                        var minfo = sorted[i];
                                        if (minfo.IsEnum)
                                        {
                                            sw.Write("            Google.Protobuf.Reflection.FileDescriptor.ForceReflectionInitialization<");
                                            sw.Write(minfo.FullCSharpName);
                                            sw.Write(">();");
                                            sw.WriteLine();
                                        }
                                    }
                                    sw.WriteLine("        }");
                                    sw.WriteLine("");
                                    sw.WriteLine("#if UNITY_EDITOR");
                                    sw.WriteLine("        [UnityEditor.InitializeOnLoadMethod]");
                                    sw.WriteLine("#endif");
                                    sw.WriteLine("#if UNITY_ENGINE || UNITY_EDITOR || UNITY_5_3_OR_NEWER");
                                    sw.WriteLine("        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]");
                                    sw.WriteLine("        public static void Init()");
                                    sw.WriteLine("        {");
                                    sw.WriteLine("        }");
                                    sw.WriteLine("#endif");
                                    sw.WriteLine("    }");
                                    sw.WriteLine("}");
                                }
                            }
                        }
                    }
                }
            }

            // FrameSync Message Reg
            var mod = CapsEditorUtils.__MOD__;
            var outputdir = "Assets/Mods/" + mod + "/Protocols/";
            using (var sw = PlatDependant.OpenWriteText(outputdir + "OperationTypeReg.cs"))
            {
                sw.WriteLine("using System;");
                sw.WriteLine("using System.Collections.Generic;");
                sw.WriteLine("");
                sw.WriteLine("namespace Capstones.Net.FrameSync");
                sw.WriteLine("{");
                sw.WriteLine("#if UNITY_ENGINE || UNITY_5_3_OR_NEWER");
                sw.WriteLine("    public static class OperationTypeReg");
                sw.WriteLine("#else");
                sw.WriteLine("    public static partial class OperationType");
                sw.WriteLine("#endif");
                sw.WriteLine("    {");
                sw.WriteLine("        private static bool _Done;");
                sw.WriteLine("");
                sw.WriteLine("#if UNITY_ENGINE || UNITY_5_3_OR_NEWER");
                sw.WriteLine("#if UNITY_EDITOR");
                sw.WriteLine("        [UnityEditor.InitializeOnLoadMethod]");
                sw.WriteLine("#endif");
                sw.WriteLine("        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]");
                sw.WriteLine("        public static void RegOperationType()");
                sw.WriteLine("#else");
                sw.WriteLine("        static OperationType()");
                sw.WriteLine("#endif");
                sw.WriteLine("        {");
                sw.WriteLine("            if (!_Done)");
                sw.WriteLine("            {");
                sw.WriteLine("                RegOperationTypeImp();");
                sw.WriteLine("                _Done = true;");
                sw.WriteLine("            }");
                sw.WriteLine("        }");
                sw.WriteLine("");
                sw.WriteLine("        private static void RegOperationTypeImp()");
                sw.WriteLine("        {");

                ProtocolInfo beginInfo = null;
                ProtocolInfo tickInfo = null;

                foreach (var kvp in allmessagesinallfiles)
                {
                    var minfo = kvp.Value;
                    if (minfo.Attributes != null)
                    {
                        if (minfo.Attributes.ContainsAttribute("FrameSyncBegin"))
                        {
                            beginInfo = minfo;
                            sw.Write("            OperationType.FrameSyncBeginProtocol = typeof(");
                            sw.Write(minfo.FullCSharpName);
                            sw.Write(");");
                            sw.WriteLine();
                        }
                        else if (minfo.Attributes.ContainsAttribute("FrameSyncEnd"))
                        {
                            sw.Write("            OperationType.FrameSyncEndProtocol = typeof(");
                            sw.Write(minfo.FullCSharpName);
                            sw.Write(");");
                            sw.WriteLine();
                        }
                        else if (minfo.Attributes.ContainsAttribute("FrameSyncTick"))
                        {
                            tickInfo = minfo;
                            sw.Write("            OperationType.FrameSyncTickProtocol = typeof(");
                            sw.Write(minfo.FullCSharpName);
                            sw.Write(");");
                            sw.WriteLine();
                        }
                        else if (minfo.Attributes.ContainsAttribute("FrameSync"))
                        {
                            sw.Write("            OperationType.FrameSyncProtocols.Add(typeof(");
                            sw.Write(minfo.FullCSharpName);
                            sw.Write("));");
                            sw.WriteLine();
                        }
                        else if (minfo.Attributes.ContainsAttribute("FrameSyncReq"))
                        {
                            sw.Write("            OperationType.FrameSyncReqProtocols.Add(typeof(");
                            sw.Write(minfo.FullCSharpName);
                            sw.Write("));");
                            sw.WriteLine();
                        }
                    }
                }

                sw.WriteLine("            // Delegates");
                if (beginInfo != null)
                {
                    var fields = beginInfo.Desc["field"].Messages;
                    for (int i = 0; i < fields.Count; ++i)
                    {
                        var field = fields[i];
                        var name = field["name"].String;
                        if (name == "Interval" || name == "interval")
                        {
                            sw.Write("            OperationType.FuncGetFrameSyncBeginInterval = obj => (int)((");
                            sw.Write(beginInfo.FullCSharpName);
                            sw.Write(")obj).Interval;");
                            sw.WriteLine();
                        }
                        if (name == "Index" || name == "index")
                        {
                            sw.Write("            OperationType.FuncGetFrameSyncBeginIndex = obj => (int)((");
                            sw.Write(beginInfo.FullCSharpName);
                            sw.Write(")obj).Index;");
                            sw.WriteLine();
                        }
                        if (name == "Time" || name == "time")
                        {
                            sw.Write("            OperationType.FuncGetFrameSyncBeginTime = obj => (int)((");
                            sw.Write(beginInfo.FullCSharpName);
                            sw.Write(")obj).Time;");
                            sw.WriteLine();
                        }
                    }
                }
                if (tickInfo != null)
                {
                    var fields = tickInfo.Desc["field"].Messages;
                    for (int i = 0; i < fields.Count; ++i)
                    {
                        var field = fields[i];
                        var name = field["name"].String;
                        if (name == "Interval" || name == "interval")
                        {
                            sw.Write("            OperationType.FuncGetFrameSyncTickInterval = obj => (int)((");
                            sw.Write(tickInfo.FullCSharpName);
                            sw.Write(")obj).Interval;");
                            sw.WriteLine();
                        }
                        if (name == "Time" || name == "time")
                        {
                            sw.Write("            OperationType.FuncGetFrameSyncTickTime = obj => (int)((");
                            sw.Write(tickInfo.FullCSharpName);
                            sw.Write(")obj).Time;");
                            sw.WriteLine();
                        }
                    }
                }

                sw.WriteLine("        }");
                sw.WriteLine("    }");
                sw.WriteLine("}");
            }
        }

        [MenuItem("Net/Generate All", priority = 100110)]
        public static void Generate_All()
        {
            Generate_CS_From_Protobuf();
            Generate_Protocols_Reg();
#if MOD_CAPSLUA_V2
            LuaBridgeGenerator.Generate_Lua_Data_Bridge();
#endif

        }
    }
}