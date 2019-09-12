using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using Unity.Collections.Concurrent;
using Capstones.UnityEngineEx;

namespace Capstones.Net
{
    public static class IPAddressInfo
    {
        private static IPAddress[] _LocalIPv4Addresses;
        public static IPAddress[] LocalIPv4Addresses { get { return _LocalIPv4Addresses; } }
        private static IPAddress[] _LocalIPv6Addresses;
        public static IPAddress[] LocalIPv6Addresses { get { return _LocalIPv6Addresses; } }

        private struct IPAddressDetail
        {
            public IPAddress Address;
            public int InterfaceIndex;
        }
        private static Dictionary<IPAddress, IPAddressDetail> _LocalAddressDetails;

        public static int GetInterfaceIndex(IPAddress address)
        {
            var details = _LocalAddressDetails;
            if (details != null)
            {
                IPAddressDetail detail;
                if (details.TryGetValue(address, out detail))
                {
                    return detail.InterfaceIndex;
                }
            }
            return 0;
        }

        public static IPAddress IPv4MulticastAddress = IPAddress.Parse("225.36.0.9");
        public static IPAddress IPv6MulticastAddressOrganization = IPAddress.Parse("ff18::3609");
        public static IPAddress IPv6MulticastAddressSiteLocal = IPAddress.Parse("ff15::3609");
        public static IPAddress IPv6MulticastAddressLinkLocal = IPAddress.Parse("ff12::3609");
        public static string IPv4MulticastAddressVal = "225.36.0.9";
        public static string IPv6MulticastAddressOrganizationVal = "ff18::3609";
        public static string IPv6MulticastAddressSiteLocalVal = "ff15::3609";
        public static string IPv6MulticastAddressLinkLocalVal = "ff12::3609";

        public static IPAddress IPv6MulticastAddress
        {
            get
            {
                bool global = false;
                bool site = false;
                var addrs = _LocalIPv6Addresses;
                if (addrs != null)
                {
                    for (int i = 0; i < addrs.Length; ++i)
                    {
                        var addr = addrs[i];
                        if (!addr.IsIPv6LinkLocal && !addr.IsIPv6SiteLocal)
                        {
                            global = true;
                            break;
                        }
                        else if (addr.IsIPv6SiteLocal)
                        {
                            site = true;
                        }
                    }
                }
                if (global)
                {
                    return IPv6MulticastAddressOrganization;
                }
                else if (site)
                {
                    return IPv6MulticastAddressSiteLocal;
                }
                else
                {
                    return IPv6MulticastAddressLinkLocal;
                }
            }
        }
        public static string IPv6MulticastAddressVal
        {
            get
            {
                bool global = false;
                bool site = false;
                var addrs = _LocalIPv6Addresses;
                if (addrs != null)
                {
                    for (int i = 0; i < addrs.Length; ++i)
                    {
                        var addr = addrs[i];
                        if (!addr.IsIPv6LinkLocal && !addr.IsIPv6SiteLocal)
                        {
                            global = true;
                            break;
                        }
                        else if (addr.IsIPv6SiteLocal)
                        {
                            site = true;
                        }
                    }
                }
                if (global)
                {
                    return IPv6MulticastAddressOrganizationVal;
                }
                else if (site)
                {
                    return IPv6MulticastAddressSiteLocalVal;
                }
                else
                {
                    return IPv6MulticastAddressLinkLocalVal;
                }
            }
        }

        static IPAddressInfo()
        {
            Refresh();
        }

        public static void Refresh()
        {
            if (_RefreshWorkDone.WaitOne(0))
            {
                RefreshWork();
                _RefreshWorkDone.Set();
            }
            else
            {
                _RefreshWorkDone.WaitOne();
                _RefreshWorkDone.Set();
            }
        }
        private static AutoResetEvent _RefreshWorkDone = new AutoResetEvent(true);
        private static void RefreshWork()
        {
            List<IPAddress> ipv4 = new List<IPAddress>();
            List<IPAddress> ipv6 = new List<IPAddress>();
            Dictionary<IPAddress, IPAddressDetail> details = new Dictionary<IPAddress, IPAddressDetail>();
            try
            {
                var nis = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
                for (int i = 0; i < nis.Length; ++i)
                {
                    try
                    {
                        var ni = nis[i];
                        if (ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                        {
                            var ipps = ni.GetIPProperties();
                            if (ipps != null && ipps.UnicastAddresses != null)
                            {
                                int index4 = 0;
                                int index6 = 0;
                                try
                                {
                                    var ipv4p = ipps.GetIPv4Properties();
                                    if (ipv4p != null)
                                    {
                                        index4 = ipv4p.Index;
                                    }
                                }
                                catch { }
                                try
                                {
                                    var ipv6p = ipps.GetIPv6Properties();
                                    if (ipv6p != null)
                                    {
                                        index6 = ipv6p.Index;
                                    }
                                }
                                catch { }
                                for (int j = 0; j < ipps.UnicastAddresses.Count; ++j)
                                {
                                    try
                                    {
                                        var addr = ipps.UnicastAddresses[j];
                                        if (addr != null && addr.Address != null && !IPAddress.IsLoopback(addr.Address))
                                        {
                                            if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                                            {
                                                ipv4.Add(addr.Address);
                                                if (index4 != 0 || index6 != 0)
                                                {
                                                    details[addr.Address] = new IPAddressDetail() { Address = addr.Address, InterfaceIndex = index4 == 0 ? index6 : index4 };
                                                }
                                            }
                                            else if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                                            {
                                                ipv6.Add(addr.Address);
                                                if (index4 != 0 || index6 != 0)
                                                {
                                                    details[addr.Address] = new IPAddressDetail() { Address = addr.Address, InterfaceIndex = index6 == 0 ? index4 : index6 };
                                                }
                                            }
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
            _LocalIPv4Addresses = ipv4.ToArray();
            _LocalIPv6Addresses = ipv6.ToArray();
            _LocalAddressDetails = details;
        }
    }
}
