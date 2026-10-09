using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MochiDay
{
    // Only changes this application's own window; no hooks, registry edits or desktop embedding.
    public static class WindowsWindow
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        delegate bool EnumProc(IntPtr hwnd, IntPtr param);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, IntPtr param);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int w, int h, uint flags);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd, int command);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool SetWindowText(IntPtr hwnd,string text);
        [DllImport("user32.dll")] static extern bool FlashWindowEx(ref FlashInfo info);
        [DllImport("user32.dll")] static extern bool MessageBeep(uint type);
        [StructLayout(LayoutKind.Sequential)] struct FlashInfo { public uint size; public IntPtr hwnd; public uint flags,count,timeout; }
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int left, top, right, bottom; }
        [StructLayout(LayoutKind.Sequential)] struct MonitorInfo { public int size; public NativeRect monitor, work; public uint flags; }
        static IntPtr Find()
        {
            IntPtr result = IntPtr.Zero; uint own = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            EnumWindows((h, p) => { uint pid; GetWindowThreadProcessId(h, out pid); if (pid == own && IsWindowVisible(h)) { result = h; return false; } return true; }, IntPtr.Zero);
            return result;
        }
#endif
        public static bool SetTopmost(bool enabled)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var hwnd = Find(); return hwnd != IntPtr.Zero && SetWindowPos(hwnd, new IntPtr(enabled ? -1 : -2), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
#else
            return false;
#endif
        }
        public static void Corner()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var hwnd = Find(); NativeRect r; var info = new MonitorInfo { size = Marshal.SizeOf(typeof(MonitorInfo)) };
            if (hwnd != IntPtr.Zero && GetWindowRect(hwnd, out r) && GetMonitorInfo(MonitorFromWindow(hwnd, 2), ref info))
                SetWindowPos(hwnd, IntPtr.Zero, Math.Max(info.work.left, info.work.right - (r.right-r.left)-18), Math.Max(info.work.top, info.work.bottom-(r.bottom-r.top)-18), 0, 0, 0x0001 | 0x0004 | 0x0010);
#endif
        }
        public static void Minimize()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var hwnd = Find(); if (hwnd != IntPtr.Zero) ShowWindow(hwnd, 6);
#endif
        }
        public static void Notify(bool sound)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var hwnd=Find();
            if(hwnd!=IntPtr.Zero) { var info=new FlashInfo { size=(uint)Marshal.SizeOf(typeof(FlashInfo)),hwnd=hwnd,flags=2,count=3,timeout=0 }; FlashWindowEx(ref info); }
            if(sound) MessageBeep(0);
#endif
        }
        public static void Title(string text)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var hwnd=Find(); if(hwnd!=IntPtr.Zero) SetWindowText(hwnd,text);
#endif
        }
    }
}
