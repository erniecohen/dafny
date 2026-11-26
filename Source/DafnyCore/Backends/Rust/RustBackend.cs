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
          "only wholly inherited classes with at most one extern argument, foreign methods or constructors, and no fields or trait parents are supported");
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

  // Fully imported classes need only an import from the refining foreign module.
  // Partial classes, fields and trait implementations still need remapping of
  // generated implementations and retain the explicit rejection above.
  internal static bool IsImportedRefinementClass(TopLevelDecl declaration, DafnyOptions options) {
    var module = declaration.EnclosingModuleDefinition;
    return declaration is ClassDecl cl && cl.Origin.IsInherited(module) && cl.Traits.Count == 0 &&
      cl.IsExtern(options, out var enclosing, out _) && enclosing == null &&
      cl.Members.All(member => member.IsGhost ||
        (member.Origin.IsInherited(module) && member.IsExtern(options, out _, out _) &&
         member is Function { Body: null } or MethodOrConstructor { Body: null }));
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
