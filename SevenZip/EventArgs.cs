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
using System.IO;

namespace SevenZip
{
    /// <summary>
    /// EventArgs for storing PercentDone property
    /// </summary>
    public class PercentDoneEventArgs : EventArgs
    {
        private readonly byte _PercentDone;
        private bool _Cancel;
        /// <summary>
        /// Gets the percent of finished work
        /// </summary>
        public byte PercentDone
        {
            get
            {
                return _PercentDone;
            }
        }
        /// <summary>
        /// Gets or sets whether to stop the current archive operation
        /// </summary>
        public bool Cancel
        {
            get
            {
                return _Cancel;
            }

            set
            {
                _Cancel = value;
            }
        }
        /// <summary>
        /// Initializes a new instance of the PercentDoneEventArgs class
        /// </summary>
        /// <param name="percentDone">The percent of finished work</param>
        public PercentDoneEventArgs(byte percentDone)
        {
            if (percentDone > 100 || percentDone < 0)
            {
                throw new ArgumentOutOfRangeException("percentDone", "The percent of finished work must be between 0 and 100.");
            }
            _PercentDone = percentDone;
        }
        /// <summary>
        /// Converts a [0, 1] rate to its percent equivalent
        /// </summary>
        /// <param name="doneRate">The rate of the done work</param>
        /// <returns>Percent integer equivalent</returns>
        internal static byte ProducePercentDone(float doneRate)
        {
            return (byte)Math.Round(100 * doneRate, MidpointRounding.AwayFromZero);
        }
    }

    /// <summary>
    /// The EventArgs class for accurate progress handling
    /// </summary>
    public sealed class ProgressEventArgs : PercentDoneEventArgs
    {
        private byte _Delta;
        /// <summary>
        /// Gets the change in done work percentage
        /// </summary>
        public byte PercentDelta
        {
            get
            {
                return _Delta;
            }
        }
        /// <summary>
        /// Initializes a new instance of the ProgressEventArgs class
        /// </summary>
        /// <param name="percentDone">The percent of finished work</param>
        /// <param name="percentDelta">The percent of work done after the previous event</param>
        public ProgressEventArgs(byte percentDone, byte percentDelta)
            : base(percentDone)
        {
            _Delta = percentDelta;
        }
    }

    /// <summary>
    /// EventArgs used to report the index of file which is going to be unpacked
    /// </summary>
    public sealed class IndexEventArgs : PercentDoneEventArgs
    {
        private readonly int _FileIndex;        
        /// <summary>
        /// Gets file index in the archive file table
        /// </summary>
        public int FileIndex
        {
            get
            {
                return _FileIndex;
            }
        }        
        /// <summary>
        /// Initializes a new instance of the IndexEventArgs class
        /// </summary>
        /// <param name="fileIndex">File index in the archive file table</param>
        /// <param name="percentDone">The percent of finished work</param>
        [CLSCompliantAttribute(false)]
        public IndexEventArgs(uint fileIndex, byte percentDone)
            : base(percentDone)
        {
            _FileIndex = (int)fileIndex;
        }
    }
    /// <summary>
    /// EventArgs used to report the file information which is going to be packed
    /// </summary>
    public sealed class FileInfoEventArgs : PercentDoneEventArgs
    {
        private readonly FileInfo _FileInfo;
        private readonly string _FileName;
        /// <summary>
        /// Gets the current FileInfo
        /// </summary>
        public FileInfo FileInfo
        {
            get
            {
                return _FileInfo;
            }
        }
        /// <summary>
        /// Gets the current file name
        /// </summary>
        public string FileName
        {
            get
            {
                return _FileName;
            }
        }
        /// <summary>
        /// Initializes a new instance of the FileInfoEventArgs class
        /// </summary>
        /// <param name="fileInfo">The current file FileInfo</param>
        /// <param name="fileName">The current file name.</param>
        /// <param name="percentDone">The percent of finished work</param>
        public FileInfoEventArgs(FileInfo fileInfo, string fileName, byte percentDone)
            : base(percentDone)
        {
            _FileInfo = fileInfo;
            _FileName = fileName;
        }
    }
    /// <summary>
    /// EventArgs used to report the size of unpacked archive data
    /// </summary>
    public sealed class OpenEventArgs : EventArgs
    {
        private ulong _TotalSize;
        /// <summary>
        /// Gets the size of unpacked archive data
        /// </summary>
        [CLSCompliantAttribute(false)]
        public ulong TotalSize
        {
            get
            {
                return _TotalSize;
            }
        }
        /// <summary>
        /// Initializes a new instance of the OpenEventArgs class
        /// </summary>
        /// <param name="totalSize">Size of unpacked archive data</param>
        [CLSCompliantAttribute(false)]
        public OpenEventArgs(ulong totalSize)
        {
            _TotalSize = totalSize;
        }
    }

    /// <summary>
    /// Stores an int number
    /// </summary>
    public sealed class IntEventArgs : EventArgs
    {
        private int _Value;

        /// <summary>
        /// Gets the value of the IntEventArgs class
        /// </summary>
        public int Value
        {
            get
            {
                return _Value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the IntEventArgs class
        /// </summary>
        /// <param name="value">Useful data carried by the IntEventArgs class</param>
        public IntEventArgs(int value)
        {
            _Value = value;
        }
    }

    /// <summary>
    /// EventArgs for FileExists event, stores the file name
    /// </summary>
    public sealed class FileNameEventArgs : EventArgs
    {
        private string _FileName;
        private bool _Overwrite;

        /// <summary>
        /// Gets the file name
        /// </summary>
        public string FileName
        {
            get
            {
                return _FileName;
            }
        }

        /// <summary>
        /// Gets or sets the value indicating whether to overwrite the existing file or not
        /// </summary>
        public bool Overwrite
        {
            get
            {
                return _Overwrite;
            }

            set
            {
                _Overwrite = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the FileNameEventArgs class
        /// </summary>
        /// <param name="fileName">The file name</param>
        public FileNameEventArgs(string fileName)
        {
            _FileName = fileName;
            _Overwrite = true;
        }
    }
}
