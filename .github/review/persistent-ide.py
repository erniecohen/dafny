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


def run(name, overlay=text, directory=cache, extra=(), workspace=destination, document=program, manual=False, module_reports=False):
    uri = document.as_uri()
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
            cwd=workspace, start_new_session=os.name != "nt")
        messages = queue.Queue()
        received = []

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
                received.append(message)
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
            send("initialize", {"processId": None, "rootUri": workspace.as_uri(),
                               "capabilities": {}}, 1)
            assert "error" not in wait(lambda m: m.get("id") == 1)
            send("initialized", {})
            send("textDocument/didOpen", {"textDocument": {"uri": uri, "languageId": "dafny",
                                                         "version": 1, "text": overlay}})
            if manual:
                idle = wait(lambda m: m.get("method") == "dafny/textDocument/symbolStatus" and
                    m["params"].get("uri") == uri and m["params"].get("version") == 1 and
                    m["params"].get("namedVerifiables"))
                assert all(symbol["status"] in (0, "stale", "Stale")
                           for symbol in idle["params"]["namedVerifiables"]), "manual mode ran on open"
                send("dafny/textDocument/verifySymbol", {"textDocument": {"uri": uri},
                    "position": {"line": 50, "character": 0}}, 10)
                assert wait(lambda m: m.get("id") == 10).get("result") is False
                send("textDocument/didSave", {"textDocument": {"uri": uri}})
                send("textDocument/hover", {"textDocument": {"uri": uri},
                    "position": {"line": 0, "character": 0}}, 11)
                assert "error" not in wait(lambda m: m.get("id") == 11)
                before = [json.loads(line) for line in profile.read_text().splitlines()]
                assert not any(e.get("ev") == "result-task" for e in before), "manual mode ran on save"
                send("dafny/textDocument/verifySymbol", {"textDocument": {"uri": uri},
                    "position": {"line": overlay[:overlay.index("Positive")].count("\n"), "character": 8}}, 12)
                assert wait(lambda m: m.get("id") == 12).get("result") is True
            def completed(m):
                return m.get("method") == "dafny/textDocument/symbolStatus" and (
                m["params"].get("uri") == uri and m["params"].get("version") == 1 and
                m["params"].get("namedVerifiables") and all(
                    symbol["status"] in (5, "correct", "Correct") for symbol in m["params"]["namedVerifiables"]))
            # A warm task can finish before the manual request's response is read.
            status = next((m for m in reversed(received) if completed(m)), None) or wait(completed)
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
        assert tasks or module_reports and any(e.get("ev") == "persistent-module" and e.get("hit") for e in events), (name, "no completed task evidence")
        facts = sorted((e["task"], e["split"], e["seed"], e["outcome"], e["resourceCount"]) for e in tasks)
        assert all(e["outcome"] == "Valid" for e in tasks)
        return status["params"]["namedVerifiables"], facts, tasks


def main():
    cold = run("cold")
    warm = run("warm")
    assert cold[:2] == warm[:2], "changed IDE verdicts, positions or per-VC resource counts"
    assert all(not e["replayed"] for e in cold[2]), "cold cache unexpectedly populated"
    assert all(e["replayed"] for e in warm[2]), "warm IDE transport did not actually hit"
    audit = run("audit", extra=("--persistent-translation-audit",))
    assert audit[:2] == cold[:2], "audited native IDE preparation changed a result"
    assert all(not e["replayed"] for e in audit[2]), "audit reused its stored task evidence"
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


    def cli(name, directory):
        # Use the late task transport, so a whole-module hit cannot hide missing interoperability.
        profile = destination / (name + ".jsonl")
        with (destination / (name + ".stdout")).open("wb") as output:
            result = subprocess.run([shutil.which("dotnet"), str(exe), "verify", str(program),
                "--type-system-refresh", "--verification-cache", str(directory),
                "--solver-path", str(solver), "--cores", "2", "--verification-time-limit", "0",
                "--resource-limit", "1000000", "--pipeline-profile", str(profile)],
                stdout=output, stderr=subprocess.STDOUT, timeout=90)
        assert result.returncode == 0, name
        tasks = [e for e in map(json.loads, profile.read_text().splitlines()) if e.get("ev") == "result-task"]
        assert tasks, (name, "no completed CLI tasks")
        return sorted((e["task"], e["split"], e["seed"], e["outcome"], e["resourceCount"]) for e in tasks), tasks


    def keys(tasks):
        return sorted(e["executionKey"] for e in tasks)


    program.write_text(text)
    fresh_cli = cli("cli-fresh", destination / "cli-fresh-cache")
    shared = destination / "cli-to-ide-cache"
    cli_cold = cli("cli-cold", shared)
    assert cli_cold[0] == fresh_cli[0] == cold[1]
    assert all(not e["replayed"] for e in cli_cold[1])
    from_cli = run("cli-to-ide", directory=shared)
    assert from_cli[1] == fresh_cli[0] and keys(from_cli[2]) == keys(cli_cold[1])
    assert all(e["replayed"] for e in from_cli[2]), "CLI-populated tasks did not hit in the IDE"
    reverse = destination / "ide-to-cli-cache"
    ide_cold = run("ide-cold", directory=reverse)
    assert all(not e["replayed"] for e in ide_cold[2])
    from_ide = cli("ide-to-cli", reverse)
    assert from_ide[0] == fresh_cli[0] and keys(from_ide[1]) == keys(ide_cold[2])
    assert all(e["replayed"] for e in from_ide[1]), "IDE-populated tasks did not hit in the CLI"

    # Inactive legacy-cache sentinels can agree; positive levels must retain their identity.
    levels = []
    for level in (1, 2):
        extra = ("--cache-verification", str(level))
        checked = run("level-" + str(level), directory=shared, extra=extra)
        oracle = run("level-oracle-" + str(level), directory=destination / ("level-cache-" + str(level)), extra=extra)
        assert checked[:2] == oracle[:2]
        assert all(not e["replayed"] for e in checked[2]), "positive legacy-cache level lost its identity"
        assert keys(checked[2]) != keys(from_cli[2])
        levels.append(keys(checked[2]))
    assert levels[0] != levels[1], "distinct positive legacy-cache levels were merged"
    seeded = run("seeded", directory=shared, extra=("--boogie", "/randomSeed:17"))
    seed_oracle = run("seed-oracle", directory=destination / "seed-cache", extra=("--boogie", "/randomSeed:17"))
    assert seeded[:2] == seed_oracle[:2]
    assert all(not e["replayed"] for e in seeded[2]) and keys(seeded[2]) != keys(from_cli[2])
    print("CLI/IDE task reuse, legacy levels and seed isolation pass")

    manual_store = destination / "manual-cache"
    manual_extra = ("--verify-on", "Never")
    manual_fresh = run("manual-fresh", directory=destination / "manual-oracle", extra=manual_extra, manual=True)
    manual_cold = run("manual-cold", directory=manual_store, extra=manual_extra, manual=True)
    manual_warm = run("manual-warm", directory=manual_store, extra=manual_extra, manual=True)
    manual_audit = run("manual-audit", directory=manual_store,
                       extra=(*manual_extra, "--persistent-translation-audit"), manual=True)
    assert manual_fresh[:2] == manual_cold[:2] == manual_warm[:2] == manual_audit[:2]
    assert all(not e["replayed"] for e in manual_cold[2])
    assert all(e["replayed"] for e in manual_warm[2]), "manual warm request did not hit"
    assert all(not e["replayed"] for e in manual_audit[2]), "manual audit reused its stored task evidence"
    print("Manual requests defer verification until selection and preserve fresh VC facts")


if __name__ == "__main__":
    main()
