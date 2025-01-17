//
// StringEscaping.cs
// 
// Author:
//   Michael Hutchinson <mhutchinson@novell.com>
// 
// Copyright (C) 2008 Novell, Inc (http://www.novell.com)
// 
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Text;

namespace GNU.Gettext;

public static class StringEscaping
{
    //based on http://www-ccs.ucsd.edu/c/charset.html
    //with modififications, as Gettext dosn't follow all C escaping
    public static string ToGettextFormat(string text)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    continue;

                //Gettext doesn't like escaping this
                //case '\'':
                //	sb.Append ("\\'");
                //	continue;

                //These shouldn't be in translations... but will be caught by IsControl
                //case '\a':
                //case '\b':
                //case '\f':
                //case '\v':

                //this doesn't matter since we're not dealing with trigraphs
                //case "?":
                //	sb.Append ("\\?");
                //	continue;

                case '\\':
                    sb.Append("\\\\");
                    continue;
                case '\n':
                    sb.Append("\\n");
                    continue;
                case '\r':
                    sb.Append("\\r");
                    continue;
                case '\t':
                    sb.Append("\\t");
                    continue;
            }

            if (c != '_' && char.IsControl(c))
                throw new FormatException(string.Format("Invalid character '{0}' in translatable string: '{1}'", c, text));

            sb.Append(c);
        }
        return sb.ToString();
    }

    public static string FromGettextFormat(string text)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            switch (c)
            {
                case '\\':
                    if (i + 1 < text.Length)
                    {
                        char nextChar = text[i + 1];
                        if (nextChar == '\\' || nextChar == '"')
                        {
                            sb.Append(nextChar);
                            i++;
                            continue;
                        }
                        if (nextChar == 'n')
                        {
                            sb.Append('\n');
                            i++;
                            continue;
                        }
                        if (nextChar == 't')
                        {
                            sb.Append('\t');
                            i++;
                            continue;
                        }
                        if (nextChar == 'r')
                        {
                            sb.Append('\r');
                            i++;
                            continue;
                        }
                        throw new FormatException(string.Format("Invalid escape sequence '{0}' in string: '{1}'", nextChar, text));
                    }
                    break;
            }

            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Replace escape sequences strings by sequences.
    /// I.e. "\\n" -> "\n", "\\"" -> "\"" etc.
    /// </summary>
    /// <returns>
    /// Unescaped string
    /// </returns>
    /// <param name='mode'>
    /// Unescaping mode
    /// </param>
    /// <param name='text'>
    /// Text to process.
    /// </param>
    /// <exception cref='Exception'>
    /// Throw an error if escaping mode is not supported.
    /// </exception>
    public static string UnEscape(EscapeMode mode, string text)
    {
        return mode switch
        {
            EscapeMode.None => text,
            EscapeMode.CSharp => FromCSharpFormat(text),
            EscapeMode.CSharpVerbatim => FromCSharpVerbatimFormat(text),
            EscapeMode.Xml => FromXml(text),
            _ => throw new ArgumentException("Unknown string escaping mode '" + mode.ToString() + "'"),
        };
    }

    ///<summary>
	/// Based on http://www-ccs.ucsd.edu/c/charset.html 
    /// public static string FromCFormat (string text)
    /// Based on the C# 2.0 spec
    /// http://msdn.microsoft.com/en-us/library/aa691090%28v=vs.71%29.aspx
    /// </summary>
    static string FromCSharpVerbatimFormat(string text)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            char c1 = text[i];
            if (c1 == '"')
            {
                i++;
                char c2 = text[i];
                if (c2 != '"')
                    throw new FormatException("Unescaped \" character in C# verbatim string.");
            }
            sb.Append(c1);
        }
        return sb.ToString();
    }

    static string FromXml(string text)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            char c1 = text[i];
            if (c1 == '&')
            {
                int end = text.IndexOf(';', i);
                if (end == -1)
                    throw new FormatException("Unterminated XML entity.");
                string entity = text.Substring(i + 1, end - i - 1);
                switch (entity)
                {
                    case "lt":
                        sb.Append('<');
                        break;
                    case "gt":
                        sb.Append('>');
                        break;
                    case "amp":
                        sb.Append('&');
                        break;
                    case "apos":
                        sb.Append('\'');
                        break;
                    case "quot":
                        sb.Append('"');
                        break;
                    default:
                        throw new FormatException("Unrecogised XML entity '&" + entity + ";'.");
                }
                i = end;
            }
            else
                sb.Append(c1);
        }
        return sb.ToString();
    }

    /// Based on the C# 2.0 spec
    static string FromCSharpFormat(string text)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            char c1 = text[i];
            if (c1 != '\\')
            {
                sb.Append(c1);
                continue;
            }

            i++;
            char c2 = text[i];

            switch (c2)
            {
                case '\'':
                case '"':
                case '\\':
                    sb.Append(c2);
                    break;
                case 'a':
                    sb.Append('\a');
                    break;
                case 'b':
                    sb.Append('\b');
                    break;
                case 'f':
                    sb.Append('\f');
                    break;
                case 'n':
                    sb.Append('\n');
                    break;
                case 'r':
                    sb.Append('\r');
                    break;
                case 't':
                    sb.Append('\t');
                    break;
                case 'v':
                    sb.Append('\v');
                    break;
                case 'U':
                //FIXME UNICODE
                //break;
                case 'u':
                //FIXME unicode
                //break;
                case 'x':
                //FIXME hex unicode
                //break;
                //if (char.IsControl (c);

                default:
                    throw new FormatException("Invalid escape '\\" + c2 + "' in translatable string.");
            }

        }
        return sb.ToString();
    }

    public enum EscapeMode
    {
        None,
        CSharp,
        CSharpVerbatim,
        Xml
    }
}
