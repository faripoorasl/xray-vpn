using System;
using System.Net;
using System.Runtime.InteropServices;

namespace XrayVpnApp.Utils;

/// <summary>
/// P/Invoke bindings for the official wintun.dll (WireGuard TUN driver).
/// Source: https://git.zx2c4.com/wintun
/// </summary>
internal static class WintunNative
{
    private const string DLL = "wintun.dll";

    [DllImport(DLL, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    public static extern IntPtr WintunCreateAdapter(
        [MarshalAs(UnmanagedType.LPWStr)] string name,
        [MarshalAs(UnmanagedType.LPWStr)] string tunnelType,
        [MarshalAs(UnmanagedType.LPStruct)] Guid? requestedGUID);

    [DllImport(DLL, CallingConvention = CallingConvention.StdCall)]
    public static extern void WintunCloseAdapter(IntPtr adapterHandle);

    [DllImport(DLL, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    public static extern IntPtr WintunStartSession(
        IntPtr adapterHandle,
        int capacity);

    [DllImport(DLL, CallingConvention = CallingConvention.StdCall)]
    public static extern void WintunEndSession(IntPtr sessionHandle);

    [DllImport(DLL, CallingConvention = CallingConvention.StdCall)]
    public static extern IntPtr WintunGetReadWaitEvent(IntPtr sessionHandle);

    [DllImport(DLL, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    public static extern IntPtr WintunReceivePacket(
        IntPtr sessionHandle,
        out int packetSize);

    [DllImport(DLL, CallingConvention = CallingConvention.StdCall)]
    public static extern void WintunReleaseReceivePacket(
        IntPtr sessionHandle,
        IntPtr packet);

    [DllImport(DLL, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    public static extern IntPtr WintunAllocateSendPacket(
        IntPtr sessionHandle,
        int packetSize);

    [DllImport(DLL, CallingConvention = CallingConvention.StdCall)]
    public static extern void WintunSendPacket(
        IntPtr sessionHandle,
        IntPtr packet);

    [DllImport(DLL, CallingConvention = CallingConvention.StdCall)]
    public static extern IntPtr WintunOpenAdapter(
        [MarshalAs(UnmanagedType.LPWStr)] string name);

    [DllImport(DLL, CallingConvention = CallingConvention.StdCall)]
    public static extern void WintunDeleteAdapter(IntPtr adapterHandle);

    [DllImport(DLL, CallingConvention = CallingConvention.StdCall)]
    public static extern bool WintunSetLogger(
        Delegate callback);

    public delegate void WintunLoggerDelegate(
        int level,
        long timestamp,
        [MarshalAs(UnmanagedType.LPWStr)] string message);
}

/// <summary>
/// Helper for native Windows IP Helper API (route table manipulation).
/// </summary>
internal static class IpHelperNative
{
    [DllImport("iphlpapi.dll", SetLastError = true)]
    public static extern int GetAdaptersInfo(IntPtr adapterInfo, ref int size);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    public static extern int GetIpForwardTable(IntPtr pIpForwardTable, ref int pdwSize, bool bOrder);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    public static extern int CreateIpForwardEntry(ref MIB_IPFORWARDROW pRoute);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    public static extern int DeleteIpForwardEntry(ref MIB_IPFORWARDROW pRoute);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    public static extern int SetInterfaceEntry(IntPtr row);

    [StructLayout(LayoutKind.Sequential)]
    public struct MIB_IPFORWARDROW
    {
        public uint dwForwardDest;
        public uint dwForwardMask;
        public uint dwForwardPolicy;
        public uint dwForwardNextHop;
        public uint dwForwardIfIndex;
        public uint dwForwardType;
        public uint dwForwardProto;
        public uint dwForwardAge;
        public uint dwForwardNextHopAS;
        public uint dwForwardMetric1;
        public uint dwForwardMetric2;
        public uint dwForwardMetric3;
        public uint dwForwardMetric4;
        public uint dwForwardMetric5;
    }

    [DllImport("dnsapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern uint DnsFlushResolverCache();

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool Wow64DisableWow64FsRedirection(ref IntPtr ptr);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool Wow64RevertWow64FsRedirection(IntPtr ptr);
}
