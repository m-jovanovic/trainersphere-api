using System.Reflection;

namespace Domain.Primitives;

/// <summary>
/// Represents an enumeration of objects with a unique numeric value and a name.
/// </summary>
/// <typeparam name="TEnum">The type of the enumeration.</typeparam>
public abstract class Enumeration<TEnum> : IEquatable<Enumeration<TEnum>>
    where TEnum : Enumeration<TEnum>
{
    private static readonly Lazy<Dictionary<int, TEnum>> EnumerationsDictionary = new(() => CreateEnumerationDictionary(typeof(TEnum)));

    /// <summary>
    /// Initializes a new instance of the <see cref="Enumeration{TEnum}"/> class.
    /// </summary>
    /// <param name="value">The enumeration value.</param>
    /// <param name="name">The enumeration name.</param>
    protected Enumeration(int value, string name)
        : this()
    {
        Value = value;
        Name = name;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Enumeration{TEnum}"/> class.
    /// </summary>
    /// <remarks>
    /// Required for deserialization.
    /// </remarks>
    protected Enumeration() => Name = string.Empty;

    /// <summary>
    /// Gets the value.
    /// </summary>
    public int Value { get; private init; }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; private init; }

    public static bool operator ==(Enumeration<TEnum>? a, Enumeration<TEnum>? b)
    {
        if (a is null && b is null)
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        return a.Equals(b);
    }

    public static bool operator !=(Enumeration<TEnum> a, Enumeration<TEnum> b) => !(a == b);

    /// <summary>
    /// Gets the enumeration values.
    /// </summary>
    /// <returns>The read-only collection of enumeration values.</returns>
    public static IReadOnlyCollection<TEnum> GetValues() => EnumerationsDictionary.Value.Values.ToList();

    /// <summary>
    /// Creates an enumeration of the specified type based on the specified value.
    /// </summary>
    /// <param name="value">The enumeration value.</param>
    /// <returns>The enumeration instance that matches the specified value, if it exists.</returns>
    public static TEnum? FromValue(int value) => EnumerationsDictionary.Value.TryGetValue(value, out TEnum? enumeration) ? enumeration : null;

    /// <summary>
    /// Creates an enumeration of the specified type based on the specified name.
    /// </summary>
    /// <param name="name">The enumeration name.</param>
    /// <returns>The enumeration instance that matches the specified name, if it exists.</returns>
    public static TEnum? FromName(string name) => EnumerationsDictionary.Value.Values.SingleOrDefault(x => x.Name == name);

    /// <summary>
    /// Checks if the enumeration with the specified value exists.
    /// </summary>
    /// <param name="value">The enumeration value.</param>
    /// <returns>True if an enumeration with the specified value exists, otherwise false.</returns>
    public static bool ContainsValue(int value) => EnumerationsDictionary.Value.ContainsKey(value);

    /// <inheritdoc />
    public virtual bool Equals(Enumeration<TEnum>? other)
    {
        if (other is null)
        {
            return false;
        }

        return GetType() == other.GetType() && other.Value.Equals(Value);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (GetType() != obj.GetType())
        {
            return false;
        }

        return obj is Enumeration<TEnum> otherValue && otherValue.Value.Equals(Value);
    }

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode() * 37;

    private static Dictionary<int, TEnum> CreateEnumerationDictionary(Type enumType) => GetFieldsForType(enumType).ToDictionary(t => t.Value);

    private static IEnumerable<TEnum> GetFieldsForType(Type enumType) =>
        enumType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(fieldInfo => enumType.IsAssignableFrom(fieldInfo.FieldType))
            .Select(fieldInfo => (TEnum)fieldInfo.GetValue(default)!);
}
