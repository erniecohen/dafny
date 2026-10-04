#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OSProcesses {
  public sealed class OSProcess {
    private readonly Dafny.ISequence<Dafny.Rune> executableName;
    private Process process = null!;
    private StreamWriter input = null!;
    private StreamReader output = null!;
    private int timeoutMilliseconds;
    private int maximumResponseCharacters;
    private readonly StringBuilder stderr = new StringBuilder();
    private readonly object stderrLock = new object();
    private bool disposed;

    private OSProcess(Dafny.ISequence<Dafny.Rune> executable) {
      executableName = executable;
    }

    private static Dafny.ISequence<Dafny.Rune> Text(string value) =>
      Dafny.Sequence<Dafny.Rune>.UnicodeFromString(value);

    public static Std.Wrappers._IResult<OSProcess, Dafny.ISequence<Dafny.Rune>> Create(
      Dafny.ISequence<Dafny.Rune> executable,
      Dafny.ISequence<Dafny.ISequence<Dafny.Rune>> arguments,
      BigInteger timeoutMilliseconds, BigInteger maximumResponseCharacters) {
      var wrapper = new OSProcess(executable);
      try {
        if (timeoutMilliseconds <= 0 || timeoutMilliseconds > int.MaxValue ||
            maximumResponseCharacters <= 0 || maximumResponseCharacters > int.MaxValue) {
          throw new ArgumentOutOfRangeException("Invalid process deadline or response bound");
        }
        wrapper.timeoutMilliseconds = (int)timeoutMilliseconds;
        wrapper.maximumResponseCharacters = (int)maximumResponseCharacters;
        var startInfo = new ProcessStartInfo(executable.ToVerbatimString(false)) {
          RedirectStandardInput = true, RedirectStandardOutput = true,
          RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
        };
        foreach (var argument in arguments.CloneAsArray()) {
          startInfo.ArgumentList.Add(argument.ToVerbatimString(false));
        }
        wrapper.process = new Process { StartInfo = startInfo };
        // Continuously drain stderr, retaining a bounded diagnostic suffix.
        if (!wrapper.process.Start()) { throw new IOException("Process did not start"); }
        wrapper.input = wrapper.process.StandardInput;
        wrapper.output = wrapper.process.StandardOutput;
        _ = Task.Run(() => {
          var chunk = new char[4096];
          try {
            while (true) {
              int count = wrapper.process.StandardError.Read(chunk, 0, chunk.Length);
              if (count == 0) { break; }
              lock (wrapper.stderrLock) {
                wrapper.stderr.Append(chunk, 0, count);
                const int bound = 16384;
                if (wrapper.stderr.Length > bound) {
                  wrapper.stderr.Remove(0, wrapper.stderr.Length - bound);
                }
              }
            }
          } catch (ObjectDisposedException) { }
            catch (IOException) { }
            catch (InvalidOperationException) { }
        });
        return Std.Wrappers.Result<OSProcess, Dafny.ISequence<Dafny.Rune>>.create_Success(wrapper);
      } catch (Exception ex) {
        try { wrapper.Close(); } catch { }
        return Std.Wrappers.Result<OSProcess, Dafny.ISequence<Dafny.Rune>>.create_Failure(
          Text("Could not start solver: " + ex.Message));
      }
    }

    public Dafny.ISequence<Dafny.Rune> ExecutableName() => executableName;

    private string Diagnostic(Exception ex) {
      string diagnostic;
      lock (stderrLock) { diagnostic = stderr.ToString(); }
      return ex.Message + (diagnostic.Length == 0 ? "" : "\nSolver stderr: " + diagnostic);
    }

    public Std.Wrappers._IResult<_System._ITuple0, Dafny.ISequence<Dafny.Rune>> Send(
      Dafny.ISequence<Dafny.Rune> command) {
      try {
        if (disposed) { throw new IOException("Solver session is closed"); }
        var write = Task.Run(() => { input.WriteLine(command.ToVerbatimString(false)); input.Flush(); });
        if (!write.Wait(timeoutMilliseconds)) { throw new TimeoutException("Solver write deadline exceeded"); }
        write.GetAwaiter().GetResult();
        return Std.Wrappers.Result<_System._ITuple0, Dafny.ISequence<Dafny.Rune>>.create_Success(_System.Tuple0.create());
      } catch (Exception ex) {
        var message = Diagnostic(ex);
        try { Close(); } catch (Exception cleanup) { message += "\nCleanup: " + cleanup.Message; }
        return Std.Wrappers.Result<_System._ITuple0, Dafny.ISequence<Dafny.Rune>>.create_Failure(Text(message));
      }
    }

    private Std.Wrappers._IResult<Std.Wrappers._IOption<Dafny.ISequence<Dafny.Rune>>, Dafny.ISequence<Dafny.Rune>> Read(Func<string?> reader) {
      try {
        if (disposed) { throw new IOException("Solver session is closed"); }
        var read = Task.Run(reader);
        if (!read.Wait(timeoutMilliseconds)) { throw new TimeoutException("Solver read deadline exceeded"); }
        var value = read.GetAwaiter().GetResult();
        var result = value == null
          ? Std.Wrappers.Option<Dafny.ISequence<Dafny.Rune>>.create_None()
          : Std.Wrappers.Option<Dafny.ISequence<Dafny.Rune>>.create_Some(Text(value));
        return Std.Wrappers.Result<Std.Wrappers._IOption<Dafny.ISequence<Dafny.Rune>>, Dafny.ISequence<Dafny.Rune>>.create_Success(result);
      } catch (Exception ex) {
        var message = Diagnostic(ex);
        try { Close(); } catch (Exception cleanup) { message += "\nCleanup: " + cleanup.Message; }
        return Std.Wrappers.Result<Std.Wrappers._IOption<Dafny.ISequence<Dafny.Rune>>, Dafny.ISequence<Dafny.Rune>>.create_Failure(Text(message));
      }
    }

    public Std.Wrappers._IResult<Std.Wrappers._IOption<Dafny.ISequence<Dafny.Rune>>, Dafny.ISequence<Dafny.Rune>> ReadLine() => Read(() => {
      var buffer = new StringBuilder();
      while (true) {
        int next = output.Read();
        if (next < 0) { return buffer.Length == 0 ? null : buffer.ToString(); }
        if (next == '\n') { return buffer.ToString().TrimEnd('\r'); }
        if (buffer.Length >= maximumResponseCharacters) { throw new IOException("Solver response bound exceeded"); }
        buffer.Append((char)next);
      }
    });

    // Framing distinguishes strings (with doubled quotes), quoted symbols, comments,
    // and S-expressions. EOF within a frame is never a successful partial response.
    public Std.Wrappers._IResult<Std.Wrappers._IOption<Dafny.ISequence<Dafny.Rune>>, Dafny.ISequence<Dafny.Rune>> ReadResponse() =>
      Read(() => ReadSmtResponse(output, maximumResponseCharacters));

    public static string? ReadSmtResponse(TextReader reader, int maximumCharacters) {
      var buffer = new StringBuilder();
      int next;
      int skipped = 0;
      while (true) {
        next = reader.Read();
        if (next < 0) { return null; }
        if (++skipped > maximumCharacters) { throw new IOException("Solver response bound exceeded"); }
        if (char.IsWhiteSpace((char)next)) { continue; }
        if (next == ';') {
          do {
            next = reader.Read();
            if (++skipped > maximumCharacters) { throw new IOException("Solver response bound exceeded"); }
          } while (next >= 0 && next != '\n');
          if (next < 0) { return null; }
          continue;
        }
        break;
      }
      bool expression = next == '(';
      bool inString = false, inSymbol = false, inComment = false;
      int depth = 0;
      while (true) {
        if (next < 0) {
          if (!expression && !inString && !inSymbol) { return buffer.ToString(); }
          throw new IOException("EOF within solver response");
        }
        char ch = (char)next;
        if (!expression && !inString && !inSymbol && char.IsWhiteSpace(ch)) { return buffer.ToString(); }
        if (buffer.Length >= maximumCharacters) { throw new IOException("Solver response bound exceeded"); }
        buffer.Append(ch);
        if (inComment) {
          if (ch == '\n') { inComment = false; }
        } else if (inString) {
          if (ch == '"') {
            if (reader.Peek() == '"') {
              if (buffer.Length >= maximumCharacters) { throw new IOException("Solver response bound exceeded"); }
              buffer.Append((char)reader.Read());
            } else { inString = false; }
          }
        } else if (inSymbol) {
          if (ch == '|') { inSymbol = false; }
          else if (ch == '\\') { throw new IOException("Invalid escape in quoted SMT symbol"); }
        } else if (ch == '"') { inString = true; }
        else if (ch == '|') { inSymbol = true; }
        else if (ch == ';') { inComment = true; }
        else if (ch == '(') {
          if (!expression) { throw new IOException("Malformed solver atom"); }
          depth++;
        } else if (ch == ')') {
          if (!expression || --depth < 0) { throw new IOException("Unbalanced solver response"); }
          if (depth == 0) { return buffer.ToString(); }
        }
        next = reader.Read();
      }
    }

    private void Close() {
      if (disposed) { return; }
      disposed = true;
      try {
        if (process != null && !process.HasExited) { process.Kill(entireProcessTree: true); }
        if (process != null && !process.WaitForExit(5000)) { throw new IOException("Solver did not exit after termination"); }
      } finally {
        input?.Dispose(); output?.Dispose(); process?.Dispose();
      }
    }

    public Std.Wrappers._IResult<_System._ITuple0, Dafny.ISequence<Dafny.Rune>> Dispose() {
      try {
        Close();
        return Std.Wrappers.Result<_System._ITuple0, Dafny.ISequence<Dafny.Rune>>.create_Success(_System.Tuple0.create());
      } catch (Exception ex) {
        return Std.Wrappers.Result<_System._ITuple0, Dafny.ISequence<Dafny.Rune>>.create_Failure(Text("Solver cleanup failed: " + ex.Message));
      }
    }
  }
}
