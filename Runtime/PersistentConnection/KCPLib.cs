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
        }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int kcp_output(IntPtr buf, int len, Connection kcp, IntPtr user);

#if UNITY_ANDROID && !UNITY_EDITOR
        private static class ImportedPrivate
        {
            [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr kcp_create(uint conv, IntPtr user);
        }
        public static Connection kcp_create(uint conv, IntPtr user)
        {
            return (Connection)ImportedPrivate.kcp_create(conv, user);
        }
#else
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        public static extern Connection kcp_create(uint conv, IntPtr user);
#endif
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        public static extern void kcp_release(this Connection kcp);
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        public static extern void kcp_setoutput(this Connection kcp, kcp_output output);
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        public static extern int kcp_recv(this Connection kcp, byte[] buffer, int len);
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        public static extern int kcp_send(this Connection kcp, byte[] buffer, int len);
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        public static extern void kcp_update(this Connection kcp, uint current);
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint kcp_check(this Connection kcp, uint current);
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        public static extern int kcp_input(this Connection kcp, byte[] data, int size);
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        public static extern void kcp_flush(this Connection kcp);
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        public static extern int kcp_peeksize(this Connection kcp);
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        public static extern int kcp_setmtu(this Connection kcp, int mtu);
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        public static extern int kcp_wndsize(this Connection kcp, int sndwnd, int rcvwnd);
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        public static extern int kcp_waitsnd(this Connection kcp);
        [DllImport(LIB_PATH, CallingConvention = CallingConvention.Cdecl)]
        public static extern int kcp_nodelay(this Connection kcp, int nodelay, int interval, int resend, int nc);
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