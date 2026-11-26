using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Dafny.Compilers;

public class JavaScriptBackend : ExecutableBackend {
  public override IReadOnlySet<string> SupportedExtensions => new HashSet<string> { ".js" };

  public override string TargetName => "JavaScript";
  public override bool IsStable => true;
  public override string TargetExtension => "js";

  public override bool SupportsInMemoryCompilation => true;
  public override bool TextualTargetIsExecutable => true;

  public override IReadOnlySet<string> SupportedNativeTypes =>
    new HashSet<string>(new List<string> { "number" });

  protected override SinglePassCodeGenerator CreateCodeGenerator() {
    return new JavaScriptCodeGenerator(Options, Reporter);
  }

  public override void Compile(Program dafnyProgram, string dafnyProgramName, ConcreteSyntaxTree output) {
    foreach (var module in dafnyProgram.CompileModules.Where(module => module.CanCompile())) {
      var refinesGroup = false;
      for (var ancestor = module.Implements?.Target.Def; ancestor != null; ancestor = ancestor.Implements?.Target.Def) {
        refinesGroup |= ancestor.RecursiveGroup != null;
      }
      if (refinesGroup && module.TopLevelDecls.FirstOrDefault(declaration =>
            declaration is not ModuleDecl and not DefaultClassDecl && declaration.IsExtern(Options, out _, out _) &&
            (declaration is not ClassDecl type || type.Members.Any(member =>
              !member.IsGhost && !codeGenerator.IsExternallyImported(member)))) is { } externType) {
        Reporter.Error(MessageSource.Compiler, externType.Origin,
          "JavaScript refinements of recursive module groups require fully imported extern classes; partial classes and other extern type kinds are not yet supported");
        return;
      }
    }
    foreach (var module in dafnyProgram.Modules().Where(module => module.CanCompile() && JavaScriptCodeGenerator.IsRecursiveGroupContext(module))) {
      if (module.IsExtern(Options, out var moduleQualifier, out _) && moduleQualifier != null) {
        Reporter.Error(MessageSource.Compiler, module.Origin,
          "JavaScript recursive groups require preloaded extern module objects; require-based extern modules are not yet supported");
        return;
      }
      foreach (var type in module.TopLevelDecls.OfType<TopLevelDeclWithMembers>()) {
        if (type.IsExtern(Options, out var qualifier, out _) &&
            (qualifier != null || !module.IsExtern(Options, out _, out _))) {
          Reporter.Error(MessageSource.Compiler, type.Origin,
            "JavaScript recursive groups require extern types in a preloaded extern module, without a two-argument type qualifier");
          return;
        }
        if (type.Members.OfType<Constructor>().FirstOrDefault(constructor =>
              !constructor.IsGhost && constructor.Body == null && constructor.IsExtern(Options, out _, out _)) is { } externalConstructor &&
            (!type.IsExtern(Options, out _, out _) || type.Members.Any(member =>
              !member.IsGhost && !codeGenerator.IsExternallyImported(member)))) {
          Reporter.Error(MessageSource.Compiler, externalConstructor.Origin,
            "JavaScript recursive groups require extern constructors on fully imported classes; partial classes must use a Dafny constructor body");
          return;
        }
        if (type.Members.FirstOrDefault(member => !member.IsGhost &&
              member is not Function and not Method { IsStatic: true } and not MethodOrConstructor { Body: not null } &&
              member.IsExtern(Options, out var memberQualifier, out _) && memberQualifier != null) is { } qualifiedMember) {
          Reporter.Error(MessageSource.Compiler, qualifiedMember.Origin,
            "JavaScript recursive groups require qualified extern members to be functions or static methods");
          return;
        }
        if (type.Members.FirstOrDefault(member => !member.IsGhost &&
              member is not Function { Body: not null } and not MethodOrConstructor { Body: not null } &&
              member.IsExtern(Options, out var memberQualifier, out _) && memberQualifier == null &&
              !(type is DefaultClassDecl ? module.IsExtern(Options, out _, out _) : type.IsExtern(Options, out _, out _))) is { } unboundMember) {
          Reporter.Error(MessageSource.Compiler, unboundMember.Origin,
            "JavaScript recursive groups require unqualified extern members in an extern type or module");
          return;
        }
      }
    }
    base.Compile(dafnyProgram, dafnyProgramName, output);
  }

  public override async Task<(bool Success, object CompilationResult)> CompileTargetProgram(string dafnyProgramName,
    string targetProgramText,
    string callToMain /*?*/, string targetFilename /*?*/, ReadOnlyCollection<string> otherFileNames,
    bool runAfterCompile, IDafnyOutputWriter outputWriter) {
    if (runAfterCompile) {
      Contract.Assert(callToMain != null);  // this is part of the contract of CompileTargetProgram
                                            // Since the program is to be run soon, nothing further is done here. Any compilation errors (that is, any errors
                                            // in the emitted program--this should never happen if the compiler itself is correct) will be reported as 'node'
                                            // will run the program.
      return (true, null);
    } else {
      // compile now
      return (await SendToNewNodeProcess(dafnyProgramName, targetProgramText, null, targetFilename, otherFileNames, outputWriter), null);
    }
  }

  public override Task<bool> RunTargetProgram(string dafnyProgramName, string targetProgramText,
    string callToMain, /*?*/
    string targetFilename, ReadOnlyCollection<string> otherFileNames,
    object compilationResult, IDafnyOutputWriter outputWriter) {

    return SendToNewNodeProcess(dafnyProgramName, targetProgramText, callToMain, targetFilename, otherFileNames, outputWriter);
  }

  async Task<bool> SendToNewNodeProcess(string dafnyProgramName, string targetProgramText, string/*?*/ callToMain,
    string targetFilename, ReadOnlyCollection<string> otherFileNames,
    IDafnyOutputWriter outputWriter) {
    Contract.Requires(targetFilename != null || otherFileNames.Count == 0);

    var psi = new ProcessStartInfo("node", "") {
      RedirectStandardInput = true,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      StandardInputEncoding = Encoding.UTF8,
    };

    try {
      Process nodeProcess = Process.Start(psi);
      foreach (var filename in otherFileNames) {
        WriteFromFile(filename, nodeProcess.StandardInput);
      }
      await nodeProcess.StandardInput.WriteAsync(targetProgramText);
      if (callToMain != null && Options.RunAfterCompile) {
        await nodeProcess.StandardInput.WriteLineAsync("require('process').stdout.setEncoding(\"utf-8\");");
        await nodeProcess.StandardInput.WriteLineAsync("require('process').argv = [\"node\",\"stdin\", " + string.Join(",", Options.MainArgs.Select(((JavaScriptCodeGenerator)codeGenerator).ToStringLiteral)) + "];");
        await nodeProcess.StandardInput.WriteAsync(callToMain);
      }
      await nodeProcess.StandardInput.FlushAsync();
      nodeProcess.StandardInput.Close();
      // Fixes a problem of Node on Windows, where Node does not prints to the parent console its standard outputs.
      await PassthroughBuffer(nodeProcess.StandardError, Options.ErrorWriter);
      await using var tempOutputWriter = Options.OutputWriter.StatusWriter();
      await PassthroughBuffer(nodeProcess.StandardOutput, tempOutputWriter);
      await nodeProcess.WaitForExitAsync();
#pragma warning disable VSTHRD00
      return nodeProcess.ExitCode == 0;
    } catch (System.ComponentModel.Win32Exception e) {
      await outputWriter.Status($"Error: Unable to start node.js ({psi.FileName}): {e.Message}");
      return false;
    }
  }

  public JavaScriptBackend(DafnyOptions options) : base(options) {
  }
}
