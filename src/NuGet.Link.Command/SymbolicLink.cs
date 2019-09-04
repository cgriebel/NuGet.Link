using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Win32.SafeHandles;

namespace NuGet.Link.Command
{
    public static class SymbolicLink
    {
        private static class Unix
        {
            public static IOException Failure(string message)
            {
                return new IOException($"{message}: {Marshal.PtrToStringAnsi(strerror(Marshal.GetLastWin32Error()))}");
            }

            [DllImport("libc")]
            public static extern IntPtr strerror(int errnum);

            [DllImport("libc", SetLastError = true)]
            public static extern int symlink([MarshalAs(UnmanagedType.LPStr)] string target, [MarshalAs(UnmanagedType.LPStr)] string path);

            [DllImport("libc", SetLastError = true)]
            public static extern IntPtr readlink([MarshalAs(UnmanagedType.LPStr)] string path, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] target, UIntPtr targetlen);

            [DllImport("libc", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.LPStr)]
            public static extern string realpath([MarshalAs(UnmanagedType.LPStr)] string path, IntPtr buf);
        }

        private static class Win32
        {
            public static IOException Failure(string message)
            {
                return new IOException($"{message}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
            }

            internal enum SymbolicLinkFlags
            {
                File = 0x0,
                Directory = 0x1,
                AllowUnprivilegedCreate = 0x2,
            }

            internal enum FileAccess
            {
                All = 0x10000000,
                Read = int.MinValue,
                Write = 0x40000000,
                Execute = 0x20000000
            }

            internal enum PathNameFlags
            {
                FileNameNormalized = 0x0,
                FileNameOpened = 0x8,
                VolumeNameDOS = 0x0,
                VolumeNameGuid = 0x1,
                VolumeNameNT = 0x2,
                VolumeNameNone = 0x4
            }

            internal const int BackupSemantics = 33554432;

            internal const int OpenReparsePoint = 2097152;

            internal const int FsctlGetReparsePoint = 589992;

            internal const int ErrorInsufficientBuffer = 122;

            internal const int ErrorMoreData = 234;

            internal const uint ReparseTagSymlink = 2684354572u;

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.I1)]
            internal static extern bool CreateSymbolicLink(string target, string source, SymbolicLinkFlags flags);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            internal static extern SafeFileHandle CreateFile(string path, FileAccess access, FileShare share, IntPtr securityAttributes, FileMode disposition, int flagsAndAttributes, IntPtr template);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            internal static extern bool DeviceIoControl(SafeFileHandle handle, int ioControlCode, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] inBuffer, int inBufferLen, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 5)] byte[] outBuffer, int outBufferLen, out int bytesReturned, IntPtr overlapped);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            internal static extern int GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, int pathlen, PathNameFlags flags);
        }

        public static void Create(string target, string path)
        {
            string text = Path.Combine(Path.GetDirectoryName(path), target);
            switch (Environment.OSVersion.Platform)
            {
                default:
                    throw new InvalidOperationException("Unsupported operating system");
                case PlatformID.Unix:
                case PlatformID.MacOSX:
                    {
                        if (!File.Exists(text) && !Directory.Exists(text))
                        {
                            FileNotFoundException ex = new FileNotFoundException("Link target does not exist", text);
                            throw ex;
                        }
                        if (Unix.symlink(target, path) < 0)
                        {
                            throw Unix.Failure("Could not create link");
                        }
                        break;
                    }
                case PlatformID.Win32NT:
                    {
                        Win32.SymbolicLinkFlags flags;
                        if (File.Exists(text))
                        {
                            flags = Win32.SymbolicLinkFlags.File;
                        }
                        else
                        {
                            if (!Directory.Exists(text))
                            {
                                FileNotFoundException ex = new FileNotFoundException("Link target does not exist", text);
                                throw ex;
                            }
                            flags = Win32.SymbolicLinkFlags.Directory;
                        }
                        flags |= Win32.SymbolicLinkFlags.AllowUnprivilegedCreate;
                        text = !Path.IsPathRooted(target)
                            ? target 
                            : "\\\\?\\" + Path.GetFullPath(target);

                        string target2 = "\\\\?\\" + Path.GetFullPath(path);
                        if (!Win32.CreateSymbolicLink(target2, text, flags))
                        {
                            throw Win32.Failure("Could not create link");
                        }
                        break;
                    }
            }
        }
    }
}