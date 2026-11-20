using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DafnyB3;

// This assembly deliberately has no dependency on Dafny, Boogie or a B3 implementation.
public enum Outcome { ToolError, Proved, Failed, Unknown, Unsupported, Cancelled, Timeout, ResourceLimit }

public sealed record Request(int Version, string RequestId, string UnitId, string[] Obligations);
public sealed record Response(int Version, string RequestId, string UnitId, int Sequence,
  string Kind, string? ObligationId, Outcome Outcome, int Completed);

public sealed class ProtocolException(string message) : IOException(message);

public static class Protocol {
  public const int Version = 1;
  public const int MaxFrameBytes = 64 * 1024;
  public const int MaxObligations = 256;
  private static readonly UTF8Encoding Utf8 = new(false, true);
  private static readonly JsonSerializerOptions Json = new() {
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectRequiredConstructorParameters = true,
    Converters = { new OutcomeConverter() }
  };

  private sealed class OutcomeConverter : JsonConverter<Outcome> {
    public override Outcome Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) {
      if (reader.TokenType != JsonTokenType.String ||
          !Enum.TryParse<Outcome>(reader.GetString(), ignoreCase: false, out var outcome) ||
          !Enum.IsDefined(outcome) || reader.GetString() != outcome.ToString()) {
        throw new JsonException("Invalid protocol outcome");
      }
      return outcome;
    }

    public override void Write(Utf8JsonWriter writer, Outcome value, JsonSerializerOptions options) {
      if (!Enum.IsDefined(value)) { throw new JsonException("Invalid protocol outcome"); }
      writer.WriteStringValue(value.ToString());
    }
  }

  public static void Validate(Request request) {
    if (request.Version != Version || !Identity(request.RequestId) || !Identity(request.UnitId) ||
        request.Obligations == null || request.Obligations.Length is 0 or > MaxObligations ||
        request.Obligations.Any(id => !Identity(id)) ||
        request.Obligations.Distinct(StringComparer.Ordinal).Count() != request.Obligations.Length) {
      throw new ProtocolException("Invalid verification request or empty manifest");
    }
  }

  private static bool Identity(string? text) => text is { Length: > 0 and <= 128 } &&
    text.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.' or ':');

  public static async Task WriteAsync<T>(Stream stream, T value, CancellationToken cancellationToken) {
    var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
    if (bytes.Length > MaxFrameBytes) {
      throw new ProtocolException("Protocol frame exceeds limit");
    }
    await stream.WriteAsync(bytes, cancellationToken);
    await stream.WriteAsync(new byte[] { 10 }, cancellationToken);
    await stream.FlushAsync(cancellationToken);
  }

  // Read through LF, rejecting partial frames, invalid UTF-8, duplicate properties and extensions.
  // The caller owns a deadline; no read can make unbounded progress without consuming its budget.
  public static async Task<T?> ReadAsync<T>(Stream stream, CancellationToken cancellationToken) where T : class {
    using var frame = new MemoryStream();
    var one = new byte[1];
    while (true) {
      var read = await stream.ReadAsync(one, cancellationToken);
      if (read == 0) {
        if (frame.Length != 0) { throw new ProtocolException("Truncated protocol frame"); }
        return null;
      }
      if (one[0] == 10) { break; }
      if (frame.Length == MaxFrameBytes) { throw new ProtocolException("Protocol frame exceeds limit"); }
      frame.WriteByte(one[0]);
    }
    try {
      var json = Utf8.GetString(frame.ToArray());
      using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
      if (doc.RootElement.ValueKind != JsonValueKind.Object) {
        throw new ProtocolException("Expected a protocol object");
      }
      var names = new HashSet<string>(StringComparer.Ordinal);
      foreach (var property in doc.RootElement.EnumerateObject()) {
        if (!names.Add(property.Name)) { throw new ProtocolException("Duplicate protocol property"); }
      }
      return JsonSerializer.Deserialize<T>(json, Json) ?? throw new ProtocolException("Null protocol frame");
    } catch (Exception error) when (error is JsonException or DecoderFallbackException) {
      throw new ProtocolException("Malformed protocol frame");
    }
  }
}

/// <summary>A single-use ledger. No partial result or completion frame alone establishes success.</summary>
public sealed class ResponseLedger {
  private readonly Request request;
  private readonly HashSet<string> remaining;
  private int sequence;
  private bool complete;
  private bool allProved = true;
  private bool poisoned;

  public ResponseLedger(Request request) {
    Protocol.Validate(request);
    // Snapshot mutable caller data before starting I/O.
    this.request = request with { Obligations = request.Obligations.ToArray() };
    remaining = new HashSet<string>(this.request.Obligations, StringComparer.Ordinal);
  }

  public void Accept(Response response) {
    try {
      if (poisoned || complete || response.Version != Protocol.Version ||
          response.RequestId != request.RequestId || response.UnitId != request.UnitId ||
          response.Sequence != sequence || !Enum.IsDefined(response.Outcome)) {
        throw new ProtocolException("Uncorrelated, out-of-order or trailing response");
      }
      sequence++;
      if (response.Kind == "result") {
        if (response.ObligationId == null || !remaining.Remove(response.ObligationId) || response.Completed != 0) {
          throw new ProtocolException("Foreign, duplicate or malformed result");
        }
        allProved &= response.Outcome == Outcome.Proved;
      } else if (response.Kind == "complete") {
        if (response.ObligationId != null || remaining.Count != 0 ||
            response.Completed != request.Obligations.Length ||
            (response.Outcome == Outcome.Proved && !allProved)) {
          throw new ProtocolException("Incomplete or inconsistent traversal");
        }
        allProved &= response.Outcome == Outcome.Proved;
        complete = true;
      } else {
        throw new ProtocolException("Unknown response kind");
      }
    } catch {
      poisoned = true;
      throw;
    }
  }

  // The client must additionally establish clean stdout EOF, exit zero and successful I/O cleanup.
  public void RequireProved() {
    if (poisoned || !complete || remaining.Count != 0 || !allProved) {
      throw new ProtocolException("Worker did not prove the complete manifest");
    }
  }
}
