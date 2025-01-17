//
// CatalogEntry.cs
//
// Author:
//   David Makovsk� <yakeen@sannyas-on.net>
//
// Copyright (C) 1999-2006 Vaclav Slavik (Code and design inspiration - poedit.org)
// Copyright (C) 2007 David Makovsk�
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
using System.Collections.Generic;

namespace GNU.Gettext;

// This class holds information about one particular string.
// This includes original string and its occurences in source code
// (so-called references), translation and translation's status
// (fuzzy, non translated, translated) and optional comment.
public class CatalogEntry
{
    public enum Validity
    {
        Unknown,
        Invalid,
        Valid
    }

    string str, plural;
    bool hasPlural;
    List<string> translations;

    readonly List<string> references;
    readonly List<string> autocomments;
    bool isFuzzy;
    readonly bool hasBadTokens;
    string moreFlags;
    string comment;
    string context = string.Empty;

    readonly Catalog owner;

    // Initializes the object with original string and translation.
    public CatalogEntry(Catalog owner, string str, string plural)
    {
        this.owner = owner;
        this.str = str;
        this.plural = plural;

        hasPlural = !string.IsNullOrEmpty(plural);
        references = new List<string>();
        autocomments = new List<string>();
        translations = new List<string>();
        isFuzzy = false;
        IsModified = false;
        IsAutomatic = false;
        DataValidity = Validity.Unknown;
    }

    public CatalogEntry(Catalog owner, CatalogEntry dt)
    {
        this.owner = owner;
        str = dt.str;
        plural = dt.plural;
        hasPlural = dt.hasPlural;
        translations = new List<string>(dt.translations);
        references = new List<string>(dt.references);
        autocomments = new List<string>(dt.autocomments);
        isFuzzy = dt.isFuzzy;
        IsModified = dt.IsModified;
        IsAutomatic = dt.IsAutomatic;
        hasBadTokens = dt.hasBadTokens;
        moreFlags = dt.moreFlags;
        comment = dt.comment;
        DataValidity = dt.DataValidity;
        ErrorString = dt.ErrorString;
        context = dt.Context;
    }

    // Returns the original string.
    public string String
    {
        get { return str; }
    }

    // Does this entry have a msgid_plural?
    public bool HasPlural
    {
        get { return hasPlural; }
    }

    // Returns the plural string.
    public string PluralString
    {
        get { return plural; }
    }

    // How many translations (plural forms) do we have?
    public int NumberOfTranslations
    {
        get { return translations.Count; }
    }

    public int TranslationsCount
    {
        get { return translations.Count; }
    }

    // Returns the nth-translation.
    public string GetTranslation(int index)
    {
        if (index < 0 || index >= translations.Count)
            return string.Empty;
        else
            return translations[index];
    }

    public string Context
    {
        get { return context; }
        set { context = value.Trim(); }
    }

    public bool HasContext
    {
        get { return !string.IsNullOrEmpty(context); }
    }

    public string Key
    {
        get { return MakeKey(String, Context); }
    }

    public static string MakeKey(string msgid, string context)
    {
        if (string.IsNullOrEmpty(msgid))
            throw new ArgumentException("Msgid cannot be empty");
        return string.Format("{0}{1}",
                             string.IsNullOrEmpty(context) ? string.Empty : context.Trim() + '|',
                             msgid);
    }

    // Returns array of all occurences of this string in source code.
    public string[] References
    {
        get { return references.ToArray(); }
    }

    // Returns comment added by the translator to this entry
    public string Comment
    {
        get { return comment; }
        set
        {
            if (comment != value)
            {
                comment = value;
                MarkOwnerDirty();
            }
        }
    }

    // Returns array of all auto comments.
    public string[] AutoComments
    {
        get { return autocomments.ToArray(); }
    }

    // Convenience function: does this entry has a comment?
    public bool HasComment
    {
        get { return !string.IsNullOrEmpty(comment); }
    }

    // Adds new reference to the entry (used by SourceDigger).
    public void AddReference(string reference)
    {
        if (!references.Contains(reference))
            references.Add(reference);
    }

    // Clears references (used by SourceDigger).
    public void ClearReferences()
    {
        references.Clear();
    }

    public bool RemoveReferenceTo(string fileNamePrefix)
    {
        bool result = false;
        for (int i = 0; i < this.references.Count; i++)
        {
            if (references[i].StartsWith(fileNamePrefix))
            {
                references.RemoveAt(i);
                i--;
                result = true;
            }
        }
        return result;
    }

    public void RemoveReference(string reference)
    {
        if (references.Contains(reference))
            references.Remove(reference);
    }

    // Sets the string.
    public void SetString(string str)
    {
        this.str = str;
        DataValidity = Validity.Unknown;
    }

    // Sets the plural form (if applicable).
    public void SetPluralString(string plural)
    {
        this.plural = plural;
        this.hasPlural = !string.IsNullOrEmpty(plural);
    }

    // Sets the translation. Changes "translated" status to true if \a t is not empty.
    public void SetTranslation(string translation, int index)
    {
        while (index >= translations.Count)
            translations.Add(string.Empty);

        if (translations[index] != translation)
        {
            translations[index] = translation;

            DataValidity = Validity.Unknown;
            MarkOwnerDirty();
        }
    }

    // Sets all translations.
    public void SetTranslations(string[] translations)
    {
        this.translations = new List<string>(translations);

        DataValidity = Validity.Unknown;
        MarkOwnerDirty();
    }

    // gettext flags directly in string format. It may be
    // either empty string or "#, fuzzy", "#, c-format",
    // #, csharp-format" or others.
    public string Flags
    {
        get
        {
            string retStr = string.Empty;
            if (isFuzzy)
                retStr = ", fuzzy";
            retStr += moreFlags;
            if (!string.IsNullOrEmpty(retStr))
                return "#" + retStr;
            else
                return string.Empty;
        }
        set
        {
            isFuzzy = false;
            moreFlags = string.Empty;

            if (string.IsNullOrEmpty(value))
                return;

            string[] tokens = value.TrimStart('#', ',').Split(',');
            foreach (string token in tokens)
            {
                if (token.Trim() == "fuzzy")
                    isFuzzy = true;
                else
                    moreFlags += ", " + token.Trim();
            }
        }
    }

    // Fuzzy flag
    public bool IsFuzzy
    {
        get { return isFuzzy; }
        set
        {
            isFuzzy = value;
            MarkOwnerDirty();
        }
    }

    // Translated property
    public bool IsTranslated
    {
        get
        {
            bool isTranslated = translations.Count >= owner.PluralFormsCount ||
    !HasPlural && !string.IsNullOrEmpty(translations[0]);
            if (isTranslated && HasPlural)
            {
                for (int i = 0; i < owner.PluralFormsCount; i++)
                {
                    if (string.IsNullOrEmpty(translations[i]))
                    {
                        isTranslated = false;
                        break;
                    }
                }
            }
            return isTranslated;
        }
    }

    // Modified flag.
    public bool IsModified { get; set; }

    // Automatic translation flag.
    public bool IsAutomatic { get; set; }

    // Returns true if the gettext flags line contains "foo-format"
    // flag when called with "foo" as argument.
    public bool IsInFormat(string format)
    {
        if (string.IsNullOrEmpty(moreFlags))
            return false;
        string lookingFor = string.Format("{0}-format", format);
        string[] tokens = moreFlags.Split(',');
        foreach (string token in tokens)
        {
            if (token.Trim() == lookingFor)
                return true;
        }
        return false;
    }

    // Adds new autocomments (#. )
    public void AddAutoComment(string comment, bool ifNotExists)
    {
        if (!ifNotExists || !autocomments.Contains(comment))
        {
            autocomments.Add(comment);
        }
    }

    public void AddAutoComment(string comment)
    {
        AddAutoComment(comment, false);
    }

    // Clears autocomments.
    public void ClearAutoComments()
    {
        autocomments.Clear();
    }

    // Checks if %i etc. are correct in the translation (true if yes).
    // Strings that are not c-format are always correct.
    // TODO: make it checking for c-sharp, .Net string validity
    public Validity DataValidity { get; set; }

    public string ErrorString { get; set; }

    public string LocaleCode
    {
        get { return owner.LocaleCode; }
    }

    void MarkOwnerDirty()
    {
        if (owner != null)
            owner.IsDirty = true;
    }
}

