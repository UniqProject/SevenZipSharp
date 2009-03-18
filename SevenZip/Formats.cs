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
using System.Collections.Generic;
using System.IO;

namespace SevenZip
{
    /// <summary>
    /// Readable archive format enumeration
    /// </summary>
    public enum InArchiveFormat
    {
        /// <summary>
        /// Open 7-zip archive format
        /// </summary>  
        /// <remarks><a href="http://en.wikipedia.org/wiki/7-zip">Wikipedia information</a></remarks> 
        SevenZip,
        /// <summary>
        /// Proprietary Arj archive format
        /// </summary>
        /// <remarks><a href="http://en.wikipedia.org/wiki/ARJ">Wikipedia information</a></remarks>
        Arj,
        /// <summary>
        /// Open Bzip2 archive format
        /// </summary>
        /// <remarks><a href="http://en.wikipedia.org/wiki/Bzip2">Wikipedia information</a></remarks>
        BZip2,
        /// <summary>
        /// Microsoft cabinet archive format
        /// </summary>
        /// <remarks><a href="http://en.wikipedia.org/wiki/Cabinet_(file_format)">Wikipedia information</a></remarks>
        Cab,
        /// <summary>
        /// Microsoft Compiled HTML Help file format
        /// </summary>
        /// <remarks><a href="http://en.wikipedia.org/wiki/Microsoft_Compiled_HTML_Help">Wikipedia information</a></remarks>
        Chm,
        /// <summary>
        /// Microsoft Compound file format
        /// </summary>
        /// <remarks><a href="http://en.wikipedia.org/wiki/Compound_File_Binary_Format">Wikipedia information</a></remarks>
        Compound,
        /// <summary>
        /// Open Cpio archive format
        /// </summary>
        /// <remarks><a href="http://en.wikipedia.org/wiki/Cpio">Wikipedia information</a></remarks>
        Cpio,
        /// <summary>
        /// Open Debian software package format
        /// </summary>
        /// <remarks><a href="http://en.wikipedia.org/wiki/Deb_(file_format)">Wikipedia information</a></remarks>
        Deb,
        /// <summary>
        /// Open Gzip archive format
        /// </summary>
        /// <remarks><a href="http://en.wikipedia.org/wiki/Gzip">Wikipedia information</a></remarks>
        GZip,
        /// <summary>
        /// Open ISO disk image format
        /// </summary>
        /// <remarks><a href="http://en.wikipedia.org/wiki/ISO_image">Wikipedia information</a></remarks>
        Iso,
        /// <summary>
        /// Open Lzh archive format
        /// </summary>
        /// <remarks><a href="http://en.wikipedia.org/wiki/Lzh">Wikipedia information</a></remarks>
        Lzh,
        /// <summary>
        /// Open core 7-zip Lzma raw archive format
        /// </summary>
        /// <remarks><a href="http://en.wikipedia.org/wiki/Lzma">Wikipedia information</a></remarks>
        Lzma,
        /// <summary>
        /// Nullsoft installation package format
        /// </summary>
        /// <remarks><a href="http://en.wikipedia.org/wiki/NSIS">Wikipedia information</a></remarks>
        Nsis,
        /// <summary>
        /// RarLab Rar archive format
        /// </summary>
        /// <remarks><a href="http://en.wikipedia.org/wiki/Rar">Wikipedia information</a></remarks>
        Rar,
        /// <summary>
        /// Open Rpm software package format
        /// </summary>
        /// <remarks><a href="http://en.wikipedia.org/wiki/RPM_Package_Manager">Wikipedia information</a></remarks>
        Rpm,
        /// <summary>
        /// Open split file format
        /// </summary>
        /// <remarks><a href="http://en.wikipedia.org/wiki/RPM_Package_Manager">Wikipedia information</a></remarks>
        Split,
        /// <summary>
        /// Open Tar archive format
        /// </summary>
        /// <remarks><a href="http://en.wikipedia.org/wiki/Tar_(file_format)">Wikipedia information</a></remarks>
        Tar,
        /// <summary>
        /// Microsoft Windows Imaging disk image format
        /// </summary>
        /// <remarks><a href="http://en.wikipedia.org/wiki/Windows_Imaging_Format">Wikipedia information</a></remarks>
        Wim,
        /// <summary>
        /// Open LZW archive format; implemented in "compress" program
        /// </summary>
        /// <remarks><a href="http://en.wikipedia.org/wiki/Compress">Wikipedia information</a></remarks>
        Lzw,
        /// <summary>
        /// Open Zip archive format
        /// </summary>
        /// <remarks><a href="http://en.wikipedia.org/wiki/ZIP_(file_format)">Wikipedia information</a></remarks>
        Zip
    }
    /// <summary>
    /// Writable archive format enumeration
    /// </summary>    
    public enum OutArchiveFormat
    {
        /// <summary>
        /// Open 7-zip archive format
        /// </summary>
        /// <remarks><a href="http://en.wikipedia.org/wiki/7-zip">Wikipedia information</a></remarks>
        SevenZip,
        /// <summary>
        /// Open Zip archive format
        /// </summary>
        /// <remarks><a href="http://en.wikipedia.org/wiki/ZIP_(file_format)">Wikipedia information</a></remarks>
        Zip,
        /// <summary>       
        /// Open Bzip2 archive format
        /// </summary>
        /// <remarks><a href="http://en.wikipedia.org/wiki/Bzip2">Wikipedia information</a></remarks>
        BZip2
    }
    /// <summary>
    /// Archive format routines
    /// </summary>
    public static class Formats
    {
        /// <summary>
        /// Gets max value in the enum of type e
        /// </summary>
        /// <param name="type">Type of the enum</param>
        /// <returns>Max value</returns>
        public static int GetMaxValue(Type type)
        {
            List<int> enumList = new List<int>((IEnumerable<int>)Enum.GetValues(type));
            enumList.Sort();
            return enumList[enumList.Count - 1];
        }
        /// <summary>
        /// List of readable archive format interface guids for 7-zip COM interop
        /// </summary>
        internal readonly static Dictionary<InArchiveFormat, Guid> InFormatGuids = new Dictionary<InArchiveFormat, Guid>(20) 
        #region InFormatGuids initialization
        { {InArchiveFormat.SevenZip,    new Guid("23170f69-40c1-278a-1000-000110070000")},
          {InArchiveFormat.Arj,         new Guid("23170f69-40c1-278a-1000-000110040000")},
          {InArchiveFormat.BZip2,       new Guid("23170f69-40c1-278a-1000-000110020000")},
          {InArchiveFormat.Cab,         new Guid("23170f69-40c1-278a-1000-000110080000")},
          {InArchiveFormat.Chm,         new Guid("23170f69-40c1-278a-1000-000110e90000")},
          {InArchiveFormat.Compound,    new Guid("23170f69-40c1-278a-1000-000110e50000")},
          {InArchiveFormat.Cpio,        new Guid("23170f69-40c1-278a-1000-000110ed0000")},
          {InArchiveFormat.Deb,         new Guid("23170f69-40c1-278a-1000-000110ec0000")},
          {InArchiveFormat.GZip,        new Guid("23170f69-40c1-278a-1000-000110ef0000")},
          {InArchiveFormat.Iso,         new Guid("23170f69-40c1-278a-1000-000110e70000")},
          {InArchiveFormat.Lzh,         new Guid("23170f69-40c1-278a-1000-000110060000")},
          {InArchiveFormat.Lzma,        new Guid("23170f69-40c1-278a-1000-0001100a0000")},
          {InArchiveFormat.Nsis,        new Guid("23170f69-40c1-278a-1000-000110090000")},
          {InArchiveFormat.Rar,         new Guid("23170f69-40c1-278a-1000-000110030000")},
          {InArchiveFormat.Rpm,         new Guid("23170f69-40c1-278a-1000-000110eb0000")},
          {InArchiveFormat.Split,       new Guid("23170f69-40c1-278a-1000-000110ea0000")},
          {InArchiveFormat.Tar,         new Guid("23170f69-40c1-278a-1000-000110ee0000")},
          {InArchiveFormat.Wim,         new Guid("23170f69-40c1-278a-1000-000110e60000")},
          {InArchiveFormat.Lzw,           new Guid("23170f69-40c1-278a-1000-000110050000")},
          {InArchiveFormat.Zip,         new Guid("23170f69-40c1-278a-1000-000110010000")}};
        #endregion
        /// <summary>
        /// List of writable archive format interface guids for 7-zip COM interop
        /// </summary>
        internal readonly static Dictionary<OutArchiveFormat, Guid> OutFormatGuids = new Dictionary<OutArchiveFormat, Guid>(2)
        #region OutFormatGuids initialization
        { {OutArchiveFormat.SevenZip,   new Guid("23170f69-40c1-278a-1000-000110070000")},
          {OutArchiveFormat.Zip,        new Guid("23170f69-40c1-278a-1000-000110010000")},
          {OutArchiveFormat.BZip2,      new Guid("23170f69-40c1-278a-1000-000110020000")}};
        #endregion
        /// <summary>
        /// List of archive formats corresponding to specific extensions
        /// </summary>
        internal readonly static Dictionary<string, InArchiveFormat> InExtensionFormats = new Dictionary<string, InArchiveFormat>()
        #region InExtensionFormats initialization
        { {"7z", InArchiveFormat.SevenZip},
          {"gz", InArchiveFormat.GZip},
          {"tar", InArchiveFormat.Tar},
          {"rar", InArchiveFormat.Rar},
          {"zip", InArchiveFormat.Zip},
          {"lzma", InArchiveFormat.Lzma},
          {"lzh", InArchiveFormat.Lzh},
          {"arj", InArchiveFormat.Arj},
          {"bz2", InArchiveFormat.BZip2},
          {"cab", InArchiveFormat.Cab},
          {"chm", InArchiveFormat.Chm},
          {"deb", InArchiveFormat.Deb},
          {"iso", InArchiveFormat.Iso},
          {"rpm", InArchiveFormat.Rpm},
          {"wim", InArchiveFormat.Wim},
          {"MyCustomFormatExtension", InArchiveFormat.Zip}};
        #endregion

        /// <summary>
        /// Gets InArchiveFormat for specified archive file name
        /// </summary>
        /// <param name="fileName">Archive file name</param>
        /// <param name="reportErrors">Indicates whether to throw exceptions</param>
        /// <returns>InArchiveFormat recognized by the file name extension</returns>
        public static InArchiveFormat FormatByFileName(string fileName, bool reportErrors)
        {
            if (String.IsNullOrEmpty(fileName) && reportErrors)
            {
                throw new ArgumentException("File name is null or empty string!");
            }
            string extension = Path.GetExtension(fileName).Substring(1);
            if (!InExtensionFormats.ContainsKey(extension) && reportErrors)
            {
                throw new ArgumentException("Extension \"" + extension + "\" is not a supported archive file name extension.");
            }
            else
            {
                return InExtensionFormats[extension];
            }
        }
    }
}
