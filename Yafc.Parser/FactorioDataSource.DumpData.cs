using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Yafc.Parser;

public static partial class FactorioDataSource {
    private static void PrintTable(LuaTable table, string name, string file, bool append) {
        using StreamWriter writer = new(file, append);
        writer.Write(name + " = {");
        writeTableContent(writer, table, 2);
        writer.WriteLine('}');

        static void writeTableContent(StreamWriter writer, LuaTable table, int indent) {
            double idx = 1;
            Dictionary<object, object?> elements = table.ObjectElements;
            // To allow single-line output when the single child is a table (including a table with multiple keys), remove `or LuaTable`.
            bool useFullOutput = elements.Count > 1 || (elements.Count == 1 && elements.Values.First() is null or LuaTable);
            if (useFullOutput) {
                writer.WriteLine();
            }
            else {
                writer.Write(' ');
            }

            foreach ((object key, object? value) in elements.Order(LuaKeyComparer.Instance)) {
                if (useFullOutput) {
                    writer.Write(new string(' ', indent));
                }

                if (key as double? == idx) {
                    idx++;
                }
                else if (key is double) {
                    idx = double.NaN;
                    writer.Write($"[{key}] = ");
                }
                else if (IdentifierRegex().IsMatch(key.ToString()!)) {
                    writer.Write($"{key} = ");
                }
                else {
                    writer.Write($"""["{key}"] = """);
                }

                switch (value) {
                    case LuaTable nested:
                        writer.Write('{');
                        writeTableContent(writer, nested, indent + (useFullOutput ? 2 : 0));
                        writer.Write("},");
                        break;
                    case string str:
                        if (str.LastIndexOf('\n') != str.IndexOf('\n') && !str.Contains("[[") && !str.Contains("]]")) {
                            // Use [[...]] for strings with at least two \n characters, unless they contain [[ or ]].
                            writer.WriteLine("[[");
                            writer.Write(str);
                            writer.Write("]],");
                        }
                        else {
                            // Use C-style strings, escaping control characters, \, and ".
                            writer.Write("\"" + SpecialCharacterRegex().Replace(str, m => m.ToString()[0] switch {
                                '\r' => "\\r",
                                '\n' => "\\n",
                                < ' ' => $"\\{(int)m.ToString()[0]:000}",
                                _ => '\\' + m.ToString(),
                            }) + "\",");
                        }
                        break;
                    case true:
                        writer.Write("true,");
                        break;
                    case false:
                        writer.Write("false,");
                        break;
                    case null:
                        writer.Write("nil, -- This value exists in the Lua table, but cannot be extracted. It is probably a function.");
                        break;
                    default:
                        writer.Write(value + ",");
                        break;
                }

                if (useFullOutput) {
                    writer.WriteLine();
                }
            }

            if (useFullOutput) {
                writer.Write(new string(' ', indent - 2));
            }
            else {
                writer.Flush();
                writer.BaseStream.Seek(-1, SeekOrigin.Current);
                writer.BaseStream.WriteByte((byte)' ');
            }
        }
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex IdentifierRegex();
    [GeneratedRegex(@"[\0-\x1F""\\]")]
    private static partial Regex SpecialCharacterRegex();
}

// This is only intended for use when sorting Lua keys, prior to dumping the table data.
file sealed class LuaKeyComparer : IComparer<KeyValuePair<object, object?>> {
    public static LuaKeyComparer Instance = new();
    public int Compare(KeyValuePair<object, object?> left, KeyValuePair<object, object?> right) {
        object l = left.Key;
        object r = right.Key;
        // Sort doubles (array indexes) before named keys
        if (l is string && r is double) {
            return 1;
        }
        if (l is double && r is string) {
            return -1;
        }
        // Sort array indexes and named keys in their natural order.
        if (l is string lString && r is string rString) {
            return lString.CompareTo(rString);
        }
        if (l is double lDouble && r is double rDouble) {
            return lDouble.CompareTo(rDouble);
        }

        throw new NotSupportedException("Lua tables with keys other than numbers or strings are not supported.");
    }
}
