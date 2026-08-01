namespace AspectN.Metadata;

public enum TypeKind
{
    Unknown,
    Class,
    Struct,
    Interface,
    Enum,
    Delegate
}

public enum TypeSignatureKind
{
    Named,
    Array,
    GenericParameter,
    Unsupported
}

public enum GenericParameterOwnerKind
{
    Type,
    Method
}

public enum RefKind
{
    None,
    Ref,
    Out,
    In,
    RefReadOnly
}

public enum TypeUseKind
{
    Other,
    Parameter,
    Return,
    Property,
    DeclaringType,
    GenericArgument
}

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

public enum AttributeTargetKind
{
    Type,
    Callable,
    Property,
    Parameter,
    Return
}
