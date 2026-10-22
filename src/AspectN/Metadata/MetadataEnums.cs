namespace AspectN.Metadata;

/// <summary>Specifies the type kind values.</summary>
public enum TypeKind
{
    /// <summary>An unspecified type classification; query targets may use it to leave the kind unconstrained.</summary>
    Unknown,
    /// <summary>A class type.</summary>
    Class,
    /// <summary>A non-enum value type.</summary>
    Struct,
    /// <summary>An interface type.</summary>
    Interface,
    /// <summary>An enumeration type.</summary>
    Enum,
    /// <summary>A delegate type.</summary>
    Delegate
}

/// <summary>Specifies the type signature kind values.</summary>
public enum TypeSignatureKind
{
    /// <summary>A named type definition or constructed named type.</summary>
    Named,
    /// <summary>An array with an element type, rank, and shape.</summary>
    Array,
    /// <summary>A type or method generic parameter identified by its declaration slot.</summary>
    GenericParameter,
    /// <summary>A type shape represented without structural matching support.</summary>
    Unsupported
}

/// <summary>Specifies the generic parameter owner kind values.</summary>
public enum GenericParameterOwnerKind
{
    /// <summary>A generic parameter declared by a type.</summary>
    Type,
    /// <summary>A generic parameter declared by a method.</summary>
    Method
}

/// <summary>Specifies the ref kind values.</summary>
public enum RefKind
{
    /// <summary>A non-by-reference use.</summary>
    None,
    /// <summary>A ref parameter or writable by-reference return.</summary>
    Ref,
    /// <summary>An out parameter.</summary>
    Out,
    /// <summary>An in parameter.</summary>
    In,
    /// <summary>A ref readonly parameter or return.</summary>
    RefReadOnly
}

/// <summary>Specifies the type use kind values.</summary>
public enum TypeUseKind
{
    /// <summary>A type use outside the other classified signature positions.</summary>
    Other,
    /// <summary>The type of a callable parameter.</summary>
    Parameter,
    /// <summary>The return type of a callable.</summary>
    Return,
    /// <summary>The type of a property.</summary>
    Property,
    /// <summary>The type declaring a member.</summary>
    DeclaringType,
    /// <summary>A type supplied in a generic argument slot.</summary>
    GenericArgument
}

/// <summary>Specifies the callable kind values.</summary>
public enum MethodKind
{
    /// <summary>An ordinary method without a more specific callable classification.</summary>
    OrdinaryMethod,
    /// <summary>A property get accessor.</summary>
    PropertyGetter,
    /// <summary>A property set accessor.</summary>
    PropertySetter,
    /// <summary>An event add accessor.</summary>
    EventAdder,
    /// <summary>An event remove accessor.</summary>
    EventRemover,
    /// <summary>An event raise accessor.</summary>
    EventRaiser,
    /// <summary>An operator method recognized from metadata.</summary>
    Operator,
    /// <summary>A conversion operator method.</summary>
    Conversion,
    /// <summary>An instance constructor.</summary>
    InstanceConstructor,
    /// <summary>A type initializer or static constructor.</summary>
    StaticConstructor
}

/// <summary>Specifies the accessibility values.</summary>
public enum Accessibility
{
    /// <summary>Accessibility has not been classified.</summary>
    Unknown,
    /// <summary>Public access.</summary>
    Public,
    /// <summary>Access within the declaring type and by derived types.</summary>
    Protected,
    /// <summary>Access within the declaring assembly.</summary>
    Internal,
    /// <summary>Private access within the declaring type.</summary>
    Private,
    /// <summary>Access within the declaring assembly or by derived types.</summary>
    ProtectedInternal,
    /// <summary>Access within the declaring type and by derived types in the declaring assembly.</summary>
    PrivateProtected
}

/// <summary>Specifies the attribute target kind values.</summary>
public enum AttributeTargetKind
{
    /// <summary>Attributes declared directly on a type.</summary>
    Type,
    /// <summary>Attributes declared directly on a method.</summary>
    Method,
    /// <summary>Attributes declared directly on a property.</summary>
    Property,
    /// <summary>Attributes declared directly on a parameter.</summary>
    Parameter,
    /// <summary>Attributes declared directly on a return value.</summary>
    Return
}
