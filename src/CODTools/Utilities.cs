using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CODTools
{
    internal static class Utilities
    {
        public static string ToRoundedFloat(this double value)
        {
            return value.ToString("0.000000", new NumberFormatInfo() { NumberDecimalSeparator = "." });
        }

        public static string ToRoundedFloat(this float value)
        {
            return value.ToString("0.000000", new NumberFormatInfo() { NumberDecimalSeparator = "." });
        }

        public static string ReadNullTerminatedString(this BinaryReader stream)
        {
            string str = "";
            char ch;
            while ((int)(ch = stream.ReadChar()) != 0)
                str = str + ch;
            return str;
        }
    }
}
