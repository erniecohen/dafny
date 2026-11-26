"""Real language-server restart: persistent hits must retain native IDE evidence."""
import json
import os
from pathlib import Path
import queue
import shutil
import signal
import subprocess
import sys
import threading
import time

exe, solver, source, destination = (Path(p).resolve() for p in sys.argv[1:])
if destination.exists():
    shutil.rmtree(destination)
destination.mkdir(parents=True)
program = destination / "input.dfy"
# An unsaved overlay must win over the different bytes on disk.
program.write_text("lemma Disk() { assert false; }\n")
cache = destination / "cache"
text = source.read_text()
uri = program.as_uri()


def run(name, overlay=text, directory=cache, extra=()):
    profile = destination / (name + ".jsonl")
    environment = {k: v for k, v in os.environ.items()
                   if k not in ("DAFNY_CACHE_DIR", "DAFNY_CACHE_PARTITION")}
    with (destination / (name + ".stderr")).open("wb") as errors:
        process = subprocess.Popen([shutil.which("dotnet"), str(exe), "server",
            "--type-system-refresh", "--verification-cache", str(directory),
            "--persistent-translation", "--persistent-counterexamples", "--resolution-cache-check",
            "--solver-path", str(solver), "--cores", "2", "--translation-workers", "2", "--verification-time-limit", "0",
            "--resource-limit", "1000000", "--pipeline-profile", str(profile), *extra],
            stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=errors, env=environment,
            cwd=destination, start_new_session=os.name != "nt")
        messages = queue.Queue()

        def read():
            try:
                while True:
                    headers = {}
                    while True:
                        line = process.stdout.readline()
                        if not line:
                            raise EOFError("language server exited")
                        if line == b"\r\n":
                            break
                        key, value = line.decode().split(":", 1)
                        headers[key.lower()] = value.strip()
                    length = int(headers["content-length"])
                    payload = process.stdout.read(length)
                    messages.put(json.loads(payload))
            except Exception as error:
                messages.put(error)

        threading.Thread(target=read, daemon=True).start()

        def send(method, params=None, identifier=None):
            message = {"jsonrpc": "2.0", "method": method, "params": params}
            if identifier is not None:
                message["id"] = identifier
            emit(message)

        def emit(message):
            payload = json.dumps(message).encode()
            process.stdin.write(b"Content-Length: " + str(len(payload)).encode() + b"\r\n\r\n" + payload)
            process.stdin.flush()

        def wait(predicate):
            deadline = time.monotonic() + 60
            while time.monotonic() < deadline:
                try:
                    message = messages.get(timeout=max(.01, deadline - time.monotonic()))
                except queue.Empty as error:
                    raise TimeoutError(name + ": no matching server progress; see protocol log") from error
                if isinstance(message, Exception):
                    raise message
                with (destination / (name + ".protocol.jsonl")).open("a") as protocol:
                    protocol.write(json.dumps(message) + "\n")
                if "id" in message and "method" in message:
                    # OmniSharp asks clients for configuration and dynamic registrations.
                    result = [{} for _ in message["params"]["items"]] if message["method"] == "workspace/configuration" else None
                    emit({"jsonrpc": "2.0", "id": message["id"], "result": result})
                elif predicate(message):
                    return message
            raise TimeoutError("language server made no matching progress")

        try:
            send("initialize", {"processId": None, "rootUri": destination.as_uri(),
                               "capabilities": {}}, 1)
            assert "error" not in wait(lambda m: m.get("id") == 1)
            send("initialized", {})
            send("textDocument/didOpen", {"textDocument": {"uri": uri, "languageId": "dafny",
                                                         "version": 1, "text": overlay}})
            status = wait(lambda m: m.get("method") == "dafny/textDocument/symbolStatus" and
                m["params"].get("uri") == uri and m["params"].get("version") == 1 and
                m["params"].get("namedVerifiables") and all(
                    symbol["status"] in (5, "correct", "Correct") for symbol in m["params"]["namedVerifiables"]))
            send("textDocument/hover", {"textDocument": {"uri": uri},
                "position": {"line": overlay[:overlay.index("Positive")].count("\n"), "character": 8}}, 2)
            assert "error" not in wait(lambda m: m.get("id") == 2)
            send("shutdown", None, 3)
            assert "error" not in wait(lambda m: m.get("id") == 3)
            send("exit")
            process.wait(timeout=15)
        finally:
            if process.poll() is None:
                if os.name == "nt":
                    process.kill()
                else:
                    os.killpg(process.pid, signal.SIGKILL)
                process.wait()
        events = [json.loads(line) for line in profile.read_text().splitlines()]
        tasks = [e for e in events if e.get("ev") == "result-task"]
        assert tasks, (name, "no completed task evidence")
        facts = sorted((e["task"], e["split"], e["seed"], e["outcome"], e["resourceCount"]) for e in tasks)
        assert all(e["outcome"] == "Valid" for e in tasks)
        return status["params"]["namedVerifiables"], facts, tasks


cold = run("cold")
warm = run("warm")
assert cold[:2] == warm[:2], "changed IDE verdicts, positions or per-VC resource counts"
assert all(not e["replayed"] for e in cold[2]), "cold cache unexpectedly populated"
assert all(e["replayed"] for e in warm[2]), "warm IDE transport did not actually hit"
audit = run("audit", extra=("--persistent-translation-audit",))
assert audit[:2] == cold[:2], "audited native IDE preparation changed a result"
changed = text.replace("x + 1", "x + 2")
assert changed != text
stale = run("stale", overlay=changed)
oracle = run("changed-oracle", overlay=changed, directory=destination / "oracle-cache")
assert stale[:2] == oracle[:2], "stale entry changed a current VC"
assert all(not e["replayed"] for e in stale[2]), "changed solver input reused an old result"
assert {e["executionKey"] for e in stale[2]}.isdisjoint(e["executionKey"] for e in warm[2])
records = list((cache / "v1" / "results").rglob("data"))
assert records, "no persistent task records to corrupt"
for record in records:
    record.write_bytes(b"corrupted result")
corrupt = run("corrupt")
assert corrupt[:2] == cold[:2], "corrupted entry changed an IDE result"
assert all(not e["replayed"] for e in corrupt[2]), "corrupted results did not fall back"
print("IDE restart, overlays, stale/corrupt fallback and per-VC equality pass")
