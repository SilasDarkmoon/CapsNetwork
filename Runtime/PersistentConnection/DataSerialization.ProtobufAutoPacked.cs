using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
#if UNITY_ENGINE || UNITY_5_3_OR_NEWER
using UnityEngine;
#endif
using Capstones.UnityEngineEx;
using System.IO;

using PlatDependant = Capstones.UnityEngineEx.PlatDependant;

namespace Capstones.Net
{
    public class ProtobufAutoPackedSplitter : DataSplitter<ProtobufAutoPackedSplitter>
    {
        public static readonly TemplateProtobufMessage MessageProto = new TemplateProtobufMessage()
        {
            { 1, "type", ProtobufNativeType.TYPE_FIXED32 },
            { 2, "flags", ProtobufNativeType.TYPE_FIXED32 },
            { 3, "seq", ProtobufNativeType.TYPE_FIXED32 },
            { 4, "sseq", ProtobufNativeType.TYPE_FIXED32 },
            { 5, "raw", ProtobufNativeType.TYPE_BYTES },
        };
        static ProtobufAutoPackedSplitter()
        {
            MessageProto.FinishBuild();
        }

        public ListSegment<byte> CurrentMessage;

        public ProtobufAutoPackedSplitter() { }
        public ProtobufAutoPackedSplitter(Stream input) : this()
        {
            Attach(input);
        }

#if UNITY_ENGINE || UNITY_5_3_OR_NEWER
        private InsertableStream _ReadBuffer = new NativeBufferStream();
#else
        private InsertableStream _ReadBuffer = new ArrayBufferStream();
#endif
        public override void ReadBlock()
        {
            var message = ProtobufEncoder.ReadRaw(CurrentMessage);
            message = message.ApplyTemplate(MessageProto);
            CurrentMessage = new ListSegment<byte>();

            _ReadBuffer.Clear();
            var raw = message["raw"].Bytes;
            if (raw != null)
            {
                _ReadBuffer.Write(raw, 0, raw.Length);
            }
            FireReceiveBlock(_ReadBuffer, _ReadBuffer.Count, message["type"], message["flags"], message["seq"], message["sseq"], null);
        }

        public override bool TryReadBlock()
        {
            if (CurrentMessage.List == null)
            {
                return false;
            }
            try
            {
                ReadBlock();
                return true;
            }
            catch (Exception e)
            {
                PlatDependant.LogError(e);
                return false;
            }
        }

        public override void OnReceiveData(byte[] data, int offset, int cnt)
        {
            CurrentMessage = new ListSegment<byte>(data, offset, cnt);
            base.OnReceiveData(data, offset, cnt);
        }
    }

    public class ProtobufAutoPackedComposer : DataComposer
    {
        public override void PrepareBlock(InsertableStream data, uint type, uint flags, uint seq, uint sseq, object exFlags)
        {
            byte[] raw = new byte[data.Count];
            data.CopyTo(raw, 0);
            ProtobufMessage message = new ProtobufMessage();
            message.ApplyTemplate(ProtobufAutoPackedSplitter.MessageProto);
            message["type"].Set(type);
            message["flags"].Set(flags);
            message["seq"].Set(seq);
            message["sseq"].Set(sseq);
            message["raw"].Set(raw);

            data.Clear();
            ProtobufEncoder.WriteRaw(message, data, 0);
        }
    }
}