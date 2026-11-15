#nullable enable
using System;
using System.IO;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text.RegularExpressions;
using DafnyCore;

namespace Microsoft.Dafny.Auditor;

/// <summary>
/// A rewriter pass that produces an AuditReport and writes it to either
/// standard output or a file, in one of several formats. If neither a file
/// or a format is provided, it reports all assumptions as warnings.
/// </summary>
public class Auditor : IRewriter {
  public enum ReportFormat { HTML, MarkdownTable, MarkdownIETF, Text }

  public static readonly Option<string> ReportFileOption = new("--report-file",
    "Specify a path to store the audit report file. Without this, the report will take the form of standard warnings.");
  public static readonly Option<bool> CompareReportOption = new("--compare-report",
    "Compare the report that would have been generated with the existing file given by --report-file, and fail if they differ.");

  public static string SupportedReportFormats = "plain text in the format of warnings ('txt', 'text'); standalone HTML ('html'); a Markdown table ('md', 'markdown', 'md-table', 'markdown-table'); or an IETF-language document in Markdown format ('md-ietf', 'markdown-ietf')";

  public static Option<ReportFormat?> ReportFormatOption =
    new("--report-format",
      arg => {
        if (arg.Tokens.Any()) {
          switch (arg.Tokens[0].Value) {
            case "md":
            case "md-table":
            case "markdown":
            case "markdown-table":
              return ReportFormat.MarkdownTable;
            case "md-ietf":
            case "markdown-ietf":
              return ReportFormat.MarkdownIETF;
            case "html":
              return ReportFormat.HTML;
            case "text":
            case "txt":
              return ReportFormat.Text;
            default:
              arg.ErrorMessage =
                $"Unsupported report format. Supported formats are: {SupportedReportFormats}";
              return null;
          }
        }

        return null;
      },
      false,
      $"Specify the file format to use for the audit report. Supported options include: {SupportedReportFormats}. " +
               "With no option given, the format will be inferred from the filename extension. " +
               "With no filename or format given, the report will consist of standard Dafny warnings.") { ArgumentHelpName = "format" };

  private readonly string? reportFileName;
  private readonly ReportFormat? reportFormat;
  private readonly bool compareReport;

  static Auditor() {
    ReportFormatOption.FromAmong("html",
                                 "md", "markdown", "md-table", "markdown-table",
                                 "md-ietf", "markdown-ietf",
                                 "txt");

    OptionRegistry.RegisterOption(ReportFileOption, OptionScope.Cli);
    OptionRegistry.RegisterOption(ReportFormatOption, OptionScope.Cli);
    OptionRegistry.RegisterOption(CompareReportOption, OptionScope.Cli);
  }

  /// <summary>
  /// Construct an auditor to write to or compare to the given file in the
  /// given format.
  /// </summary>
  /// <param name="reporter">
  /// the reporter to use to emit errors and warnings
  /// </param>
  public Auditor(ErrorReporter reporter) : base(reporter) {
    reportFileName = reporter.Options.Get(ReportFileOption);
    compareReport = reporter.Options.Get(CompareReportOption);
    var format = reporter.Options.Get(ReportFormatOption);
    if (format is null) {
      if (reportFileName is null) {
        return;
      }

      if (reportFileName.EndsWith(".html")) {
        reportFormat = ReportFormat.HTML;
      } else if (reportFileName.EndsWith(".md")) {
        reportFormat = ReportFormat.MarkdownTable;
      } else if (reportFileName.EndsWith(".txt")) {
        reportFormat = ReportFormat.Text;
      } else {
        Reporter.Error(MessageSource.Verifier, Token.NoToken,
          $"Unsupported extension on report filename: {reportFileName}, using plain text. " +
               "Supported extensions are: .html, .md, .txt");
      }
    } else {
      reportFormat = format;
    }
  }

  private static string GlobalNote(string kind) =>
    kind == ModularTerminationTrustList.GhostFunctionKind
      ? ", a global premise: its postconditions hold wherever the function is visible, not only at a call"
      : "";

  /// <summary>
  /// Under --modular-termination with a trust list, adds to each {:axiom} finding whether the trust list
  /// admits it as a premise of a library record, and if not, why.  Audit never hides a premise.
  /// </summary>
  private Func<Assumption, Assumption>? TrustAnnotation(Program program) {
    var trustList = ModularTerminationTrustList.Selected(program.Options);
    if (trustList == null) {
      return null;
    }
    IReadOnlyDictionary<MemberDecl, ModularTerminationPremise> premises = new Dictionary<MemberDecl, ModularTerminationPremise>();
    string? unavailable = null;
    if (program.AfterParsingClone == null || program.Options.Get(CommonOptionBag.UserDefinedOperators)) {
      unavailable = "its retained program is not available";
    } else {
      var programText = new DooFile(program.AfterParsingClone).ProgramText;
      var (evaluation, error) = ModularTerminationTrustList.Evaluation.Compute(program, programText,
        new Uri("transcript:///modular-termination-audit.dfy")).GetAwaiter().GetResult();
      if (evaluation == null) {
        unavailable = $"its retained program does not parse: {error}";
      } else {
        premises = evaluation.Premises;
      }
    }
    var option = $"--modular-termination-trust:{trustList}";
    return assumption => {
      if (assumption.desc != AssumptionDescription.HasAxiomAttribute) {
        return assumption;
      }
      string note;
      if (assumption.decl is MemberDecl member && premises.TryGetValue(member, out var premise)) {
        note = ModularTerminationTrustList.Evaluation.Mismatch(program.Options, premise) is { } mismatch
          ? $"Under {option}, {mismatch}, so it refuses a library record."
          : $"Under {option}, the trust list admits it as the trusted premise {premise.Id} ({premise.Kind}{GlobalNote(premise.Kind)}).";
      } else if (unavailable != null && ModularTerminationTrustList.CandidateId(assumption.decl) is { } id &&
                 ModularTerminationTrustList.Find(trustList, id) != null) {
        note = $"Under {option}, its digests cannot be computed: {unavailable}.";
      } else {
        note = $"Under {option}, it is not on the trust list, so it refuses a library record.";
      }
      return assumption with { desc = assumption.desc with { Issue = assumption.desc.Issue + " " + note } };
    };
  }

  private static Regex TableRegex = new Regex(@"\{\{TABLE\}\}\r?\n");

  private string GenerateHtmlReport(AuditReport report) {
    var table = report.RenderHTMLTable();
    var assembly = System.Reflection.Assembly.GetCallingAssembly();
    var templateStream = assembly.GetManifestResourceStream("audit_template.html");
    if (templateStream is null) {
      Reporter.Warning(MessageSource.Verifier, "", Token.NoToken, "Embedded HTML template not found. Returning raw HTML.");
      return table;
    }
    var templateText = new StreamReader(templateStream).ReadToEnd();
    return TableRegex.Replace(templateText, table);
  }

  internal override void PostResolve(Program program) {
    var report = AuditReport.BuildReport(program, TrustAnnotation(program));

    if (reportFileName is null && reportFormat is null) {
      foreach (var (_, assumptions) in report.AllAssumptions()) {
        foreach (var assumption in assumptions) {
          Reporter.Warning(MessageSource.Verifier, "", assumption.Tok, assumption.Warning());
        }
      }
    } else {
      var text = reportFormat switch {
        ReportFormat.HTML => GenerateHtmlReport(report),
        ReportFormat.MarkdownTable => report.RenderMarkdownTable(),
        ReportFormat.MarkdownIETF => report.RenderMarkdownIETF(),
        ReportFormat.Text => report.RenderText(),
        _ => $"Internal error: unknown format {reportFormat}"
      };
      if (reportFileName is null) {
        _ = Options.OutputWriter.Status(text);
      } else {
        if (compareReport) {
          try {
            var matches = File.ReadAllText(reportFileName).Equals(text);
            if (!matches) {
              Reporter.Error(MessageSource.Verifier, Token.NoToken,
                $"Given report file ({reportFileName}) does not match text generated (and saved in {reportFileName}.expect).");
              File.WriteAllText(reportFileName + ".expect", text);
            }
          } catch (IOException ioe) {
            Reporter.Error(MessageSource.Verifier, Token.NoToken, $"I/O exception trying to read {reportFileName} ({ioe.Message}).");
          }
        } else {
          File.WriteAllText(reportFileName, text);
        }
      }
    }

    var findingCount = report.AllAssumptions().SelectMany(d => d.Value).Count();
    // Under --modular-termination, the trusted premises recorded by the admitted libraries.
    foreach (var (library, trustList, premise) in program.Compilation.ModularTerminationPremises) {
      Reporter.Warning(MessageSource.Verifier, "", Token.Cli,
        $"{library}: the library relies on the trusted premise {premise.Id} ({premise.Kind}{GlobalNote(premise.Kind)}) " +
        $"of the {trustList} trust list, admitted under --modular-termination-trust:{trustList}");
      findingCount++;
    }
    _ = Options.OutputWriter.Status($"Dafny auditor completed with {findingCount} findings");
  }
}
