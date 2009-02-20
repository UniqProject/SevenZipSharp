/*  This file is part of SevenZipSharp.

    SevenZipSharp is free software: you can redistribute it and/or modify
    it under the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    SevenZipSharp is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with SevenZipSharp.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Runtime.InteropServices;

namespace SevenZip
{
    internal static class NativeMethods
    {      
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr LoadLibrary(string fileName);

        [DllImport("kernel32.dll")]
        public static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("ole32.dll")]
        public static extern int PropVariantClear(ref SevenZip.ComRoutines.PropVariant pvar);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int CreateObjectDelegate(
            [In] ref Guid classID,
            [In] ref Guid interfaceID,
            [MarshalAs(UnmanagedType.Interface)] out object outObject);        

        public static T SafeCast<T>(object obj, T def)
        {
            if (obj != null)
            {
                return (T)obj;
            }
            else
            {
                return def;
            }
        }
    }    
}
