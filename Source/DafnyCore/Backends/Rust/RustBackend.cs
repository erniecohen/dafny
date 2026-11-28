using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.CommandLine;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.Dafny.Compilers;

public class RustBackend : DafnyExecutableBackend {
  protected override bool PreventShadowing => false;
  protected override string InternalFieldPrefix => "_i_";

  public override IReadOnlySet<string> SupportedExtensions => new HashSet<string> { ".rs" };
  public override string TargetName => "Rust";
  public override bool IsStable => true;
  public override bool IsInternal => true;
  public override string TargetExtension => "rs";
  public override int TargetIndentSize => 4;
  public override bool SupportsInMemoryCompilation => false;
  public override bool TextualTargetIsExecutable => false;

  public static readonly Option<string> RustModuleNameOption = new("--rust-module-name",
    @"The enclosing Rust module name for the currently translated code, i.e. what goes between crate:: ...  ::module_name") {
  };
  public static readonly Option<bool> RustSyncOption = new("--rust-sync",
    @"Ensures that all values implement the Sync and Send traits") {
  };
  public override IEnumerable<Option> SupportedOptions => new List<Option> { RustModuleNameOption, RustSyncOption };

  static RustBackend() {
    OptionRegistry.RegisterOption(RustModuleNameOption, OptionScope.Translation);
    OptionRegistry.RegisterOption(RustSyncOption, OptionScope.Translation);
  }

  public override IReadOnlySet<string> SupportedNativeTypes =>
    new HashSet<string> { "byte", "sbyte", "ushort", "short", "uint", "int", "ulong", "long", "udoublelong", "doublelong" };

  public override string TargetBasename(string dafnyProgramName) =>
    Regex.Replace(base.TargetBasename(dafnyProgramName), "[^_A-Za-z0-9]", "_");

  public override string TargetBaseDir(string dafnyProgramName) =>
    $"{Path.GetFileNameWithoutExtension(dafnyProgramName)}-rust/src";

  protected override DafnyWrittenCodeGenerator CreateDafnyWrittenCompiler() {
    return new RustCodeGenerator(Options);
  }

  protected override SinglePassCodeGenerator CreateCodeGenerator() =>
    new GroupCodeGenerator(Options, Reporter, InternalFieldPrefix, PreventShadowing, CanEmitUncompilableCode);

  private class GroupCodeGenerator : DafnyCodeGenerator {
    public bool HasRecursiveGroups;

    public GroupCodeGenerator(DafnyOptions options, ErrorReporter reporter, string fieldPrefix,
      bool preventShadowing, bool canEmitUncompilableCode)
      : base(options, reporter, fieldPrefix, preventShadowing, canEmitUncompilableCode) { }

    private SubsetTypeDecl integerBitwiseNat;

    protected override void EmitHeader(Program program, ConcreteSyntaxTree wr) {
      integerBitwiseNat = program.SystemModuleManager.NatDecl;
      base.EmitHeader(program, wr);
    }

    protected override DAST.Type GenType(Type type) {
      // The builtin nat is an integer at runtime. In particular, a newtype's
      // generated constraint check must not retain an unavailable _System alias.
      // Restrict this normalization to the opt-in interface and builtin identity.
      if (Options.Get(CommonOptionBag.IntegerBitwise) &&
          type.NormalizeExpandKeepConstraints() is UserDefinedType { ResolvedClass: var declaration } &&
          declaration == integerBitwiseNat) {
        return base.GenType(Type.Int);
      }
      return base.GenType(type);
    }

    protected override void EmitIntegerBitwise(ConversionExpr expression, bool inLetExprBody,
      ConcreteSyntaxTree wr, ConcreteSyntaxTree wStmts) {
      var name = ((FunctionCallExpr)expression.E.Resolved).Function.Name;
      if (name is not ("andNat" or "orNat" or "xorNat" or "notNat" or "shlNat" or
          "shrNat" or "rolNat" or "rorNat" or "popNat")) {
        throw new InvalidOperationException("unexpected integer bitwise reference");
      }
      // Widen native operands before entering the runtime and narrow only the
      // final result. Each source operand occurs once, in source order, among
      // Rust's left-to-right call arguments. The runtime borrows those values.
      wr = EmitCoercionIfNecessary(Type.Int, expression.Type, expression.Origin, wr);
      if (!GetExprConverter(wr, wStmts, out var builder, out var convert)) {
        throw new InvalidOperationException();
      }
      var args = new List<DAST._IExpression> {
        convert(Expression.CreateIntLiteral(expression.Origin, expression.IntegerBitwiseWidth.Value))
      };
      foreach (var operand in expression.IntegerBitwiseOperands) {
        args.Add(DAST.Expression.create_Convert(convert(operand),
          GenType(GetRuntimeType(operand.Type)), GenType(Type.Int)));
      }
      var path = global::Dafny.Sequence<global::Dafny.ISequence<global::Dafny.Rune>>.FromElements(
        global::Dafny.Sequence<global::Dafny.Rune>.UnicodeFromString("::dafny_runtime"),
        global::Dafny.Sequence<global::Dafny.Rune>.UnicodeFromString("integer_bitwise"));
      var signature = DAST.CallSignature.create_CallSignature(
        global::Dafny.Sequence<DAST._IFormal>.Empty, global::Dafny.Sequence<DAST._IFormal>.Empty);
      var call = DAST.CallName.create_CallName(global::Dafny.Sequence<global::Dafny.Rune>.UnicodeFromString(name),
        Std.Wrappers.Option<DAST.Type>.create_None(),
        Std.Wrappers.Option<DAST._IFormal>.create_None(), false, signature);
      builder.Builder.AddExpr((DAST.Expression)DAST.Expression.create_Call(
        DAST.Expression.create_ExternCompanion(path), call,
        global::Dafny.Sequence<DAST._IType>.Empty, global::Dafny.Sequence<DAST._IExpression>.FromArray(args.ToArray())));
    }

    protected override bool UseExternalConstructorArguments(Constructor constructor) =>
      // A constructor body runs after allocation and receives its arguments then.
      // Only a bodiless foreign constructor passes arguments to the allocation hook.
      (!HasRecursiveGroups || constructor.Body == null) && base.UseExternalConstructorArguments(constructor);

    protected override void EmitNewArray(Type elementType, IOrigin tok, List<Expression> dimensions,
      bool mustInitialize, string exampleElement, ConcreteSyntaxTree wr, ConcreteSyntaxTree wStmts) {
      if (HasRecursiveGroups && mustInitialize) {
        // With --enforce-determinism, verification permits an omitted array
        // initializer only when some dimension is zero. Cache every dimension
        // once, in source order, and also check this at runtime before exposing
        // an array of uninitialized elements (including with --no-verify).
        var cachedDimensions = new List<Expression>();
        Expression nonempty = Expression.CreateBoolLiteral(tok, true);
        foreach (var dimension in dimensions) {
          var local = new LocalVariable(tok, ProtectedFreshId("_arrayDimension"), Type.Int, false) {
            type = Type.Int
          };
          DeclareLocalVar(IdName(local), Type.Int, tok, dimension, false, wStmts);
          var identifier = new IdentifierExpr(tok, local);
          cachedDimensions.Add(identifier);
          nonempty = Expression.CreateAnd(nonempty, Expression.CreateNot(tok,
            Expression.CreateEq(identifier, Expression.CreateIntLiteral(tok, 0), Type.Int)));
        }
        var halt = EmitIf(out var guard, false, wStmts);
        EmitExpr(nonempty, false, guard, wStmts);
        EmitHalt(tok, null, halt);
        dimensions = cachedDimensions;
      }
      base.EmitNewArray(elementType, tok, dimensions, mustInitialize, exampleElement, wr, wStmts);
    }
  }

  public override void Compile(Program dafnyProgram, string dafnyProgramName, ConcreteSyntaxTree output) {
    foreach (var module in dafnyProgram.CompileModules.Where(module => module.CanCompile())) {
      var refinesGroup = false;
      for (var ancestor = module.Implements?.Target.Def; ancestor != null; ancestor = ancestor.Implements?.Target.Def) {
        refinesGroup |= ancestor.RecursiveGroup != null;
      }
      if (refinesGroup && module.TopLevelDecls.FirstOrDefault(declaration =>
            declaration is not ModuleDecl and not DefaultClassDecl && declaration.IsExtern(Options, out _, out _) &&
            !IsImportedRefinementClass(declaration, Options)) is { } externType) {
        Reporter.Error(MessageSource.Compiler, externType.Origin,
          $"Rust cannot compile extern type '{externType.Name}' in refining module '{module.FullDafnyName}' above a recursive module group: " +
          "supported extern classes have no compiled fields (including inherited fields), at most one extern argument, and either bodiless foreign members or implemented functions, methods or constructors without a foreign enclosing path");
        return;
      }
    }
    // Group members retain their attributes and public paths. The ordinary Rust
    // lowering therefore imports foreign declarations from the same root-level
    // extern namespace, including when their generated impls live in a container.
    // Unsupported extern forms are diagnosed by that lowering as usual.
    ((GroupCodeGenerator)codeGenerator).HasRecursiveGroups = dafnyProgram.CompileModules.Any(module => module.RecursiveGroup != null);
    ((RustCodeGenerator)DafnyCodeGenerator).Program = dafnyProgram;
    base.Compile(dafnyProgram, dafnyProgramName, output);
  }

  // Import the refining foreign type once, then let ordinary class lowering
  // generate its inherent members and ordinary trait implementations. Foreign
  // types still supply allocation; own and inherited fields require layout rules.
  internal static bool IsImportedRefinementClass(TopLevelDecl declaration, DafnyOptions options) {
    return declaration is ClassDecl cl &&
      cl.IsExtern(options, out var enclosing, out _) && enclosing == null &&
      cl.InheritedMembers.All(member => member.IsGhost || member is not Field) &&
      cl.Members.All(member => member.IsGhost ||
        (member.IsExtern(options, out _, out _) &&
         member is Function { Body: null } or MethodOrConstructor { Body: null }) ||
        (member is Function { Body: not null } or MethodOrConstructor { Body: not null } &&
         (!member.IsExtern(options, out var memberEnclosing, out _) || memberEnclosing == null)));
  }

  // Knowing that the result of the compilation will be placed in a dafnyProgramName.rs,
  // and that Dafny needs to import all the OtherFileNames into the same folder, but does not really care about their names,
  // this function returns a mapping from full paths of Rust files to a unique resulting name.
  //
  // For example, if OtherFiles == ["C:\Users\myextern.rs", "C:\Users\path\myextern.rs", "C:\Users\nonconflictextern.rs"] and dafnyProgramName == "myextern.dfy", it will create the dictionary
  // new Dictionary() {
  // { "C:\Users\myextern.rs", "myextern_1.rs" },
  // { "C:\Users\path\myextern.rs", "myextern_2.rs" },
  // { "C:\Users\myotherextern.rs", "nonconflictingextern.rs" }
  // }
  public override Dictionary<string, string> ImportFilesMapping(string dafnyProgramName) {
    Dictionary<string, string> importedFilesMapping = new();
    var baseName = Path.GetFileNameWithoutExtension(dafnyProgramName);
    importedFilesMapping["dummy"] = baseName + ".rs";
    var keyToRemove = "dummy to lower";
    importedFilesMapping[keyToRemove] = baseName.ToLower() + ".rs";
    var toRemove = new List<string> { "dummy", keyToRemove };
    if (OtherFileNames != null) {
      foreach (var otherFileFullPath in OtherFileNames) {
        var otherFileName = Path.GetFileName(otherFileFullPath);
        if (importedFilesMapping.ContainsValue(otherFileName) || importedFilesMapping.ContainsValue(otherFileName.ToLower())) {
          var newOtherFileBase = Path.GetFileNameWithoutExtension(otherFileName);
          var i = 0;
          do {
            i++;
            otherFileName = newOtherFileBase + $"_{i}.rs";
          } while (importedFilesMapping.ContainsValue(otherFileName) || importedFilesMapping.ContainsValue(otherFileName.ToLower()));
        }
        // Ensures we don't have overwrites in case-insensitive systems such as Windows
        importedFilesMapping[otherFileFullPath] = otherFileName;
        importedFilesMapping["to lower " + otherFileFullPath] = otherFileName.ToLower();
        toRemove.Add("to lower " + otherFileFullPath);
      }
    }

    foreach (var path in toRemove) {
      importedFilesMapping.Remove(path);
    }
    return importedFilesMapping;
  }


  public override async Task<bool> OnPostGenerate(string dafnyProgramName, string targetDirectory, IDafnyOutputWriter outputWriter) {
    foreach (var keyValue in ImportFilesMapping(dafnyProgramName)) {
      var fullRustExternName = keyValue.Key;
      var expectedRustName = keyValue.Value;
      var targetName = Path.Combine(targetDirectory, expectedRustName);
      if (fullRustExternName == targetName) {
        return true;
      }
      File.Copy(fullRustExternName, targetName, true);
    }

    if (Options.IncludeRuntime) {
      ImportRuntimeTo(Path.GetDirectoryName(targetDirectory));
    }
    return await base.OnPostGenerate(dafnyProgramName, targetDirectory, outputWriter);
  }

  private string ComputeExeName(string targetFilename) {
    var targetDirectory = Path.GetDirectoryName(Path.GetDirectoryName(targetFilename));
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
      return Path.Combine(targetDirectory, "target", "debug", Path.GetFileNameWithoutExtension(targetFilename) + ".exe");
    } else {
      return Path.Combine(targetDirectory, "target", "debug", Path.GetFileNameWithoutExtension(targetFilename));
    }
  }

  public override async Task<(bool Success, object CompilationResult)> CompileTargetProgram(string dafnyProgramName,
    string targetProgramText,
    string callToMain /*?*/, string targetFilename /*?*/, ReadOnlyCollection<string> otherFileNames,
    bool runAfterCompile, IDafnyOutputWriter outputWriter) {
    var targetDirectory = Path.GetDirectoryName(Path.GetDirectoryName(targetFilename));
    ImportRuntimeTo(targetDirectory);

    await WriteCargoFile(callToMain, targetFilename, targetDirectory);

    var args = new List<string> {
      "build",
      "--quiet"
    };

    if (callToMain == null) {
      args.Add("--lib");
    } else {
      args.Add("--bin");
      args.Add(Path.GetFileNameWithoutExtension(targetFilename));
    }

    var psi = PrepareProcessStartInfo("cargo", args);
    psi.WorkingDirectory = targetDirectory;
    var sw = outputWriter.StatusWriter();
    return (0 == await RunProcess(psi, sw, sw, "Error while compiling Rust files."), null);
  }

  private static async Task WriteCargoFile(string callToMain, string targetFilename, string targetDirectory) {
    await using (var cargoToml = new FileStream(Path.Combine(targetDirectory, "Cargo.toml"), FileMode.Create, FileAccess.Write)) {
      await using var cargoTomlWriter = new StreamWriter(cargoToml);
      await cargoTomlWriter.WriteLineAsync("[package]");
      var packageName = Path.GetFileNameWithoutExtension(targetFilename);
      // package name cannot start with a digit
      if (char.IsDigit(packageName[0])) {
        packageName = "_" + packageName;
      }
      await cargoTomlWriter.WriteLineAsync($"name = \"{packageName}\"");
      await cargoTomlWriter.WriteLineAsync("version = \"0.1.0\"");
      await cargoTomlWriter.WriteLineAsync("edition = \"2021\"");
      await cargoTomlWriter.WriteLineAsync();
      await cargoTomlWriter.WriteLineAsync("[dependencies]");
      await cargoTomlWriter.WriteLineAsync("dafny_runtime = { path = \"runtime\" }");
      await cargoTomlWriter.WriteLineAsync();

      if (callToMain == null) {
        await cargoTomlWriter.WriteLineAsync("[lib]");
        await cargoTomlWriter.WriteLineAsync("path = \"src/" + Path.GetFileName(targetFilename) + "\"");
        await cargoTomlWriter.WriteLineAsync();
      } else {
        await cargoTomlWriter.WriteLineAsync("[[bin]]");
        await cargoTomlWriter.WriteLineAsync($"name = \"{Path.GetFileNameWithoutExtension(targetFilename)}\"");
        await cargoTomlWriter.WriteLineAsync("path = \"src/" + Path.GetFileName(targetFilename) + "\"");
        await cargoTomlWriter.WriteLineAsync();
      }
    }
  }

  private static void ImportRuntimeTo(string targetDirectory) {
    var runtimeDirectory = Path.Combine(targetDirectory, "runtime");
    if (Directory.Exists(runtimeDirectory)) {
      Directory.Delete(runtimeDirectory, true);
    }
    Directory.CreateDirectory(runtimeDirectory);

    var assembly = System.Reflection.Assembly.Load("DafnyPipeline");
    assembly.GetManifestResourceNames().Where(f => f.StartsWith("DafnyPipeline.DafnyRuntimeRust")).ToList().ForEach(f => {
      var stream = assembly.GetManifestResourceStream(f);
      var dotToSlashPath = "";
      var parts = f.Replace("DafnyPipeline.DafnyRuntimeRust.", "").Split('.');
      for (var i = 0; i < parts.Length; i++) {
        dotToSlashPath += parts[i];

        if (i < parts.Length - 2) {
          dotToSlashPath += "/";
        } else if (i == parts.Length - 2) {
          // extension
          dotToSlashPath += ".";
        }
      }

      var containingDirectory = Path.Combine(runtimeDirectory, Path.GetDirectoryName(dotToSlashPath));
      if (!Directory.Exists(containingDirectory)) {
        Directory.CreateDirectory(containingDirectory);
      }

      using var outFile = new FileStream(Path.Combine(runtimeDirectory, dotToSlashPath), FileMode.Create, FileAccess.Write);
      stream.CopyTo(outFile);
    });
  }

  public override Encoding OutputWriterEncoding => Encoding.UTF8;

  public override async Task<bool> RunTargetProgram(string dafnyProgramName, string targetProgramText,
    string callToMain, /*?*/
    string targetFilename, ReadOnlyCollection<string> otherFileNames, object compilationResult,
    IDafnyOutputWriter outputWriter) {
    Contract.Requires(targetFilename != null || otherFileNames.Count == 0);
    var psi = PrepareProcessStartInfo(ComputeExeName(targetFilename), Options.MainArgs);
    await using var sw = outputWriter.StatusWriter();
    await using var ew = outputWriter.ErrorWriter();
    return 0 == await RunProcess(psi, sw, ew);
  }

  public RustBackend(DafnyOptions options) : base(options) {
  }
}
