"""Compressed verification-cache blobs (https://github.com/erniecohen/dafny/issues/388).

A blob is stored as a header and a zlib stream, or as its own bytes, and is addressed by the
SHA-256 of its decoded bytes. A cold run must store compressed blobs whose decoded bytes hash to
their addresses; a warm run must replay every task; a cache whose blobs are rewritten as earlier
writers stored them (raw), wholly or in part, must still replay; and a cache whose compressed
blobs are truncated or corrupted must recompute every affected task with the same report.
"""
import hashlib
import json
from pathlib import Path
import shutil
import struct
import subprocess
import sys
import zlib

exe, solver, source, destination = (Path(s).resolve() for s in sys.argv[1:5])
if destination.exists():
    shutil.rmtree(destination)
destination.mkdir(parents=True)
input_file = destination / "input.dfy"
input_file.write_text(source.read_text(encoding="utf-8"), encoding="utf-8")
MAGIC = b"\0dcz"


def run(name, cache):
    profile = destination / (name + ".jsonl")
    args = ["dotnet", str(exe), "verify", str(input_file), "--cores", "1", "--type-system-refresh",
            "--solver-path", str(solver), "--verification-time-limit", "0", "--resource-limit", "16000000", "--use-basename-for-filename",
            "--show-snippets:false", "--pipeline-profile", str(profile), "--verification-cache", str(cache)]
    result = subprocess.run(args, capture_output=True, timeout=300)
    events = [json.loads(line) for line in profile.read_text().splitlines()]
    stats = [e for e in events if e.get("ev") == "result-cache"]
    assert len(stats) == 1, (name, result)
    return (result.returncode, result.stdout, result.stderr), stats[0]


def blobs(cache):
    return sorted((cache / "v1/blobs/sha256").glob("*/*/data"))


def result_blobs(cache):
    for index in sorted((cache / "v1/results").glob("*/*/data")):
        digest = json.loads(index.read_text())["Blob"]
        yield cache / "v1/blobs/sha256" / digest[:2] / digest / "data"


def decode(stored):
    if not stored.startswith(MAGIC):
        return stored
    assert stored[4:6] == b"\x01\x01", stored[:10]
    length, = struct.unpack(">I", stored[6:10])
    decoded = zlib.decompress(stored[10:])
    assert len(decoded) == length
    return decoded


cache = destination / "cache"
cold, cold_stats = run("cold", cache)
assert cold_stats["solverStarts"] == 2 and cold_stats["publications"] == 2, cold_stats
results = list(result_blobs(cache))
assert len(results) == 2
# Every blob decodes to bytes that hash to its address; every result record is compressed.
for blob in blobs(cache):
    assert hashlib.sha256(decode(blob.read_bytes())).hexdigest() == blob.parent.name, blob
for blob in results:
    stored = blob.read_bytes()
    assert stored.startswith(MAGIC) and len(stored) < len(decode(stored)), blob
    json.loads(decode(stored))

warm, warm_stats = run("warm", cache)
assert warm == cold and warm_stats["hits"] == 2 and warm_stats["solverStarts"] == 0, warm_stats

# Caches written before compression stored every blob raw. Rewrite all, then one, as raw.
for name, rewrite in (("uncompressed", results), ("mixed", results[:1])):
    copy = destination / (name + "-cache")
    shutil.copytree(cache, copy)
    for blob in rewrite:
        target = copy / blob.relative_to(cache)
        target.write_bytes(decode(target.read_bytes()))
    assert sum(not (copy / b.relative_to(cache)).read_bytes().startswith(MAGIC) for b in results) == len(rewrite)
    output, stats = run(name, copy)
    assert output == cold and stats["hits"] == 2 and stats["solverStarts"] == 0, (name, stats)

# A truncated or corrupted compressed blob is a miss, never a different result.
for name, damage in (("truncated", lambda b: b[:-1]), ("short", lambda b: b[:len(b) // 2]),
                     ("flipped", lambda b: b[:-8] + bytes([b[-8] ^ 1]) + b[-7:]),
                     ("length", lambda b: b[:9] + bytes([b[9] ^ 1]) + b[10:])):
    copy = destination / (name + "-cache")
    shutil.copytree(cache, copy)
    for blob in results:
        target = copy / blob.relative_to(cache)
        target.write_bytes(damage(target.read_bytes()))
    output, stats = run(name, copy)
    assert output == cold and stats["hits"] == 0 and stats["solverStarts"] == 2, (name, stats)

print("verification cache compression: compressed round trip; uncompressed and mixed caches read; corrupt blobs miss")
