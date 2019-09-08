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
#pragma warning disable IDE1006 // Naming Styles
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
#pragma warning restore IDE1006 // Naming Styles
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
                Read = int.MinValue,
                All = 0x10000000,
                Execute = 0x20000000,
                Write = 0x40000000
            }

            // see https://docs.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfinalpathnamebyhandlea
            internal enum PathNameFlags
            {
                FileNameNormalized = 0x0,
#pragma warning disable RCS1234 // Duplicate enum value. - this flag can mean multiple things
                VolumeNameDOS = 0x0,
#pragma warning restore RCS1234 // Duplicate enum value.
                VolumeNameGuid = 0x1,
                VolumeNameNT = 0x2,
                VolumeNameNone = 0x4,
                FileNameOpened = 0x8
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

        public static void Create(string target, string symbolicLink)
        {
            if (!File.Exists(target) && !Directory.Exists(target))
            {
                FileNotFoundException ex = new FileNotFoundException("Link target does not exist", target);
                throw ex;
            }
            switch (Environment.OSVersion.Platform)
            {
                default:
                    throw new InvalidOperationException("Unsupported operating system");
                case PlatformID.Unix:
                case PlatformID.MacOSX:
                    if (Unix.symlink(target, symbolicLink) < 0)
                    {
                        throw Unix.Failure("Could not create link");
                    }
                    break;
                case PlatformID.Win32NT:
                    Win32.SymbolicLinkFlags flags = File.Exists(target)
                        ? Win32.SymbolicLinkFlags.File
                        : Win32.SymbolicLinkFlags.Directory;

                    flags |= Win32.SymbolicLinkFlags.AllowUnprivilegedCreate;

                    target = !Path.IsPathRooted(target)
                        ? target
                        : "\\\\?\\" + Path.GetFullPath(target);

                    symbolicLink = "\\\\?\\" + Path.GetFullPath(symbolicLink);
                    if (!Win32.CreateSymbolicLink(symbolicLink, target, flags))
                    {
                        throw Win32.Failure("Could not create link");
                    }
                    break;
            }
        }
    }
}