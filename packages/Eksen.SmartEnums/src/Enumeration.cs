using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Eksen.Core;

namespace Eksen.SmartEnums;

[ExcludeFromCodeCoverage]
public abstract record Enumeration<TSelf> : IEnumeration, IComparable<TSelf> where TSelf : Enumeration<TSelf>
{
    private static Dictionary<string, TSelf>? _items;

    public string Code { get; }

    protected Enumeration([CallerMemberName] string? code = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException(message: "Enumeration name cannot be null or empty", nameof(code));
        }

        Code = code;
    }

    public static int MaxLength
    {
        get
        {
            EnsureItemsInitialized();

            return _items!.Values.Max(x => x.Code.Length);
        }
    }

    public static IReadOnlyCollection<TSelf> GetValues()
    {
        EnsureItemsInitialized();

        return _items!.Values;
    }

    public static TSelf Parse(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException($"{typeof(TSelf).Name}.Parse was called with null argument", nameof(code));
        }

        EnsureItemsInitialized();

        code = code.Replace(oldValue: " ", newValue: "").Trim();

        var match = _items!
            .Values
            .FirstOrDefault(s => string.Equals(s.Code.Replace(oldValue: " ", newValue: ""), code, StringComparison.OrdinalIgnoreCase));

        return match ?? throw CoreErrors.ObjectNotFound.Raise(typeof(TSelf), code);
    }

    public override string ToString()
    {
        return Code;
    }

    private static void EnsureItemsInitialized()
    {
        if (_items != null)
        {
            return;
        }

        var members = typeof(TSelf)
            .GetFields(BindingFlags.Public
                       | BindingFlags.Static
                       | BindingFlags.DeclaredOnly)
            .Select(f => f.GetValue(obj: null))
            .Cast<TSelf>()
            .ToList();

        var codeDuplicates = members
            .GroupBy(x => x.Code.Trim().Replace(oldValue: " ", newValue: "").ToLowerInvariant())
            .Where(group => group.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        if (codeDuplicates.Count > 0)
        {
            throw new Exception($"Enumeration {typeof(TSelf).Name} has duplicate codes: {string.Join(separator: ", ", codeDuplicates)}");
        }

        _items = members.ToDictionary(k => k.Code, v => v);
    }

    public int CompareTo(object? obj)
    {
        return string.Compare(Code, ((IEnumeration?)obj)?.Code, StringComparison.Ordinal);
    }

    public int CompareTo(TSelf? other)
    {
        return string.Compare(Code, other?.Code, StringComparison.Ordinal);
    }

    public override int GetHashCode()
    {
        return Code.GetHashCode();
    }
}