using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using SDL2;
using Yafc.Model;
using Yafc.UI;

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

    static readonly IEnumerable<object> icons = ((List<(IntPtr, List<(string file, SDL.SDL_Rect source, SDL.SDL_Rect dest)>)>)typeof(IconCollection).GetField("icons", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!)
        .Select(i => i.Item2.Select(i => new { i.file, i.source, i.dest }));

    private static void PrintObjects(string file) {
        Project.current = new();
        using StreamWriter sw = new(file);
        foreach (FactorioObject obj in Database.objects.all.OrderBy(o => o.typeDotName)) {
            sw.WriteLine($$"""Database.objectsByTypename["{{obj.typeDotName}}"] = new {{obj.GetType().Name}} {""");
            writeMembers(sw, obj, 4);
            sw.WriteLine("};");
        }

        static void writeMembers(StreamWriter sw, object obj, int indent, bool allProperties = false) {
            foreach (var property in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).OrderBy(p => p.Name)) {
                if (property.Name is "id" || property.GetMethod is null) {
                    continue;
                }
                else if (property.Name == "icon") {
                    writeMember(sw, indent, property, icons.Skip((int)property.GetValue(obj)!).First());
                }
                else if (allProperties || property.SetMethod is not null || (property.PropertyType.IsAssignableTo(typeof(IEnumerable)) && property.PropertyType != typeof(string))) {
                    writeMember(sw, indent, property, property.GetValue(obj));
                }
            }

            foreach (var field in obj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(f => f.Name)) {
                writeMember(sw, indent, field, field.GetValue(obj));
            }
        }

        static void writeMember(StreamWriter sw, int indent, MemberInfo member, object? value) {
            sw.Write(new string(' ', indent) + $"{member.Name} = ");
            writeValue(sw, indent, value);
            sw.WriteLine();
        }

        static void writeValue(StreamWriter sw, int indent, object? value) {
            if (value != null && value.GetType().IsGenericType && value.GetType().GetGenericTypeDefinition() == typeof(Lazy<>)) {
                value = value.GetType().GetProperty("Value")!.GetValue(value, null);
            }
            switch (value) {
                case null:
                    sw.Write($"null,");
                    break;
                case FactorioObject obj:
                    sw.Write($"""Database.objectsByTypename["{obj.typeDotName}"],""");
                    break;
                case string str:
                    if (str.Contains('\n')) {
                        sw.WriteLine("\"\"\"");
                        sw.WriteLine(str);
                        sw.Write("\"\"\",");
                    }
                    else {
                        sw.Write($"\"{str.Replace("\\", @"\\").Replace("\"", @"\""")}\",");
                    }
                    break;
                case IEnumerable enumerable:
                    writeEnumerable(sw, indent, enumerable.Cast<object>());
                    break;
                case Ingredient or Product or EffectReceiver or EntityEnergy or FactorioIconPart or Effect or ModuleSpecification or SDL.SDL_Rect:
                    sw.WriteLine("new() {");
                    writeMembers(sw, value, indent + 4);
                    sw.Write(new string(' ', indent) + "},");
                    break;
                case TemperatureRange range:
                    if (range.min == TemperatureRange.Any.min && range.max == TemperatureRange.Any.max) {
                        sw.Write($"TemperatureRange.Any,");
                    }
                    else if (range.min == range.max) {
                        sw.Write($"new TemperatureRange({range.min}),");
                    }
                    else {
                        sw.Write($"new TemperatureRange({range.min}, {range.max}),");
                    }
                    break;
                case true:
                    sw.Write("true,");
                    break;
                case false:
                    sw.Write("false,");
                    break;
                case float.PositiveInfinity:
                    sw.Write("float.PositiveInfinity,");
                    break;
                case double.PositiveInfinity:
                    sw.Write("double.PositiveInfinity,");
                    break;
                default:
                    var type = value.GetType();
                    if (type.IsGenericType && type.Name.Contains("AnonymousType") && type.GetCustomAttribute<CompilerGeneratedAttribute>() != null) {
                        sw.WriteLine("new {");
                        writeMembers(sw, value, indent + 4, true);
                        sw.Write(new string(' ', indent) + "},");
                    }
                    else {
                        sw.Write($"{value},");
                    }
                    break;
            }
        }

        static void writeEnumerable(StreamWriter sw, int indent, IEnumerable<object> enumerable) {
            switch (enumerable.Count()) {
                case 0:
                    sw.Write("[],");
                    return;
                case 1:
                    sw.Write("[ ");
                    writeValue(sw, indent, enumerable.First());
                    sw.Write(" ],");
                    return;
            }

            sw.WriteLine('[');

            if (enumerable is IEnumerable<IComparable>) {
                enumerable = enumerable.Order();
            }
            else if (enumerable is IEnumerable<FactorioObject> fobjs) {
                enumerable = fobjs.OrderBy(o => o.typeDotName);
            }

            foreach (object item in enumerable) {
                sw.Write(new string(' ', indent + 4));
                writeValue(sw, indent + 4, item);
                sw.WriteLine();
            }

            sw.Write(new string(' ', indent) + "],");
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
