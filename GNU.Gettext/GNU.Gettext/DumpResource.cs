/* GNU gettext for C#
 * Copyright (C) 2003-2004 Free Software Foundation, Inc.
 * Written by Bruno Haible <bruno@clisp.org>, 2003.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2, or (at your option)
 * any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 59 Temple Place - Suite 330, Boston, MA 02111-1307, USA.
 */

/*
 * This program dumps a GettextResourceSet subclass (in a satellite assembly)
 * or a .resources file as a PO file.
 */

using System;
using System.Reflection;
using System.Collections;
using System.IO;
using System.Text;
using System.Resources;

namespace GNU.Gettext;

public class DumpResource
{
    private readonly TextWriter Out;
    private void DumpString(string str)
    {
        int n = str.Length;
        Out.Write('"');
        for (int i = 0; i < n; i++)
        {
            char c = str[i];
            if (c == 0x0008)
            {
                Out.Write('\\'); Out.Write('b');
            }
            else if (c == 0x000c)
            {
                Out.Write('\\'); Out.Write('f');
            }
            else if (c == 0x000a)
            {
                Out.Write('\\'); Out.Write('n');
            }
            else if (c == 0x000d)
            {
                Out.Write('\\'); Out.Write('r');
            }
            else if (c == 0x0009)
            {
                Out.Write('\\'); Out.Write('t');
            }
            else if (c == '\\' || c == '"')
            {
                Out.Write('\\'); Out.Write(c);
            }
            else
                Out.Write(c);
        }
        Out.Write('"');
    }
    private void DumpMessage(string msgid, string msgid_plural, Object msgstr)
    {
        Out.Write("msgid "); DumpString(msgid); Out.Write('\n');
        if (msgid_plural != null)
        {
            Out.Write("msgid_plural "); DumpString(msgid_plural); Out.Write('\n');
            for (int i = 0; i < (msgstr as string[]).Length; i++)
            {
                Out.Write("msgstr[" + i + "] ");
                DumpString((msgstr as string[])[i]);
                Out.Write('\n');
            }
        }
        else
        {
            Out.Write("msgstr "); DumpString(msgstr as string); Out.Write('\n');
        }
        Out.Write('\n');
    }

    // ---------------- Dumping a GettextResourceSet ----------------

    private void Dump(GettextResourceSet catalog)
    {
        MethodInfo pluralMethod =
          catalog.GetType().GetMethod("GetMsgidPluralTable", Type.EmptyTypes);
        // Search for the header entry.
        {
            var header_entry = catalog.GetObject("");
            // If there is no header entry, fake one.
            // FIXME: This is not needed; right after po_lex_charset_init set
            // the PO charset to UTF-8.
            header_entry ??= "Content-Type: text/plain; charset=UTF-8\n";
            DumpMessage("", null, header_entry);
        }
        // Now the other messages.
        {
            Hashtable plural = null;
            if (pluralMethod != null)
                plural = pluralMethod.Invoke(catalog, Array.Empty<object>()) as Hashtable;

            foreach (DictionaryEntry dict in catalog)
            {
                string key = dict.Key.ToString();
                if (!string.IsNullOrEmpty(key))
                {
                    object value = catalog.GetObject(key);
                    string key_plural =
                      (plural != null && value is string[]? plural[key] as string : null);
                    DumpMessage(key, key_plural, value);
                }
            }
        }
    }
    // Essentially taken from class GettextResourceManager.
    private static Assembly GetSatelliteAssembly(string baseDirectory, string resourceName, string cultureName)
    {
        string satelliteExpectedLocation =
          baseDirectory
          + Path.DirectorySeparatorChar + cultureName
          + Path.DirectorySeparatorChar + resourceName + ".resources.dll";
        return Assembly.LoadFrom(satelliteExpectedLocation);
    }
    // Taken from class GettextResourceManager.
    private static string ConstructClassName(string resourceName)
    {
        // We could just return an arbitrary fixed class name, like "Messages",
        // assuming that every assembly will only ever contain one
        // GettextResourceSet subclass, but this assumption would break the day
        // we want to support multi-domain PO files in the same format...
        bool valid = resourceName.Length > 0;
        for (int i = 0; valid && i < resourceName.Length; i++)
        {
            char c = resourceName[i];
            if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c == '_')
                  || (i > 0 && c >= '0' && c <= '9')))
                valid = false;
        }
        if (valid)
            return resourceName;
        else
        {
            // Use hexadecimal escapes, using the underscore as escape character.
            string hexdigit = "0123456789abcdef";
            StringBuilder b = new StringBuilder();
            b.Append("__UESCAPED__");
            for (int i = 0; i < resourceName.Length; i++)
            {
                char c = resourceName[i];
                if (c >= 0xd800 && c < 0xdc00
                    && i + 1 < resourceName.Length
                    && resourceName[i + 1] >= 0xdc00 && resourceName[i + 1] < 0xe000)
                {
                    // Combine two UTF-16 words to a character.
                    char c2 = resourceName[i + 1];
                    int uc = 0x10000 + ((c - 0xd800) << 10) + (c2 - 0xdc00);
                    b.Append('_');
                    b.Append('U');
                    b.Append(hexdigit[(uc >> 28) & 0x0f]);
                    b.Append(hexdigit[(uc >> 24) & 0x0f]);
                    b.Append(hexdigit[(uc >> 20) & 0x0f]);
                    b.Append(hexdigit[(uc >> 16) & 0x0f]);
                    b.Append(hexdigit[(uc >> 12) & 0x0f]);
                    b.Append(hexdigit[(uc >> 8) & 0x0f]);
                    b.Append(hexdigit[(uc >> 4) & 0x0f]);
                    b.Append(hexdigit[uc & 0x0f]);
                    i++;
                }
                else if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')
                             || (c >= '0' && c <= '9')))
                {
                    int uc = c;
                    b.Append('_');
                    b.Append('u');
                    b.Append(hexdigit[(uc >> 12) & 0x0f]);
                    b.Append(hexdigit[(uc >> 8) & 0x0f]);
                    b.Append(hexdigit[(uc >> 4) & 0x0f]);
                    b.Append(hexdigit[uc & 0x0f]);
                }
                else
                    b.Append(c);
            }
            return b.ToString();
        }
    }
    // Essentially taken from class GettextResourceManager.
    private static GettextResourceSet InstantiateResourceSet(Assembly satelliteAssembly, string resourceName, string cultureName)
    {
        Type clazz = satelliteAssembly.GetType(ConstructClassName(resourceName) + "_" + cultureName.Replace('-', '_'));
        ConstructorInfo constructor = clazz.GetConstructor(Type.EmptyTypes);
        return constructor.Invoke(null) as GettextResourceSet;
    }
    public DumpResource(string baseDirectory, string resourceName, string cultureName)
    {
        // We are only interested in the messages belonging to the locale
        // itself, not in the inherited messages. Therefore we instantiate just
        // the GettextResourceSet, not a GettextResourceManager.
        Assembly satelliteAssembly =
          GetSatelliteAssembly(baseDirectory, resourceName, cultureName);
        GettextResourceSet catalog =
          InstantiateResourceSet(satelliteAssembly, resourceName, cultureName);
        BufferedStream stream = new BufferedStream(Console.OpenStandardOutput());
        Out = new StreamWriter(stream, new UTF8Encoding());
        Dump(catalog);
        Out.Close();
        stream.Close();
    }

    // ----------------- Dumping a .resources file ------------------

    public DumpResource(string filename)
    {
        BufferedStream stream = new BufferedStream(Console.OpenStandardOutput());
        Out = new StreamWriter(stream, new UTF8Encoding());
        ResourceReader rr;
        if (filename.Equals("-"))
        {
            BufferedStream input = new BufferedStream(Console.OpenStandardInput());
            // A temporary output stream is needed because ResourceReader expects
            // to be able to seek in the Stream.
            byte[] contents;
            {
                MemoryStream tmpstream = new MemoryStream();
                byte[] buf = new byte[1024];
                while(true)
                {
                    int n = input.Read(buf, 0, 1024);
                    if (n == 0)
                        break;
                    tmpstream.Write(buf, 0, n);
                }
                contents = tmpstream.ToArray();
                tmpstream.Close();
            }
            MemoryStream tmpinput = new MemoryStream(contents);
            rr = new ResourceReader(tmpinput);
        }
        else
        {
            rr = new ResourceReader(filename);
        }
        foreach (DictionaryEntry entry in rr) // uses rr.GetEnumerator()
            DumpMessage(entry.Key as string, null, entry.Value as string);
        rr.Close();
        Out.Close();
        stream.Close();
    }
}
