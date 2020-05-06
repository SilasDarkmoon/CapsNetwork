using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Capstones.Net
{
    public static class KCPLib
    {
#if UNITY_IPHONE && !UNITY_EDITOR
        public const string LIB_PATH = "__Internal";
#else
        public const string LIB_PATH = "kcp";
#endif
        [StructLayout(LayoutKind.Sequential)]
        public struct Connection
        {
            private IntPtr _Handle;

            public static explicit operator Connection(IntPtr handle)
            {
                return new Connection() { _Handle = handle };
            }
            public static implicit operator IntPtr(Connection con)
            {
                return con._Handle;
            }

            public static Connection Create(uint conv, IntPtr user)
            {
                return kcp_create(conv, user);
            }
            public void Release()
            {
                kcp_release(this);
                _Handle = IntPtr.Zero;
            }
            public void SetOutput(kcp_output output)
            {
                if (_Handle != IntPtr.Zero)
                {
                    kcp_setoutput(this, output);
                }
            }
            public int Receive(byte[] buffer, int len)
            {
                if (_Handle != IntPtr.Zero)
                {
                    return kcp_recv(this, buffer, len);
                }
                return -100;
            }
            public int Send(byte[] buffer, int len)
            {
                if (_Handle != IntPtr.Zero)
                {
                    return kcp_send(this, buffer, len);
                }
                return -100;
            }
            public void Update(uint current)
            {
                if (_Handle != IntPtr.Zero)
                {
                    kcp_update(this, current);
                }
            }
            public uint Check(uint current)
            {
                if (_Handle != IntPtr.Zero)
                {
                    return kcp_check(this, current);
                }
                return current;
            }
            public int Input(byte[] data, int size)
            {
#if DEBUG_PVP
                var sb = new System.Text.StringBuilder();
                sb.Append("KCP Input ");
                sb.Append(size);
                sb.Append(" bytes to ");
                sb.Append(_Handle);
                //sb.Append(":");
                //for (int i = 0; i < size; ++i)
                //{
                //    if (i % 16 == 0)
                //    {
                //        sb.AppendLine();
                //    }
                //    else
                //    {
                //        if (i % 4 == 0)
                //        {
                //            sb.Append(" ");
                //        }
                //        if (i % 8 == 0)
                //        {
                //            sb.Append(" ");
                //        }
                //    }
                //    sb.Append(data[i].ToString("X2"));
                //    sb.Append(" ");
                //}
                UnityEngineEx.PlatDependant.LogInfo(sb);
#endif
                if (_Handle != IntPtr.Zero)
                {
                    return kcp_input(this, data, size);
                }
                return -100;
            }
            public void Flush()
            {
                if (_Handle != IntPtr.Zero)
                {
                    kcp_flush(this);
                }
            }
            public int PeekSize()
            {
                if (_Handle != IntPtr.Zero)
                {
                    return kcp_peeksize(this);
                }
                return -100;
            }
            public int SetMTU(int mtu)
            {
                if (_Handle != IntPtr.Zero)
                {
                    return kcp_setmtu(this, mtu);
                }
                return -100;
            }
            public int WndSize(int sndwnd, int rcvwnd)
            {
                if (_Handle != IntPtr.Zero)
                {
                    return kcp_wndsize(this, sndwnd, rcvwnd);
                }
                return 0;
            }
            public int WaitSnd()
            {
                if (_Handle != IntPtr.Zero)
                {
                    return kcp_waitsnd(this);
                }
                return 0;
            }
            public int NoDelay(int nodelay, int interval, int resend, int nc)
            {
                if (_Handle != IntPtr.Zero)
                {
                    return kcp_nodelay(this, nodelay, interval, resend, nc);
                }
                return 0;
            }
        }
        public static Connection CreateConnection(uint conv, IntPtr user)
        {
            return Connection.Create(conv, user);
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int kcp_output(IntPtr buf, int len, Connection kcp, IntPtr user);

#if UNITY_ANDROID && !UNITY_EDITOR
        private static class ImportedPrivate
        {
            [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr kcp_create(uint conv, IntPtr user);
        }
        private static Connection kcp_create(uint conv, IntPtr user)
        {
            return (Connection)ImportedPrivate.kcp_create(conv, user);
        }
#else
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        private static extern Connection kcp_create(uint conv, IntPtr user);
#endif
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        private static extern void kcp_release(this Connection kcp);
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        private static extern void kcp_setoutput(this Connection kcp, kcp_output output);
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        private static extern int kcp_recv(this Connection kcp, byte[] buffer, int len);
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        private static extern int kcp_send(this Connection kcp, byte[] buffer, int len);
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        private static extern void kcp_update(this Connection kcp, uint current);
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint kcp_check(this Connection kcp, uint current);
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        private static extern int kcp_input(this Connection kcp, byte[] data, int size);
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        private static extern void kcp_flush(this Connection kcp);
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        private static extern int kcp_peeksize(this Connection kcp);
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        private static extern int kcp_setmtu(this Connection kcp, int mtu);
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        private static extern int kcp_wndsize(this Connection kcp, int sndwnd, int rcvwnd);
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        private static extern int kcp_waitsnd(this Connection kcp);
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        private static extern int kcp_nodelay(this Connection kcp, int nodelay, int interval, int resend, int nc);
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        public static extern void kcp_memmove(IntPtr dst, IntPtr src, int cnt);

#if !UNITY_ENGINE && !UNITY_5_3_OR_NEWER
        static KCPLib()
        {
            UnityEngineEx.PluginManager.LoadLib(LIB_PATH);
        }
#endif
    }
}