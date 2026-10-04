"""Private C# Work-method instrumentation draft; never changes the Dafny proof source."""
import hashlib
from pathlib import Path
import re

_MARK = "__Issue113WorkProfileCalls"
_SIGNATURE = re.compile(
    r"(?m)^(?P<indent>[ \t]*)public\s+static\s+"
    r"(?:global::)?(?:System\.Numerics\.)?BigInteger\s+Work\(\s*"
    r"(?:global::)?(?:System\.Numerics\.)?BigInteger\s+[A-Za-z_]\w*\s*\)\s*\{"
)
_PROFILE = re.compile(r"^profile:([01]):([0-9]+):([0-9]+):([0-9]+):([0-9]+):([0-9]+)$")

def _sha(raw):
    return hashlib.sha256(raw).hexdigest()

def _closing_brace(text, opening):
    """Match emitted C# braces, skipping comments and ordinary/verbatim strings.

    Reject interpolation and raw strings rather than guessing their syntax.
    """
    depth = 1
    index = opening + 1
    while index < len(text):
        if text.startswith("//", index):
            ending = text.find("\n", index + 2)
            index = len(text) if ending < 0 else ending + 1
        elif text.startswith("/*", index):
            ending = text.find("*/", index + 2)
            if ending < 0:
                raise ValueError("Unclosed emitted C# comment")
            index = ending + 2
        elif text.startswith('$"', index) or text.startswith('$@"', index) or text.startswith('@$"', index) or text.startswith('"""', index):
            raise ValueError("Unsupported interpolated/raw string in emitted Work")
        elif text.startswith('@"', index):
            index += 2
            while index < len(text):
                if text.startswith('""', index):
                    index += 2
                elif text[index] == '"':
                    index += 1
                    break
                else:
                    index += 1
            else:
                raise ValueError("Unclosed emitted C# verbatim string")
        elif text[index] in ('"', "'"):
            quote = text[index]
            index += 1
            while index < len(text):
                if text[index] == "\\":
                    index += 2
                elif text[index] == quote:
                    index += 1
                    break
                else:
                    index += 1
            else:
                raise ValueError("Unclosed emitted C# quoted literal")
        elif text[index] == "{":
            depth += 1
            index += 1
        elif text[index] == "}":
            depth -= 1
            if depth == 0:
                return index
            index += 1
        else:
            index += 1
    raise ValueError("Unclosed emitted Work body")

def instrument(directory):
    """Archive source, wrap exactly one emitted Work, return immutable provenance."""
    directory = Path(directory)
    source = directory / "bench.cs"
    project = directory / "bench.csproj"
    if not source.is_file() or not project.is_file():
        raise ValueError("Expected exact C# source/project artifacts are absent")
    before = source.read_bytes()
    text = before.decode("utf-8")
    matches = list(_SIGNATURE.finditer(text))
    if len(matches) != 1 or _MARK in text:
        raise ValueError("Expected one uninstrumented BigInteger Work(BigInteger) method")
    match = matches[0]
    opening = match.end() - 1
    closing = _closing_brace(text, opening)
    body = text[opening + 1:closing]
    indent = match.group("indent")
    field = indent + "private static int " + _MARK + ";\n"
    prefix = """
      int __issue113ThreadBefore = System.Environment.CurrentManagedThreadId;
      long __issue113BytesBefore = System.GC.GetAllocatedBytesForCurrentThread();
      long __issue113Start = System.Diagnostics.Stopwatch.GetTimestamp();
      try {
"""
    suffix = """
      } finally {
        long __issue113End = System.Diagnostics.Stopwatch.GetTimestamp();
        long __issue113BytesAfter = System.GC.GetAllocatedBytesForCurrentThread();
        int __issue113ThreadAfter = System.Environment.CurrentManagedThreadId;
        int __issue113Call = __Issue113WorkProfileCalls++;
        System.Console.WriteLine(
          "profile:" + __issue113Call.ToString(System.Globalization.CultureInfo.InvariantCulture) +
          ":" + (__issue113BytesAfter - __issue113BytesBefore).ToString(System.Globalization.CultureInfo.InvariantCulture) +
          ":" + (__issue113End - __issue113Start).ToString(System.Globalization.CultureInfo.InvariantCulture) +
          ":" + System.Diagnostics.Stopwatch.Frequency.ToString(System.Globalization.CultureInfo.InvariantCulture) +
          ":" + __issue113ThreadBefore.ToString(System.Globalization.CultureInfo.InvariantCulture) +
          ":" + __issue113ThreadAfter.ToString(System.Globalization.CultureInfo.InvariantCulture));
      }
"""
    transformed = text[:match.start()] + field + text[match.start():opening + 1] + prefix + body + suffix + text[closing:]
    after = transformed.encode("utf-8")
    archive = directory / "bench.cs.uninstrumented"
    if archive.exists():
        raise ValueError("Profiling archive already exists; use a fresh build directory")
    archive.write_bytes(before)
    source.write_bytes(after)
    return {"mode": "cs-allocated", "source": str(source.resolve()), "project": str(project.resolve()),
            "original_sha256": _sha(before), "instrumented_sha256": _sha(after),
            "body_sha256": _sha(body.encode("utf-8")),
            "scope": "same Work body inside try/finally, two calls per independent process",
            "metric": "current-thread managed allocated bytes; not object count, live heap or native memory"}

def parse_output(stdout):
    """Validate two synchronous Work intervals; preserve original checksum output."""
    measurements = []
    ordinary = []
    for line in stdout.replace("\r\n", "\n").splitlines(keepends=True):
        if line.startswith("profile:"):
            match = _PROFILE.fullmatch(line.rstrip("\n"))
            if not match:
                raise ValueError("Malformed allocation profile marker")
            call, allocated, ticks, frequency, thread_before, thread_after = map(int, match.groups())
            if call != len(measurements) or frequency <= 0 or thread_before != thread_after:
                raise ValueError("Unexpected Work call count, timer or thread migration")
            measurements.append({"call": call, "managed_allocation_bytes": allocated,
                                 "elapsed_ticks": ticks, "frequency": frequency,
                                 "seconds": ticks / frequency, "thread": thread_before})
        else:
            ordinary.append(line)
    if len(measurements) != 2 or measurements[0]["thread"] != measurements[1]["thread"]:
        raise ValueError("Expected same-thread warmup and measured Work calls")
    return "".join(ordinary), measurements
