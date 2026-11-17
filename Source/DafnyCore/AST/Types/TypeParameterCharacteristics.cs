namespace Microsoft.Dafny;

public struct TypeParameterCharacteristics {
  public SourceOrigin SourceOrigin = null;
  public TypeParameter.EqualitySupportValue EqualitySupport;  // the resolver may change this value from Unspecified to InferredRequired (for some signatures that may immediately imply that equality support is required)
  public Type.AutoInitInfo AutoInit;
  public bool HasCompiledValue => AutoInit == Type.AutoInitInfo.CompilableValue;
  public bool IsNonempty => AutoInit != Type.AutoInitInfo.MaybeEmpty;
  public bool ContainsNoReferenceTypes;
  /// <summary>
  /// The characteristic (!reads): the type contains no function that reads the heap.  A type parameter
  /// with it may be stored in memory (https://github.com/erniecohen/dafny/issues/481).
  /// </summary>
  public bool ContainsNoReadingFunctions;

  public static TypeParameterCharacteristics Default() {
    return new TypeParameterCharacteristics {
      EqualitySupport = TypeParameter.EqualitySupportValue.Unspecified,
      AutoInit = Type.AutoInitInfo.MaybeEmpty,
      ContainsNoReferenceTypes = false,
      ContainsNoReadingFunctions = false,
    };
  }

  public TypeParameterCharacteristics(TypeParameter.EqualitySupportValue equalitySupport,
    Type.AutoInitInfo autoInit, bool containsNoReferenceTypes, bool containsNoReadingFunctions = false) {
    EqualitySupport = equalitySupport;
    AutoInit = autoInit;
    ContainsNoReferenceTypes = containsNoReferenceTypes;
    ContainsNoReadingFunctions = containsNoReadingFunctions;
  }
  public override string ToString() {
    string result = "";
    if (EqualitySupport == TypeParameter.EqualitySupportValue.Required) {
      result += ",==";
    }
    if (HasCompiledValue) {
      result += ",0";
    }
    if (AutoInit == Type.AutoInitInfo.Nonempty) {
      result += ",00";
    }
    if (ContainsNoReferenceTypes) {
      result += ",!new";
    }
    if (ContainsNoReadingFunctions) {
      result += ",!reads";
    }
    if (result.Length != 0) {
      result = "(" + result.Substring(1) + ")";
    }
    return result;
  }
}