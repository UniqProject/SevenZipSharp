using System;
using System.Security;
using System.Security.Permissions;
using SevenZip;

namespace System.Runtime.InteropServices
{
    [SecurityCritical]
    public static class COMMarshal
    {
        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.UnmanagedCode)]
        public static IntPtr StringToBSTR(string s)
        {
            if (s == null)
            {
                return IntPtr.Zero;
            }
            if ((s.Length + 1) < s.Length)
            {
                throw new ArgumentOutOfRangeException("s");
            }
            IntPtr ptr = NativeMethods.SysAllocStringLen(s, s.Length);
            if (ptr == IntPtr.Zero)
            {
                throw new OutOfMemoryException();
            }
            return ptr;
        }

        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.UnmanagedCode)]
        public static object GetObjectForNativeVariant(IntPtr pSrcNativeVariant)
        {
            return null;
        }
    }
}
