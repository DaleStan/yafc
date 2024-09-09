using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Yafc.Model;

namespace Yafc.Parser;

public static partial class FactorioDataSource {
    private static void PrintTable(LuaTable table, string name, string file, bool append) {
        using StreamWriter write = new(file, append);
        write.WriteLine(name + " = {");
        writeTableContent(write, table, 2);
        write.WriteLine('}');

        static void writeTableContent(StreamWriter writer, LuaTable table, int indent) {
            double idx = 1;
            foreach ((object key, object? value) in table.ObjectElements.Order(LuaKeyComparer.Instance)) {
                writer.Write(new string(' ', indent));
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
                        writer.WriteLine('{');
                        writeTableContent(writer, nested, indent + 2);
                        writer.WriteLine(new string(' ', indent) + "},");
                        break;
                    case string str:
                        if (str.LastIndexOf('\n') != str.IndexOf('\n') && !str.Contains("[[") && !str.Contains("]]")) {
                            // Use [[...]] for strings with at least two \n characters, unless they contain [[ or ]].
                            writer.WriteLine("[[");
                            writer.Write(str);
                            writer.WriteLine("]],");
                        }
                        else {
                            // Use C-style strings, escaping control characters, \, and ".
                            writer.WriteLine("\"" + SpecialCharacterRegex().Replace(str, m => m.ToString()[0] switch {
                                '\r' => "\\r",
                                '\n' => "\\n",
                                < ' ' => $"\\{(int)m.ToString()[0]:000}",
                                _ => '\\' + m.ToString(),
                            }) + "\",");
                        }
                        break;
                    case true:
                        writer.WriteLine("true,");
                        break;
                    case false:
                        writer.WriteLine("false,");
                        break;
                    case null:
                        writer.WriteLine("nil, -- This value exists in the Lua table, but cannot be extracted. It is probably a function.");
                        break;
                    default:
                        writer.WriteLine(value + ",");
                        break;
                }
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
