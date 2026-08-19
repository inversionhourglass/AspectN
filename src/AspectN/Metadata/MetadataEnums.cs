namespace AspectN.Metadata;

/// <summary>Specifies the type kind values.</summary>
public enum TypeKind
{
    Unknown,
    Class,
    Struct,
    Interface,
    Enum,
    Delegate
}

/// <summary>Specifies the type signature kind values.</summary>
public enum TypeSignatureKind
{
    Named,
    Array,
    GenericParameter,
    Unsupported
}

/// <summary>Specifies the generic parameter owner kind values.</summary>
public enum GenericParameterOwnerKind
{
    Type,
    Method
}

/// <summary>Specifies the ref kind values.</summary>
public enum RefKind
{
    None,
    Ref,
    Out,
    In,
    RefReadOnly
}

/// <summary>Specifies the type use kind values.</summary>
public enum TypeUseKind
{
    Other,
    Parameter,
    Return,
    Property,
    DeclaringType,
    GenericArgument
}

/// <summary>Specifies the callable kind values.</summary>
public enum CallableKind
{
    OrdinaryMethod,
    PropertyGetter,
    PropertySetter,
    EventAdder,
    EventRemover,
    EventRaiser,
    Operator,
    Conversion,
    InstanceConstructor,
    StaticConstructor
}

/// <summary>Specifies the accessibility values.</summary>
public enum Accessibility
{
    Unknown,
    Public,
    Protected,
    Internal,
    Private,
    ProtectedInternal,
    PrivateProtected
}

/// <summary>Specifies the attribute target kind values.</summary>
public enum AttributeTargetKind
{
    Type,
    Callable,
    Property,
    Parameter,
    Return
}
