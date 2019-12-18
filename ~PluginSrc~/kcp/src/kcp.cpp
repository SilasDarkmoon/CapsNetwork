#include "ikcp.h"
#include "kcp.h"
#if TARGET_OS_IPHONE
#define UNITY_INTERFACE_API
#define UNITY_INTERFACE_EXPORT
#else
#include "IUnityInterface.h"
#endif

typedef int(*ikcpcb_output)(const char *buf, int len, ikcpcb *kcp, void *user);

typedef int(*kcpcb_output)(const char *buf, int len, void *kcp, void *user);

extern "C"
{
    // create a new kcp control object, 'conv' must equal in two endpoint
    // from the same connection. 'user' will be passed to the output callback
    // output callback can be setup like this: 'kcp->output = my_udp_output'
    UNITY_INTERFACE_EXPORT void* kcp_create(IUINT32 conv, void* user)
    {
        return ikcp_create(conv, user);
    }

    // release kcp control object
    UNITY_INTERFACE_EXPORT void kcp_release(void* kcp)
    {
        ikcp_release((ikcpcb*)kcp);
    }

    // set output callback, which will be invoked by kcp
    UNITY_INTERFACE_EXPORT void kcp_setoutput(void* kcp, kcpcb_output output)
    {
        ikcp_setoutput((ikcpcb*)kcp, (ikcpcb_output)output);
    }

    // user/upper level recv: returns size, returns below zero for EAGAIN
    UNITY_INTERFACE_EXPORT int kcp_recv(void* kcp, char *buffer, int len)
    {
        return ikcp_recv((ikcpcb*)kcp, buffer, len);
    }

    // user/upper level send, returns below zero for error
    UNITY_INTERFACE_EXPORT int kcp_send(void* kcp, const char *buffer, int len)
    {
        return ikcp_send((ikcpcb*)kcp, buffer, len);
    }

    // update state (call it repeatedly, every 10ms-100ms), or you can ask 
    // ikcp_check when to call it again (without ikcp_input/_send calling).
    // 'current' - current timestamp in millisec. 
    UNITY_INTERFACE_EXPORT void kcp_update(void* kcp, IUINT32 current)
    {
        ikcp_update((ikcpcb*)kcp, current);
    }

    // Determine when should you invoke ikcp_update:
    // returns when you should invoke ikcp_update in millisec, if there 
    // is no ikcp_input/_send calling. you can call ikcp_update in that
    // time, instead of call update repeatly.
    // Important to reduce unnacessary ikcp_update invoking. use it to 
    // schedule ikcp_update (eg. implementing an epoll-like mechanism, 
    // or optimize ikcp_update when handling massive kcp connections)
    UNITY_INTERFACE_EXPORT IUINT32 kcp_check(void* kcp, IUINT32 current)
    {
        return ikcp_check((ikcpcb*)kcp, current);
    }

    // when you received a low level packet (eg. UDP packet), call it
    UNITY_INTERFACE_EXPORT int kcp_input(void* kcp, const char *data, long size)
    {
        return ikcp_input((ikcpcb*)kcp, data, size);
    }

    // flush pending data
    UNITY_INTERFACE_EXPORT void kcp_flush(void* kcp)
    {
        ikcp_flush((ikcpcb*)kcp);
    }

    // check the size of next message in the recv queue
    UNITY_INTERFACE_EXPORT int kcp_peeksize(void* kcp)
    {
        return ikcp_peeksize((ikcpcb*)kcp);
    }

    // change MTU size, default is 1400
    UNITY_INTERFACE_EXPORT int kcp_setmtu(void* kcp, int mtu)
    {
        return ikcp_setmtu((ikcpcb*)kcp, mtu);
    }

    // set maximum window size: sndwnd=32, rcvwnd=32 by default
    UNITY_INTERFACE_EXPORT int kcp_wndsize(void* kcp, int sndwnd, int rcvwnd)
    {
        return ikcp_wndsize((ikcpcb*)kcp, sndwnd, rcvwnd);
    }

    // get how many packet is waiting to be sent
    UNITY_INTERFACE_EXPORT int kcp_waitsnd(void* kcp)
    {
        return ikcp_waitsnd((ikcpcb*)kcp);
    }

    // fastest: ikcp_nodelay(kcp, 1, 20, 2, 1)
    // nodelay: 0:disable(default), 1:enable
    // interval: internal update timer interval in millisec, default is 100ms 
    // resend: 0:disable fast resend(default), 1:enable fast resend
    // nc: 0:normal congestion control(default), 1:disable congestion control
    UNITY_INTERFACE_EXPORT int kcp_nodelay(void* kcp, int nodelay, int interval, int resend, int nc)
    {
        return ikcp_nodelay((ikcpcb*)kcp, nodelay, interval, resend, nc);
    }


    //void ikcp_log(ikcpcb *kcp, int mask, const char *fmt, ...);

    //// setup allocator
    //void ikcp_allocator(void* (*new_malloc)(size_t), void(*new_free)(void*));

    //// read conv
    //IUINT32 ikcp_getconv(const void *ptr);

    UNITY_INTERFACE_EXPORT int kcp_setminrto(void* kcp, int rto)
    {
        ((ikcpcb*)kcp)->rx_minrto = rto;
        return 0;
    }

	UNITY_INTERFACE_EXPORT void kcp_memmove(void* dst, const void* src, int cnt)
	{
		ikcp_memmove(dst, src, cnt);
	}
}

#if !TARGET_OS_IPHONE
typedef void* (*fn_kcp_create)(unsigned long conv, void* user);
typedef void (*fn_kcp_release)(void* kcp);
typedef void (*fn_kcp_setoutput)(void* kcp, kcpcb_output output);
typedef int (*fn_kcp_recv)(void* kcp, char *buffer, int len);
typedef int (*fn_kcp_send)(void* kcp, const char *buffer, int len);
typedef void (*fn_kcp_update)(void* kcp, unsigned long current);
typedef unsigned long (*fn_kcp_check)(void* kcp, unsigned long current);
typedef int (*fn_kcp_input)(void* kcp, const char *data, long size);
typedef void (*fn_kcp_flush)(void* kcp);
typedef int (*fn_kcp_peeksize)(void* kcp);
typedef int (*fn_kcp_setmtu)(void* kcp, int mtu);
typedef int (*fn_kcp_wndsize)(void* kcp, int sndwnd, int rcvwnd);
typedef int (*fn_kcp_waitsnd)(void* kcp);
typedef int (*fn_kcp_nodelay)(void* kcp, int nodelay, int interval, int resend, int nc);
typedef int (*fn_kcp_setminrto)(void* kcp, int rto);

struct KCPInterface : IUnityInterface
{
    fn_kcp_create       kcp_create;
    fn_kcp_release      kcp_release;
    fn_kcp_setoutput    kcp_setoutput;
    fn_kcp_recv         kcp_recv;
    fn_kcp_send         kcp_send;
    fn_kcp_update       kcp_update;
    fn_kcp_check        kcp_check;
    fn_kcp_input        kcp_input;
    fn_kcp_flush        kcp_flush;
    fn_kcp_peeksize     kcp_peeksize;
    fn_kcp_setmtu       kcp_setmtu;
    fn_kcp_wndsize      kcp_wndsize;
    fn_kcp_waitsnd      kcp_waitsnd;
    fn_kcp_nodelay      kcp_nodelay;
    fn_kcp_setminrto    kcp_setminrto;
};

static KCPInterface l_KCPInterface;

extern "C"
{
    void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* unityInterfaces)
    {
        //UnityInterfaceGUID guidkcp(0x71760E6BFC9541F4UL, 0xB7A8F19708CA5A75UL);

        l_KCPInterface.kcp_create = (fn_kcp_create)ikcp_create;
        l_KCPInterface.kcp_release = (fn_kcp_release)ikcp_release;
        l_KCPInterface.kcp_setoutput = (fn_kcp_setoutput)ikcp_setoutput;
        l_KCPInterface.kcp_recv = (fn_kcp_recv)ikcp_recv;
        l_KCPInterface.kcp_send = (fn_kcp_send)ikcp_send;
        l_KCPInterface.kcp_update = (fn_kcp_update)ikcp_update;
        l_KCPInterface.kcp_check = (fn_kcp_check)ikcp_check;
        l_KCPInterface.kcp_input = (fn_kcp_input)ikcp_input;
        l_KCPInterface.kcp_flush = (fn_kcp_flush)ikcp_flush;
        l_KCPInterface.kcp_peeksize = (fn_kcp_peeksize)ikcp_peeksize;
        l_KCPInterface.kcp_setmtu = (fn_kcp_setmtu)ikcp_setmtu;
        l_KCPInterface.kcp_wndsize = (fn_kcp_wndsize)ikcp_wndsize;
        l_KCPInterface.kcp_waitsnd = (fn_kcp_waitsnd)ikcp_waitsnd;
        l_KCPInterface.kcp_nodelay = (fn_kcp_nodelay)ikcp_nodelay;
        l_KCPInterface.kcp_setminrto = (fn_kcp_setminrto)kcp_setminrto;

        //unityInterfaces->RegisterInterface(guidkcp, &l_KCPInterface);
        unityInterfaces->RegisterInterfaceSplit(0x71760E6BFC9541F4UL, 0xB7A8F19708CA5A75UL, &l_KCPInterface);
    }
}
#endif
