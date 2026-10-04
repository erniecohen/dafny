package OSProcesses;

import java.io.*;
import java.math.BigInteger;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.concurrent.*;

public class OSProcess {
  private final dafny.DafnySequence<? extends dafny.CodePoint> executableName;
  private Process process;
  private Writer input;
  private PushbackReader output;
  private int timeoutMilliseconds, maximumResponseCharacters;
  private final StringBuilder stderr = new StringBuilder();
  private final ExecutorService io = Executors.newSingleThreadExecutor(r -> {
    Thread t = new Thread(r, "b3-solver-io"); t.setDaemon(true); return t;
  });
  private boolean disposed;

  private OSProcess(dafny.DafnySequence<? extends dafny.CodePoint> executable) { executableName = executable; }

  private static dafny.TypeDescriptor<dafny.DafnySequence<? extends dafny.CodePoint>> textType() {
    return dafny.DafnySequence._typeDescriptor(dafny.TypeDescriptor.UNICODE_CHAR);
  }
  private static <T> Std.Wrappers.Result<T, dafny.DafnySequence<? extends dafny.CodePoint>> success(dafny.TypeDescriptor<T> t, T value) {
    return Std.Wrappers.Result.create_Success(t, textType(), value);
  }
  private static <T> Std.Wrappers.Result<T, dafny.DafnySequence<? extends dafny.CodePoint>> failure(dafny.TypeDescriptor<T> t, String message) {
    return Std.Wrappers.Result.create_Failure(t, textType(), dafny.DafnySequence.asUnicodeString(message));
  }

  public static Std.Wrappers.Result<OSProcess, dafny.DafnySequence<? extends dafny.CodePoint>> Create(
      dafny.DafnySequence<? extends dafny.CodePoint> executable,
      dafny.DafnySequence<? extends dafny.DafnySequence<? extends dafny.CodePoint>> arguments,
      BigInteger timeoutMilliseconds, BigInteger maximumResponseCharacters) {
    OSProcess p = new OSProcess(executable);
    try {
      p.timeoutMilliseconds = timeoutMilliseconds.intValueExact();
      p.maximumResponseCharacters = maximumResponseCharacters.intValueExact();
      if (p.timeoutMilliseconds <= 0 || p.maximumResponseCharacters <= 0) { throw new IOException("Invalid solver budgets"); }
      var args = new ArrayList<String>(); args.add(executable.verbatimString());
      for (var argument : arguments) { args.add(argument.verbatimString()); }
      p.process = new ProcessBuilder(args).start();
      p.input = new OutputStreamWriter(p.process.getOutputStream(), StandardCharsets.UTF_8);
      p.output = new PushbackReader(new InputStreamReader(p.process.getInputStream(), StandardCharsets.UTF_8), 1);
      var drain = new Thread(() -> {
        try (var reader = new InputStreamReader(p.process.getErrorStream(), StandardCharsets.UTF_8)) {
          var chunk = new char[4096]; int count;
          while ((count = reader.read(chunk)) >= 0) {
            synchronized (p.stderr) {
              p.stderr.append(chunk, 0, count);
              if (p.stderr.length() > 16384) { p.stderr.delete(0, p.stderr.length() - 16384); }
            }
          }
        } catch (IOException ignored) { }
      }, "b3-solver-stderr");
      drain.setDaemon(true); drain.start();
      return success(dafny.TypeDescriptor.reference(OSProcess.class), p);
    } catch (Exception e) {
      try { p.close(); } catch (Exception ignored) { }
      return failure(dafny.TypeDescriptor.reference(OSProcess.class), "Could not start solver: " + e.getMessage());
    }
  }

  public dafny.DafnySequence<? extends dafny.CodePoint> ExecutableName() { return executableName; }

  private <T> T deadline(Callable<T> operation) throws Exception {
    if (disposed) { throw new IOException("Solver session is closed"); }
    return io.submit(operation).get(timeoutMilliseconds, TimeUnit.MILLISECONDS);
  }
  private String diagnostic(Exception e) {
    synchronized (stderr) { return e.toString() + (stderr.length() == 0 ? "" : "\nSolver stderr: " + stderr); }
  }
  public Std.Wrappers.Result<dafny.Tuple0, dafny.DafnySequence<? extends dafny.CodePoint>> Send(
      dafny.DafnySequence<? extends dafny.CodePoint> command) {
    try {
      deadline(() -> { input.write(command.verbatimString()); input.write('\n'); input.flush(); return null; });
      return success(dafny.Tuple0._typeDescriptor(), dafny.Tuple0.create());
    } catch (Exception e) {
      String message = diagnostic(e); try { close(); } catch (Exception cleanup) { message += "\nCleanup: " + cleanup; }
      return failure(dafny.Tuple0._typeDescriptor(), message);
    }
  }

  private Std.Wrappers.Result<Std.Wrappers.Option<dafny.DafnySequence<? extends dafny.CodePoint>>, dafny.DafnySequence<? extends dafny.CodePoint>> read(Callable<String> reader) {
    var optionType = Std.Wrappers.Option._typeDescriptor(textType());
    try {
      String text = deadline(reader);
      var option = text == null ? Std.Wrappers.Option.create_None(textType()) :
        Std.Wrappers.Option.create_Some(textType(), dafny.DafnySequence.asUnicodeString(text));
      return success(optionType, option);
    } catch (Exception e) {
      String message = diagnostic(e); try { close(); } catch (Exception cleanup) { message += "\nCleanup: " + cleanup; }
      return failure(optionType, message);
    }
  }

  public Std.Wrappers.Result<Std.Wrappers.Option<dafny.DafnySequence<? extends dafny.CodePoint>>, dafny.DafnySequence<? extends dafny.CodePoint>> ReadLine() {
    return read(() -> {
      var text = new StringBuilder(); int ch;
      while ((ch = output.read()) >= 0) {
        if (ch == '\n') { return text.toString().replaceAll("\\r$", ""); }
        if (text.length() >= maximumResponseCharacters) { throw new IOException("Solver response bound exceeded"); }
        text.append((char)ch);
      }
      return text.length() == 0 ? null : text.toString();
    });
  }

  public Std.Wrappers.Result<Std.Wrappers.Option<dafny.DafnySequence<? extends dafny.CodePoint>>, dafny.DafnySequence<? extends dafny.CodePoint>> ReadResponse() {
    return read(() -> {
      var text = new StringBuilder(); int next, skipped = 0;
      while (true) {
        next = output.read(); if (next < 0) { return null; }
        if (++skipped > maximumResponseCharacters) { throw new IOException("Solver response bound exceeded"); }
        if (Character.isWhitespace(next)) { continue; }
        if (next == ';') {
          do { next = output.read(); if (++skipped > maximumResponseCharacters) { throw new IOException("Solver response bound exceeded"); } }
          while (next >= 0 && next != '\n');
          if (next < 0) { return null; } continue;
        }
        break;
      }
      boolean expression = next == '(', string = false, symbol = false, comment = false; int depth = 0;
      while (true) {
        if (next < 0) {
          if (!expression && !string && !symbol) { return text.toString(); }
          throw new IOException("EOF within solver response");
        }
        char ch = (char)next;
        if (!expression && !string && !symbol && Character.isWhitespace(ch)) { return text.toString(); }
        if (text.length() >= maximumResponseCharacters) { throw new IOException("Solver response bound exceeded"); }
        text.append(ch);
        if (comment) { if (ch == '\n') { comment = false; } }
        else if (string) {
          if (ch == '"') {
            int peek = output.read();
            if (peek == '"') { text.append((char)peek); if (text.length() > maximumResponseCharacters) { throw new IOException("Solver response bound exceeded"); } }
            else { string = false; if (peek >= 0) { output.unread(peek); } }
          }
        } else if (symbol) {
          if (ch == '|') { symbol = false; }
          else if (ch == '\\') { throw new IOException("Invalid escape in quoted SMT symbol"); }
        } else if (ch == '"') { string = true; }
        else if (ch == '|') { symbol = true; }
        else if (ch == ';') { comment = true; }
        else if (ch == '(') { if (!expression) { throw new IOException("Malformed solver atom"); } depth++; }
        else if (ch == ')') {
          if (!expression || --depth < 0) { throw new IOException("Unbalanced solver response"); }
          if (depth == 0) { return text.toString(); }
        }
        next = output.read();
      }
    });
  }

  private void close() throws Exception {
    if (disposed) { return; } disposed = true;
    try {
      if (process != null) {
        process.descendants().forEach(ProcessHandle::destroyForcibly);
        process.destroyForcibly();
        if (!process.waitFor(5, TimeUnit.SECONDS)) { throw new IOException("Solver did not exit"); }
      }
    } finally {
      if (input != null) { input.close(); } if (output != null) { output.close(); } io.shutdownNow();
    }
  }
  public Std.Wrappers.Result<dafny.Tuple0, dafny.DafnySequence<? extends dafny.CodePoint>> Dispose() {
    try { close(); return success(dafny.Tuple0._typeDescriptor(), dafny.Tuple0.create()); }
    catch (Exception e) { return failure(dafny.Tuple0._typeDescriptor(), "Solver cleanup failed: " + e); }
  }
}
