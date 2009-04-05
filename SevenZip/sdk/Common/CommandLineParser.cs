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
using System.Collections;

namespace SevenZip.Sdk.CommandLineParser
{
    /// <summary>
    /// The switch types
    /// </summary>
	public enum SwitchType
	{
        /// <summary>
        /// The simple switch
        /// </summary>
		Simple,
        /// <summary>
        /// PostMinus
        /// </summary>
		PostMinus,
        /// <summary>
        /// The limited post string
        /// </summary>
		LimitedPostString,
        /// <summary>
        /// The unlimited post string
        /// </summary>
		UnlimitedPostString,
        /// <summary>
        /// The post char
        /// </summary>
		PostChar
	}

    /// <summary>
    /// Switches a form
    /// </summary>
	internal class SwitchForm
	{
        /// <summary>
        /// Identificator string
        /// </summary>
		public string IDString;
		public SwitchType Type;
		public bool Multi;
		public int MinLen;
		public int MaxLen;
		public string PostCharSet;

        /// <summary>
        /// Initializes a new instance of the SwitchForm class
        /// </summary>
        /// <param name="idString"></param>
        /// <param name="type"></param>
        /// <param name="multi"></param>
        /// <param name="minLen"></param>
        /// <param name="maxLen"></param>
        /// <param name="postCharSet"></param>
		public SwitchForm(string idString, SwitchType type, bool multi,
			int minLen, int maxLen, string postCharSet)
		{
			IDString = idString;
			Type = type;
			Multi = multi;
			MinLen = minLen;
			MaxLen = maxLen;
			PostCharSet = postCharSet;
		}
		public SwitchForm(string idString, SwitchType type, bool multi, int minLen):
			this(idString, type, multi, minLen, 0, "")
		{
		}
		public SwitchForm(string idString, SwitchType type, bool multi):
			this(idString, type, multi, 0)
		{
		}
	}

	internal class SwitchResult
	{
		public bool ThereIs;
		public bool WithMinus;
		public ArrayList PostStrings = new ArrayList();
		public int PostCharIndex;
	}

	internal class Parser
	{
		public ArrayList NonSwitchStrings = new ArrayList();
		SwitchResult[] _switches;
        /// <summary>
        /// Initializes a new instance of the Parser class
        /// </summary>
        /// <param name="numSwitches"></param>
		public Parser(int numSwitches)
		{
			_switches = new SwitchResult[numSwitches];
			for (int i = 0; i < numSwitches; i++)
				_switches[i] = new SwitchResult();
		}

		bool ParseString(string srcString, SwitchForm[] switchForms)
		{
			int len = srcString.Length;
			if (len == 0)
				return false;
			int pos = 0;
			if (!IsItSwitchChar(srcString[pos]))
				return false;
			while (pos < len)
			{
				if (IsItSwitchChar(srcString[pos]))
					pos++;
				const int kNoLen = -1;
				int matchedSwitchIndex = 0;
				int maxLen = kNoLen;
				for (int switchIndex = 0; switchIndex < _switches.Length; switchIndex++)
				{
					int switchLen = switchForms[switchIndex].IDString.Length;
					if (switchLen <= maxLen || pos + switchLen > len)
						continue;
					if (String.Compare(switchForms[switchIndex].IDString, 0,
							srcString, pos, switchLen, true, System.Globalization.CultureInfo.CurrentCulture) == 0)
					{
						matchedSwitchIndex = switchIndex;
						maxLen = switchLen;
					}
				}
                if (maxLen == kNoLen)
                {
                    throw new ArgumentException("maxLen == kNoLen");
                }
				SwitchResult matchedSwitch = _switches[matchedSwitchIndex];
				SwitchForm switchForm = switchForms[matchedSwitchIndex];
                if ((!switchForm.Multi) && matchedSwitch.ThereIs)
                {
                    throw new ArgumentException("switch must be single");
                }
				matchedSwitch.ThereIs = true;
				pos += maxLen;
				int tailSize = len - pos;
				SwitchType type = switchForm.Type;
				switch (type)
				{
					case SwitchType.PostMinus:
						{
							if (tailSize == 0)
								matchedSwitch.WithMinus = false;
							else
							{
								matchedSwitch.WithMinus = (srcString[pos] == kSwitchMinus);
								if (matchedSwitch.WithMinus)
									pos++;
							}
							break;
						}
					case SwitchType.PostChar:
						{
                            if (tailSize < switchForm.MinLen)
                            {
                                throw new ArgumentException("switch is not full");
                            }
							string charSet = switchForm.PostCharSet;
							const int kEmptyCharValue = -1;
							if (tailSize == 0)
								matchedSwitch.PostCharIndex = kEmptyCharValue;
							else
							{
								int index = charSet.IndexOf(srcString[pos]);
								if (index < 0)
									matchedSwitch.PostCharIndex = kEmptyCharValue;
								else
								{
									matchedSwitch.PostCharIndex = index;
									pos++;
								}
							}
							break;
						}
					case SwitchType.LimitedPostString:
					case SwitchType.UnlimitedPostString:
						{
							int minLen = switchForm.MinLen;
                            if (tailSize < minLen)
                            {
                                throw new ArgumentException("switch is not full");
                            }
							if (type == SwitchType.UnlimitedPostString)
							{
								matchedSwitch.PostStrings.Add(srcString.Substring(pos));
								return true;
							}
							String stringSwitch = srcString.Substring(pos, minLen);
							pos += minLen;
							for (int i = minLen; i < switchForm.MaxLen && pos < len; i++, pos++)
							{
								char c = srcString[pos];
								if (IsItSwitchChar(c))
									break;
								stringSwitch += c;
							}
							matchedSwitch.PostStrings.Add(stringSwitch);
							break;
						}
				}
			}
			return true;

		}
        /// <summary>
        /// Parses command strings
        /// </summary>
        /// <param name="switchForms">The switch forms</param>
        /// <param name="commandStrings">The command strings</param>
		public void ParseStrings(SwitchForm[] switchForms, string[] commandStrings)
		{
			int numCommandStrings = commandStrings.Length;
			bool stopSwitch = false;
			for (int i = 0; i < numCommandStrings; i++)
			{
				string s = commandStrings[i];
				if (stopSwitch)
					NonSwitchStrings.Add(s);
				else
					if (s == kStopSwitchParsing)
					stopSwitch = true;
				else
					if (!ParseString(s, switchForms))
					NonSwitchStrings.Add(s);
			}
		}
        /// <summary>
        /// Switches the result
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
		public SwitchResult this[int index] { get { return _switches[index]; } }

        /// <summary>
        /// Parses the specified command
        /// </summary>
        /// <param name="commandForms">Array of command forms</param>
        /// <param name="commandString">Command string value</param>
        /// <param name="postString">The resultant string</param>
        /// <returns></returns>
		public static int ParseCommand(CommandForm[] commandForms, string commandString,
			out string postString)
		{
			for (int i = 0; i < commandForms.Length; i++)
			{
				string id = commandForms[i].IDString;
				if (commandForms[i].PostStringMode)
				{
					if (commandString.IndexOf(id, StringComparison.OrdinalIgnoreCase) == 0)
					{
						postString = commandString.Substring(id.Length);
						return i;
					}
				}
				else
					if (commandString == id)
				{
					postString = "";
					return i;
				}
			}
			postString = "";
			return -1;
		}

		static bool ParseSubCharsCommand(int numForms, CommandSubCharsSet[] forms,
			string commandString, ArrayList indexes)
		{
			indexes.Clear();
			int numUsedChars = 0;
			for (int i = 0; i < numForms; i++)
			{
				CommandSubCharsSet charsSet = forms[i];
				int currentIndex = -1;
				int len = charsSet.Chars.Length;
				for (int j = 0; j < len; j++)
				{
					char c = charsSet.Chars[j];
					int newIndex = commandString.IndexOf(c);
					if (newIndex >= 0)
					{
						if (currentIndex >= 0)
							return false;
						if (commandString.IndexOf(c, newIndex + 1) >= 0)
							return false;
						currentIndex = j;
						numUsedChars++;
					}
				}
				if (currentIndex == -1 && !charsSet.EmptyAllowed)
					return false;
				indexes.Add(currentIndex);
			}
			return (numUsedChars == commandString.Length);
		}
		const char kSwitchID1 = '-';
		const char kSwitchID2 = '/';

		const char kSwitchMinus = '-';
		const string kStopSwitchParsing = "--";

		static bool IsItSwitchChar(char c)
		{
			return (c == kSwitchID1 || c == kSwitchID2);
		}
	}

    /// <summary>
    /// The command form class
    /// </summary>
	internal class CommandForm
	{
		public string IDString = "";
		public bool PostStringMode = false;
		public CommandForm(string idString, bool postStringMode)
		{
			IDString = idString;
			PostStringMode = postStringMode;
		}
	}

	class CommandSubCharsSet
	{
		public string Chars = "";
		public bool EmptyAllowed = false;
	}
}
