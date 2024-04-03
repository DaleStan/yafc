﻿using System.IO;
using System;

namespace YAFC {
    public static class CommandLineParser {
        public static string lastError { get; private set; } = string.Empty;
        public static bool helpRequested { get; private set; }

        public static bool errorOccured => !string.IsNullOrEmpty(lastError);

        public static ProjectDefinition Parse(string[] args) {
            ProjectDefinition options = new ProjectDefinition();

            if (args.Length == 0) {
                return options;
            }

            if (!args[0].StartsWith("--")) {
                options.dataPath = args[0];
                if (!Directory.Exists(options.dataPath)) {
                    lastError = $"Data path '{options.dataPath}' does not exist.";
                    return null;
                }
            }

            for (int i = string.IsNullOrEmpty(options.dataPath) ? 0 : 1; i < args.Length; i++) {
                switch (args[i]) {
                    case "--mods-path":
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) {
                            options.modsPath = args[++i];

                            if (!Directory.Exists(options.modsPath)) {
                                lastError = $"Mods path '{options.modsPath}' does not exist.";
                                return null;
                            }
                        }
                        else {
                            lastError = "Missing argument for --mods-path.";
                            return null;
                        }
                        break;

                    case "--project-file":
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) {
                            options.path = args[++i];

                            if (!File.Exists(options.path)) {
                                lastError = $"Project file '{options.path}' does not exist.";
                                return null;
                            }
                        }
                        else {
                            lastError = "Missing argument for --project-file.";
                            return null;
                        }
                        break;

                    case "--expensive":
                        options.expensive = true;
                        break;

                    case "--help":
                        helpRequested = true;
                        break;

                    default:
                        lastError = $"Unknown argument '{args[i]}'.";
                        return null;
                }
            }

            return options;
        }

        public static void PrintHelp() {
            Console.WriteLine("Usage:");
            Console.WriteLine("YAFC [<data-path>] [--mods-path <path>] [--project-file <path>] [--expensive] [--help]");
            Console.WriteLine();
            Console.WriteLine("Description:");
            Console.WriteLine("    YAFC can be started without any arguments. However, if arguments");
            Console.WriteLine("    are supplied, it is mandatory that the first argument is the path");
            Console.WriteLine("    to the data directory of Factorio. The other arguments are optional");
            Console.WriteLine("    in any case.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("    <data-path>");
            Console.WriteLine("        Path of the data directory (mandatory, if other arguments are supplied)");
            Console.WriteLine();
            Console.WriteLine("    --mods-path <path>");
            Console.WriteLine("        Path of the mods directory (optional)");
            Console.WriteLine();
            Console.WriteLine("    --project-file <path>");
            Console.WriteLine("        Path of the project file (optional)");
            Console.WriteLine();
            Console.WriteLine("    --expensive");
            Console.WriteLine("        Enable expensive mode (optional)");
            Console.WriteLine();
            Console.WriteLine("    --help");
            Console.WriteLine("        Display this help message and exit");
        }
    }
}
