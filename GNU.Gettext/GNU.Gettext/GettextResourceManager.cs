/* GNU gettext for C#
 * Copyright (C) 2003, 2005, 2007, 2012 Free Software Foundation, Inc.
 * Written by Bruno Haible <bruno@clisp.org>, 2003.
 * Adapted by Serguei Tarassov <st@arbinada.com>, 2012.
 *
 * This program is free software; you can redistribute it and/or modify it
 * under the terms of the GNU Library General Public License as published
 * by the Free Software Foundation; either version 2, or (at your option)
 * any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Library General Public License for more details.
 *
 * You should have received a copy of the GNU Library General Public
 * License along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301,
 * USA.
 */

/*
 * Using the GNU gettext approach, compiled message catalogs are assemblies
 * containing just one class, a subclass of GettextResourceSet. They are thus
 * interoperable with standard ResourceManager based code.
 *
 * The main differences between the common .NET resources approach and the
 * GNU gettext approach are:
 * - In the .NET resource approach, the keys are abstract textual shortcuts.
 *   In the GNU gettext approach, the keys are the English/ASCII version
 *   of the messages.
 * - In the .NET resource approach, the translation files are called
 *   "Resource.locale.resx" and are UTF-8 encoded XML files. In the GNU gettext
 *   approach, the translation files are called "Resource.locale.po" and are
 *   in the encoding the translator has chosen. There are at least three GUI
 *   translating tools (Emacs PO mode, KDE KBabel, GNOME gtranslator).
 * - In the .NET resource approach, the function ResourceManager.GetString
 *   returns an empty string or throws an InvalidOperationException when no
 *   translation is found. In the GNU gettext approach, the GetString function
 *   returns the (English) message key in that case.
 * - In the .NET resource approach, there is no support for plural handling.
 *   In the GNU gettext approach, we have the GetPluralString function.
 * - In the .NET resource approach, there is no support for context specific
 *   translations.
 *   In the GNU gettext approach, we have the GetParticularString function.
 *
 * To compile GNU gettext message catalogs into C# assemblies, the msgfmt
 * program can be used.
 */

using System;
using System.Globalization;
using System.Resources;
using System.Reflection;
using System.Collections;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;

namespace GNU.Gettext
{

    /// <summary>
    /// Each instance of this class can be used to lookup translations for a
    /// given resource name. For each <c>CultureInfo</c>, it performs the lookup
    /// in several assemblies, from most specific over territory-neutral to
    /// language-neutral.
    /// </summary>
    public class GettextResourceManager : ResourceManager
    {

        public const string ResourceNameSuffix = ".Messages";

        // ======================== Public Constructors ========================

        /// <summary>
        /// Default constructor use assembly name + ".Messages" as base name to locate satellite assemblies.
        /// </summary>
        public GettextResourceManager()
            : this(Assembly.GetCallingAssembly())
        {
        }

        /// <summary>
        /// Same as default constructor but can be called from assembly other than calling one.
        /// </summary>
        /// <param name='assembly'>
        /// Assembly for locate satellites.
        /// </param>
        public GettextResourceManager(Assembly assembly)
            : base(assembly.GetName().Name,
                   assembly,
                   typeof(GettextResourceSet))
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="baseName">the resource name, also the assembly base
        ///                        name</param>
        public GettextResourceManager(string baseName)
            : base(baseName, Assembly.GetCallingAssembly(), typeof(GettextResourceSet))
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="baseName">the resource name, also the assembly base
        ///                        name</param>
        public GettextResourceManager(string baseName, Assembly assembly)
            : base(baseName, assembly, typeof(GettextResourceSet))
        {
        }

        // ======================== Implementation ========================

        /// <summary>
        /// Returns file name for satellite assembly.
        /// Used for compiling by Msgfmt.NET and loading by this resource manager
        /// </summary>
        /// <returns>
        /// The satellite assembly file name.
        /// </returns>
        /// <param name='resourceName'>
        /// Resource base name, i.e. "Solution1.App2.Module3".
        /// </param>
        public static string GetSatelliteAssemblyName(string resourceName)
        {
            return string.Format("{0}{1}.resources.dll", resourceName, ResourceNameSuffix);
        }

        /// <summary>
        /// Loads and returns a satellite assembly for a given culture..
        /// </summary>
        // This is like Assembly.GetSatelliteAssembly, but uses resourceName
        // instead of assembly.GetName().Name, and works around a bug in
        // mono-0.28.
        private static Assembly GetSatelliteAssembly(Assembly assembly, string resourceName, CultureInfo culture)
        {
            string satelliteExpectedLocation =
              Path.GetDirectoryName(assembly.Location)
              + Path.DirectorySeparatorChar + culture.Name
              + Path.DirectorySeparatorChar
                    + GetSatelliteAssemblyName(resourceName);
            if (File.Exists(satelliteExpectedLocation))
            {
                return Assembly.LoadFile(satelliteExpectedLocation);
            }
            // Try to load embedded assembly
            string embeddedResourceId = string.Format("{0}.{1}.{2}",
                assembly.GetName().Name, culture.Name, GetSatelliteAssemblyName(resourceName));
            Stream satAssemblyStream;
            using (satAssemblyStream = assembly.GetManifestResourceStream(embeddedResourceId))
            {
                if (satAssemblyStream == null)
                {
                    // Workaround: .NET doesn't alow '-' in embedded resources names 
                    // https://sourceforge.net/p/gettextnet/discussion/general/thread/88033218/
                    embeddedResourceId = string.Format("{0}.{1}.{2}",
                                                       assembly.GetName().Name,
                                                       culture.Name.Replace('-', '_'),
                                                       GetSatelliteAssemblyName(resourceName));
                    satAssemblyStream = assembly.GetManifestResourceStream(embeddedResourceId);
                }
                if (satAssemblyStream == null)
                    return null;
                Byte[] assemblyData = new Byte[satAssemblyStream.Length];
                satAssemblyStream.Read(assemblyData, 0, assemblyData.Length);
                return Assembly.Load(assemblyData);
            }
        }


        /// <summary>
        /// Converts a resource name to a class name.
        /// </summary>
        /// <returns>a nonempty string consisting of alphanumerics and underscores
        ///          and starting with a letter or underscore</returns>
        private static string ConstructClassName(string resourceName)
        {
            // We could just return an arbitrary fixed class name, like "Messages",
            // assuming that every assembly will only ever contain one
            // GettextResourceSet subclass, but this assumption would break the day
            // we want to support multi-domain PO files in the same format...
            bool valid = (resourceName.Length > 0);
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

        public static string ExtractClassName(string baseName)
        {
            if (string.IsNullOrEmpty(baseName))
                throw new ArgumentException("Empty base name");
            int pos = baseName.LastIndexOf('.');
            if (pos == baseName.Length - 1)
                throw new ArgumentException("Invalid base name");
            if (pos != -1)
                return baseName.Substring(pos + 1);
            return baseName;
        }

        public static string ExtractNamespace(string baseName)
        {
            if (string.IsNullOrEmpty(baseName))
                return string.Empty;
            int pos = baseName.LastIndexOf('.');
            if (pos > 0)
                return baseName.Substring(0, pos);
            return string.Empty;
        }

        /// <summary>
        /// Instantiates a resource set for a given culture.
        /// </summary>
        /// <exception cref="ArgumentException">
        ///   The expected type name is not valid.
        /// </exception>
        /// <exception cref="ReflectionTypeLoadException">
        ///   satelliteAssembly does not contain the expected type.
        /// </exception>
        /// <exception cref="NullReferenceException">
        ///   The type has no no-arguments constructor.
        /// </exception>
        private static GettextResourceSet InstantiateResourceSet(Assembly satelliteAssembly, string resourceName, CultureInfo culture)
        {
            // We expect a class with a culture dependent class name.
            Type type = satelliteAssembly.GetType(
                string.Format("{0}.{1}",
                          resourceName,
                          MakeResourceSetClassName(resourceName, culture)));
            // We expect it has a no-argument constructor, and invoke it.
            ConstructorInfo constructor = type.GetConstructor(Type.EmptyTypes);
            return constructor.Invoke(null) as GettextResourceSet;
        }

        /// <summary>
        /// Create class name for ResourceSet subclass used in resources satellite assembly
        /// </summary>
        /// <param name="resourceName"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public static string MakeResourceSetClassName(string resourceName, CultureInfo culture)
        {
            return ConstructClassName(resourceName) + "_" + culture.Name.Replace('-', '_');
        }

        private static readonly GettextResourceSet[] EmptyResourceSetArray = Array.Empty<GettextResourceSet>();

        // Cache for already loaded GettextResourceSet cascades.
        /* CultureInfo -> GettextResourceSet[] */
        private readonly Hashtable Loaded = new Hashtable();

        /// <summary>
        /// Returns the array of <c>GettextResourceSet</c>s for a given culture,
        /// loading them if necessary, and maintaining the cache.
        /// </summary>
        private GettextResourceSet[] GetResourceSetsFor(CultureInfo culture)
        {
            // Look up in the cache.
            if (Loaded[culture] is not GettextResourceSet[] result)
            {
                lock (this)
                {
                    // Look up again - maybe another thread has filled in the entry
                    // while we slept waiting for the lock.
                    result = Loaded[culture] as GettextResourceSet[];
                    if (result == null)
                    {
                        // Determine the GettextResourceSets for the given culture.
                        if (culture.Parent == null || culture.Equals(CultureInfo.InvariantCulture))
                            // Invariant culture.
                            result = EmptyResourceSetArray;
                        else
                        {
                            // Use a satellite assembly as primary GettextResourceSet, and
                            // the result for the parent culture as fallback.
                            GettextResourceSet[] parentResult = GetResourceSetsFor(culture.Parent);
                            Assembly satelliteAssembly;
                            try
                            {
                                satelliteAssembly = GetSatelliteAssembly(MainAssembly, BaseName, culture);
                            }
                            catch (FileNotFoundException)
                            {
                                satelliteAssembly = null;
                            }
                            if (satelliteAssembly != null)
                            {
                                GettextResourceSet satelliteResourceSet;
                                try
                                {
                                    satelliteResourceSet = InstantiateResourceSet(satelliteAssembly, BaseName, culture);
                                }
                                catch (Exception e)
                                {
                                    Trace.WriteLine(e);
                                    Trace.WriteLine(e.StackTrace);
                                    satelliteResourceSet = null;
                                }
                                if (satelliteResourceSet != null)
                                {
                                    result = new GettextResourceSet[1 + parentResult.Length];
                                    result[0] = satelliteResourceSet;
                                    Array.Copy(parentResult, 0, result, 1, parentResult.Length);
                                }
                                else
                                    result = parentResult;
                            }
                            else
                                result = parentResult;
                        }
                        // Put the result into the cache.
                        Loaded.Add(culture, result);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Returns the translation of <paramref name="msgid"/> in a given culture.
        /// </summary>
        /// <param name="msgid">the key string to be translated, an ASCII
        ///                     string</param>
        /// <returns>the translation of <paramref name="msgid"/>, or
        ///          <paramref name="msgid"/> if none is found</returns>
        public override string GetString(string msgid, CultureInfo culture)
        {
            foreach (GettextResourceSet rs in GetResourceSetsFor(culture))
            {
                string translation = rs.GetString(msgid);
                if (!string.IsNullOrEmpty(translation))
                    return translation;
            }
            // Fallback.
            return msgid;
        }

        /// <summary>
        /// Returns the translation of <paramref name="msgid"/> and
        /// <paramref name="msgidPlural"/> in a given culture, choosing the right
        /// plural form depending on the number <paramref name="n"/>.
        /// </summary>
        /// <param name="msgid">the key string to be translated, an ASCII
        ///                     string</param>
        /// <param name="msgidPlural">the English plural of <paramref name="msgid"/>,
        ///                           an ASCII string</param>
        /// <param name="n">the number, should be &gt;= 0</param>
        /// <returns>the translation, or <paramref name="msgid"/> or
        ///          <paramref name="msgidPlural"/> if none is found</returns>
        public virtual string GetPluralString(string msgid, string msgidPlural, long n, CultureInfo culture)
        {
            foreach (GettextResourceSet rs in GetResourceSetsFor(culture))
            {
                string translation = rs.GetPluralString(msgid, msgidPlural, n);
                if (!string.IsNullOrEmpty(translation))
                    return translation;
            }
            // Fallback: Germanic plural form.
            return (n == 1 ? msgid : msgidPlural);
        }

        // ======================== Public Methods ========================

        public static string MakeContextMsgid(string msgctxt, string msgid)
        {
            return msgctxt + "\u0004" + msgid;
        }

        /// <summary>
        /// Returns the translation of <paramref name="msgid"/> in the context
        /// of <paramref name="msgctxt"/> a given culture.
        /// </summary>
        /// <param name="msgctxt">the context for the key string, an ASCII
        ///                       string</param>
        /// <param name="msgid">the key string to be translated, an ASCII
        ///                     string</param>
        /// <returns>the translation of <paramref name="msgid"/>, or
        ///          <paramref name="msgid"/> if none is found</returns>
        public string GetParticularString(string msgctxt, string msgid, CultureInfo culture)
        {
            foreach (GettextResourceSet rs in GetResourceSetsFor(culture))
            {
                string translation = rs.GetString(MakeContextMsgid(msgctxt, msgid));
                if (!string.IsNullOrEmpty(translation))
                    return translation;
                // Fallback to non cotextual translation
                translation = rs.GetString(msgid);
                if (!string.IsNullOrEmpty(translation))
                    return translation;
            }
            // Fallback.
            return msgid;
        }

        /// <summary>
        /// Returns the translation of <paramref name="msgid"/> and
        /// <paramref name="msgidPlural"/> in the context of
        /// <paramref name="msgctxt"/> in a given culture, choosing the right
        /// plural form depending on the number <paramref name="n"/>.
        /// </summary>
        /// <param name="msgctxt">the context for the key string, an ASCII
        ///                       string</param>
        /// <param name="msgid">the key string to be translated, an ASCII
        ///                     string</param>
        /// <param name="msgidPlural">the English plural of <paramref name="msgid"/>,
        ///                           an ASCII string</param>
        /// <param name="n">the number, should be &gt;= 0</param>
        /// <returns>the translation, or <paramref name="msgid"/> or
        ///          <paramref name="msgidPlural"/> if none is found</returns>
        public virtual string GetParticularPluralString(string msgctxt, string msgid, string msgidPlural, long n, CultureInfo culture)
        {
            foreach (GettextResourceSet rs in GetResourceSetsFor(culture))
            {
                string translation = rs.GetPluralString(MakeContextMsgid(msgctxt, msgid), msgidPlural, n);
                if (!string.IsNullOrEmpty(translation))
                    return translation;
                // Fallback to non cotextual translation
                translation = rs.GetPluralString(msgid, msgidPlural, n);
                if (!string.IsNullOrEmpty(translation))
                    return translation;
            }
            // Fallback: Germanic plural form.
            return (n == 1 ? msgid : msgidPlural);
        }

        /// <summary>
        /// Returns the translation of <paramref name="msgid"/> in the current culture.
        /// </summary>
        /// <param name="msgid">the key string to be translated</param>
        /// <returns>the translation of <paramref name="msgid"/>, or
        ///          <paramref name="msgid"/> if none is found</returns>
        public override string GetString(string msgid)
        {
            return GetString(msgid, CultureInfo.CurrentUICulture);
        }

        /// <summary>
        /// Returns the formatted translation of <paramref name="msgid"/> in the current culture.
        /// </summary>
        /// <returns>
        /// Formatted translated or original message
        /// </returns>
        /// <param name="msgid">the key string to be translated</param>
        /// <param name="args">
        /// Arguments to apply with given format.
        /// </param>
        public virtual string GetStringFmt(string msgid, params object[] args)
        {
            return string.Format(GetString(msgid, CultureInfo.CurrentUICulture), args);
        }

        /// <summary>
        /// Returns the translation of <paramref name="msgid"/> and
        /// <paramref name="msgidPlural"/> in the current culture, choosing the
        /// right plural form depending on the number <paramref name="n"/>.
        /// </summary>
        /// <param name="msgid">the key string to be translated, an ASCII
        ///                     string</param>
        /// <param name="msgidPlural">the English plural of <paramref name="msgid"/>,
        ///                           an ASCII string</param>
        /// <param name="n">the number, should be &gt;= 0</param>
        /// <returns>the translation, or <paramref name="msgid"/> or
        ///          <paramref name="msgidPlural"/> if none is found</returns>
        public virtual string GetPluralString(string msgid, string msgidPlural, long n)
        {
            return GetPluralString(msgid, msgidPlural, n, CultureInfo.CurrentUICulture);
        }

        /// <summary>
        /// Returns the formatted translation of <paramref name="msgid"/> and
        /// <paramref name="msgidPlural"/> in the current culture, choosing the
        /// right plural form depending on the number <paramref name="n"/>.
        /// </summary>
        /// <param name="msgid">the key string to be translated, an ASCII
        ///                     string</param>
        /// <param name="msgidPlural">the English plural of <paramref name="msgid"/>,
        ///                           an ASCII string</param>
        /// <param name="n">the number, should be &gt;= 0</param>
        /// <returns>the translation, or <paramref name="msgid"/> or
        ///          <paramref name="msgidPlural"/> if none is found</returns>
        public virtual string GetPluralStringFmt(string msgid, string msgidPlural, long n)
        {
            return string.Format(GetPluralString(msgid, msgidPlural, n, CultureInfo.CurrentUICulture), n);
        }

        /// <summary>
        /// Returns the translation of <paramref name="msgid"/> in the context
        /// of <paramref name="msgctxt"/> in the current culture.
        /// </summary>
        /// <param name="msgctxt">the context for the key string, an ASCII
        ///                       string</param>
        /// <param name="msgid">the key string to be translated, an ASCII
        ///                     string</param>
        /// <returns>the translation of <paramref name="msgid"/>, or
        ///          <paramref name="msgid"/> if none is found</returns>
        public string GetParticularString(string msgctxt, string msgid)
        {
            return GetParticularString(msgctxt, msgid, CultureInfo.CurrentUICulture);
        }

        /// <summary>
        /// Returns the translation of <paramref name="msgid"/> and
        /// <paramref name="msgidPlural"/> in the context of
        /// <paramref name="msgctxt"/> in the current culture, choosing the
        /// right plural form depending on the number <paramref name="n"/>.
        /// </summary>
        /// <param name="msgctxt">the context for the key string, an ASCII
        ///                       string</param>
        /// <param name="msgid">the key string to be translated, an ASCII
        ///                     string</param>
        /// <param name="msgidPlural">the English plural of <paramref name="msgid"/>,
        ///                           an ASCII string</param>
        /// <param name="n">the number, should be &gt;= 0</param>
        /// <returns>the translation, or <paramref name="msgid"/> or
        ///          <paramref name="msgidPlural"/> if none is found</returns>
        public virtual string GetParticularPluralString(string msgctxt, string msgid, string msgidPlural, long n)
        {
            return GetParticularPluralString(msgctxt, msgid, msgidPlural, n, CultureInfo.CurrentUICulture);
        }

    }
}
