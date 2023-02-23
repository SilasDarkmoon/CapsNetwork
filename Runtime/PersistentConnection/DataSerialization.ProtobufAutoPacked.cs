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
        public ListSegment<byte> CurrentMessage;

        public ProtobufAutoPackedSplitter() { }
        public ProtobufAutoPackedSplitter(Stream input) : this()
        {
            Attach(input);
        }

        public override void ReadBlock()
        {
        }

        public override bool TryReadBlock()
        {
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
            ProtobufMessage message = new ProtobufMessage();
            message[1].Set(new ProtobufUnknowValue() { Raw = new ListSegment<byte>() });
        }
    }
}