using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using Capstones.UnityEngineEx;
using System.IO;

namespace Capstones.Net
{
    public static class CONST
    {
        public const int MTU = 1400; // KCP default mtu.
        public const int MAX_MESSAGE_LENGTH = 1024 * 1024; // The longest message is limited to 1MB, that's big enough. If we got a message larger than that, we should treat it as an error.
        public const int MAX_SERVER_PENDING_CONNECTIONS = 100;
        public const int DEFAULT_TIMEOUT = 15000;
        public const int MAX_QUEUED_MESSAGE = 1000;
    }
}
