﻿using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Globalization;
using System.Diagnostics;
using GNU.Getopt;

namespace GNU.Gettext.Msgfmt;

public static class Program
{
    public const string SOpts = "-:hvo:d:r:l:L:";
    public static LongOpt[] LOpts
    {
        get
        {
            LongOpt[] lopts = new LongOpt[]
            {
                new LongOpt("help", Argument.No, null, 'h'),
                new LongOpt("resource", Argument.Required, null, 'r'),
                new LongOpt("output-file", Argument.Required, null, 'o'),
                new LongOpt("locale", Argument.Required, null, 'l'),
                new LongOpt("lib-dir", Argument.Required, null, 'L'),
                new LongOpt("debug", Argument.No, null, 4),
                new LongOpt("check-format", Argument.No, null, 2),
                new LongOpt("csharp-resources", Argument.No, null, 3)
            };
            return lopts;
        }
    }

    static int Main(string[] args)
    {
        Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

        Options options = new();
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }
        if (!GetOptions(args, SOpts, LOpts, options, out StringBuilder message))
        {
            Console.WriteLine(message.ToString());
            return 1;
        }
        if (options.ShowUsage)
        {
            PrintUsage();
            return 0;
        }
        if (!AnalyseOptions(options, out message))
        {
            Console.WriteLine(message.ToString());
            return 1;
        }

        try
        {
            switch (options.Mode)
            {
                case Mode.Resources:
                    (new ResourcesGen(options)).Run();
                    Console.WriteLine("Resource created OK");
                    break;
                case Mode.SateliteAssembly:
                    (new AssemblyGen(options)).Run();
                    Console.WriteLine("Done");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error during execution: {0}", ex.Message);
            return 1;
        }
        return 0;
    }

    public static bool GetOptions(string[] args, string sopts, LongOpt[] lopts, Options options, out StringBuilder message)
    {
        message = new StringBuilder();
        Getopt.Getopt getopt = new(
            Assembly.GetExecutingAssembly().GetName().Name,
            args, sopts, lopts)
        {
            Opterr = false
        };

        options.Mode = Mode.SateliteAssembly;
        options.ShowUsage = false;
        options.CheckFormat = false;
        options.DebugMode = false;

        int option;
        while ((option = getopt.getopt()) != -1)
        {
            switch (option)
            {
                case 1:
                    options.InputFiles.Add(getopt.Optarg);
                    break;
                case 2:
                    options.CheckFormat = true;
                    break;
                case 3:
                    options.Mode = Mode.Resources;
                    break;
                case 4:
                    options.DebugMode = true;
                    Trace.WriteLine("Debug mode is ON");
                    break;
                case ':':
                    message.AppendFormat("Option {0} requires an argument", getopt.OptoptStr);
                    return false;
                case '?':
                    message.AppendFormat("Invalid option '{0}'", getopt.OptoptStr);
                    return false;
                case 'r':
                    options.BaseName = getopt.Optarg;
                    break;
                case 'o':
                    options.OutFile = getopt.Optarg;
                    break;
                case 'd':
                    options.OutDir = getopt.Optarg;
                    break;
                case 'l':
                    options.LocaleStr = getopt.Optarg;
                    break;
                case 'L':
                    options.LibDir = getopt.Optarg;
                    break;
                case 'h':
                    options.ShowUsage = true;
                    return true;
                default:
                    PrintUsage();
                    return false;
            }
        }

        if (getopt.Opterr)
        {
            message.AppendLine();
            message.Append("Error in command line options. Use -h to read options usage");
            return false;
        }
        return true;
    }

    public static bool AnalyseOptions(Options options, out StringBuilder message)
    {
        message = new StringBuilder();
        bool accepted = true;
        try
        {
            if (options.InputFiles.Count == 0)
            {
                message.Append("You must specify at least one input file");
                accepted = false;
            }

            if (accepted)
            {
                foreach (string fileName in options.InputFiles)
                {
                    if (!File.Exists(fileName))
                    {
                        message.AppendFormat("File {0} not found", fileName);
                        accepted = false;
                    }
                }
            }


            if (accepted && options.Mode == Mode.Resources && string.IsNullOrEmpty(options.OutFile))
            {
                message.Append("Undefined output file name");
                accepted = false;
            }

            if (accepted && options.Mode == Mode.SateliteAssembly)
            {
                if (string.IsNullOrEmpty(options.BaseName))
                {
                    message.Append("Undefined base name");
                    accepted = false;
                }
                if (accepted && string.IsNullOrEmpty(options.OutDir))
                {
                    message.Append("Output dirictory name required");
                    accepted = false;
                }
                if (accepted && string.IsNullOrEmpty(options.LocaleStr))
                {
                    message.Append("Locale is not defined");
                    accepted = false;
                }
                else if (accepted)
                {
                    options.Locale = new CultureInfo(options.LocaleStr);
                }

                if (accepted && options.Locale == null)
                {
                    message.AppendFormat("Cannot create culture from {0}", options.LocaleStr);
                    accepted = false;
                }
            }
        }
        catch (Exception e)
        {
            message.Append(e.Message);
            accepted = false;
        }

        if (!accepted)
        {
            message.AppendLine();
            message.Append("Error accepting options");
            return false;
        }

        return true;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Msgfmt (Gettext.NET tools)");
        Console.WriteLine("Custom message formatter from *.po to satellite assembly DLL or to *.resources files");
        Console.WriteLine();
        Console.WriteLine("Usage:\n" +
            "    {0}[.exe] [OPTIONS] filename.po ...\n",
            Assembly.GetExecutingAssembly().GetName().Name);
        Console.WriteLine(
            "   -r resource, --resource=resource    Base name for resources catalog i.e. 'Solution1.App2.Module3'\n" +
            "                                       Note that '{0}' suffix will be added for using by GettextResourceManager\n\n" +
            "   -o file, --output-file=file         Output file name for .NET resources file.\n" +
            "                                       Ignored when -d is specified\n\n" +
            "   -d directory                        Output directory for satellite assemblies.\n" +
            "                                       Subdirectory for specified locale will be created\n\n" +
            "   -l locale, --locale=locale          .NET locale (culture) name i.e. \"en-US\", \"en\" etc.\n\n" +
            "   -L path                             Path to directory where GNU.Gettext.dll is located (need to compile DLL)\n\n" +
            "   --check-format                      Verify C# format strings and raise error if invalid format is detected\n\n" +
            "   --csharp-resources                  Convert a PO file to a .resources file instead of satellite assembly\n\n" +
            "   -h, --help                          Display this help and exit",
            GettextResourceManager.ResourceNameSuffix
            );
    }
}
