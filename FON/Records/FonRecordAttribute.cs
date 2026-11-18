namespace FON.Records;


/// <summary>
/// Marks a class, struct or record that the FON source generator maps to and from a
/// <see cref="FON.Types.FonCollection"/>. The type must be <c>partial</c>; the generator
/// implements <see cref="IFonRecord{TSelf}"/> on it.
/// </summary>
/// <remarks>
/// Every public instance property with a getter is written under its own name, in declaration
/// order; a null value writes no key. Reading uses the public constructor with the most
/// parameters whose names all match property names, or else the public parameterless constructor
/// and every settable property. A member of a type the generator cannot map is a compile-time
/// error. Nested record types used as members or list elements must be marked too.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class FonRecordAttribute : Attribute {
}
