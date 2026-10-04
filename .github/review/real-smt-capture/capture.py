"""Bounded fail-closed strace v6.8 read/write dump reconstruction, not an SMT rewriter.

Full dump blocks follow the completed syscall atomically in strace syscall.c;
write dumps contain the attempted buffer while reads contain returned bytes.
Only positive return lengths contribute to the delivered/produced stream.
"""
import hashlib
import json
import re

MAX_TRACE = 64 * 1024 * 1024
MAX_LINE = 65536
MAX_CALLS = 250000
MAX_PIDS = 2048
MAX_STREAM = 1024 * 1024
MAX_FORMS = 10000
PREFIX = re.compile(r'^(?:\[pid\s+(\d+)\]|(\d+))\s+(\d+\.\d+)\s+(.*)$')
HEX = re.compile(r'^ \| ([0-9a-f]+)  (.{50})(.{16}) \|$')


def require(condition, message):
    if not condition:
        raise ValueError(message)


def split_arguments(value):
    parts = []; start = 0; stack = []; quoted = False; escaped = False
    for i, ch in enumerate(value):
        if quoted:
            if escaped: escaped = False
            elif ch == '\\': escaped = True
            elif ch == '"': quoted = False
        elif ch == '"': quoted = True
        elif ch in '([{':
            stack.append(ch); require(len(stack) <= 32, 'Syscall argument nesting bound')
        elif ch in ')]}':
            require(stack and '([{'.index(stack.pop()) == ')]}'.index(ch), 'Argument delimiter mismatch')
        elif ch == ',' and not stack:
            parts.append(value[start:i].strip()); start = i + 1
    require(not quoted and not stack, 'Incomplete syscall arguments')
    parts.append(value[start:].strip()); return parts


def cstring(value):
    # -xx makes every byte hexadecimal, avoiding permissive Python/C escape guesses.
    require(re.fullmatch(r'"(?:\\x[0-9a-f]{2})*"', value) is not None, 'Incomplete/nonhex strace string')
    return bytes.fromhex(value[1:-1].replace('\\x', '')).decode('utf-8', errors='strict')


def descriptor(value):
    match = re.fullmatch(r'(\d+)<pipe:\[(\d+)\]>', value)
    return (int(match[1]), int(match[2])) if match else None


def calls_from_trace(data):
    require(len(data) <= MAX_TRACE and data.endswith(b'\n'), 'Trace bound or truncated final line')
    calls = []; unfinished = {}; dump = None; seen = set(); counter = 0

    def flush():
        nonlocal dump
        if dump is None: return
        call = dump['call']; expected = dump['expected']; content = dump['bytes']
        if call['name'] in ('readv', 'writev'):
            require(dump['vectorRemaining'] == 0 or (dump['vectorRemaining'] is None and call['result'] <= 0), 'Incomplete iovec dump')
        if expected is not None:
            require(len(content) == expected, 'Dump length does not equal observed buffer length')
        require(len(content) >= max(call['result'], 0), 'Dump shorter than successful syscall return')
        call['bytes'] = bytes(content[:max(call['result'], 0)])
        dump = None

    for line_index, raw in enumerate(data.splitlines()):
        require(len(raw) <= MAX_LINE, 'Trace line bound')
        line = raw.decode('ascii', errors='strict')
        if line.startswith(' | '):
            require(dump is not None, 'Orphan dump block')
            row = HEX.fullmatch(line); require(row is not None, 'Unknown/truncated full dump line')
            offset = int(row[1], 16); require(offset == dump['offset'], 'Dump offset discontinuity')
            column = row[2]
            tokens = column.split(); require(all(re.fullmatch('[0-9a-f]{2}', x) for x in tokens), 'Malformed dump hex')
            require(0 < len(tokens) <= 16, 'Invalid dump row length')
            chunk = bytes.fromhex(''.join(tokens)); dump['bytes'].extend(chunk); dump['offset'] += len(chunk)
            require(len(dump['bytes']) <= MAX_STREAM, 'Dump buffer bound')
            if dump['vectorRemaining'] is not None:
                dump['vectorRemaining'] -= len(chunk); require(dump['vectorRemaining'] >= 0, 'Iovec overflow')
            continue
        if line.startswith(' * '):
            require(dump is not None and dump['call']['name'] in ('readv', 'writev'), 'Orphan iovec marker')
            marker = re.fullmatch(r' \* (\d+) bytes in buffer (\d+)', line)
            require(marker is not None and dump['vectorRemaining'] in (None, 0), 'Malformed iovec boundary')
            require(int(marker[2]) == dump['nextVector'], 'Iovec index discontinuity')
            dump['nextVector'] += 1; dump['vectorRemaining'] = int(marker[1]); dump['offset'] = 0
            continue
        flush()
        record = PREFIX.fullmatch(line); require(record is not None, 'Trace record has no unambiguous PID/time')
        tid = int(record[1] or record[2]); seen.add(tid); require(len(seen) <= MAX_PIDS, 'Trace identity bound')
        text = record[4]
        if text.startswith('+++') or text.startswith('---'):
            require('<detached' not in text, 'Trace detach'); continue
        if text.endswith(' <unfinished ...>'):
            require(tid not in unfinished, 'Overlapping unfinished syscalls')
            unfinished[tid] = (text[:-len(' <unfinished ...>')], line_index); continue
        if text.startswith('<... '):
            resumed = re.fullmatch(r'<\.\.\. ([a-zA-Z0-9_]+) resumed>(.*)', text)
            require(resumed is not None and tid in unfinished, 'Unmatched resumed syscall')
            initial, begin = unfinished.pop(tid)
            require(initial.startswith(resumed[1] + '('), 'Resumed syscall name differs')
            text = initial + resumed[2]
        else: begin = line_index
        match = re.fullmatch(r'([a-zA-Z0-9_]+)\((.*)\)\s+=\s+(-?\d+)(.*)', text)
        # exit/exit_group legitimately have no return. They do not carry I/O bytes.
        if not match:
            require(re.fullmatch(r'(exit|exit_group)\(\d+\)\s+=\s+\?', text) is not None,
                    'Unknown syscall result form')
            continue
        counter += 1; require(counter <= MAX_CALLS, 'Syscall count bound')
        call = {'tid': tid, 'name': match[1], 'args': split_arguments(match[2]),
                'result': int(match[3]), 'suffix': match[4], 'begin': begin, 'end': line_index}
        calls.append(call)
        if call['name'] in ('read', 'readv', 'write', 'writev'):
            arg0 = re.match(r'(\d+)', call['args'][0]); require(arg0 is not None, 'Unrecognized I/O descriptor')
            fd = int(arg0[1])
            selected = (call['name'].startswith('read') and fd == 0) or (call['name'].startswith('write') and fd == 1)
            if selected:
                expected = None
                if call['name'] == 'write':
                    require(re.fullmatch(r'\d+', call['args'][-1]) is not None, 'Invalid attempted write count')
                    expected = int(call['args'][-1])
                elif call['name'] == 'read': expected = max(call['result'], 0)
                dump = {'call': call, 'expected': expected, 'bytes': bytearray(), 'offset': 0,
                        'nextVector': 0, 'vectorRemaining': None}
    flush()
    # A syscall terminated by exit/kill may remain unfinished. Any unfinished I/O
    # on an identified solver is rejected by the qualified-stream phase below.
    return calls, unfinished


def forms(data):
    require(len(data) <= MAX_STREAM, 'SMT stream bound')
    text = data.decode('utf-8', errors='strict'); result = []; i = 0
    while i < len(text):
        if text[i].isspace(): i += 1; continue
        if text[i] == ';':
            newline = text.find('\n', i); require(newline >= 0, 'Unterminated SMT comment'); i = newline + 1; continue
        start = i; depth = 0; string = False; quoted = False
        while i < len(text):
            ch = text[i]
            if string:
                if ch == '"':
                    if i + 1 < len(text) and text[i + 1] == '"': i += 2; continue
                    string = False
            elif quoted:
                if ch == '|': quoted = False
                elif ch == '\\': raise ValueError('Unsupported quoted SMT symbol escape')
            elif ch == '"': string = True
            elif ch == '|': quoted = True
            elif ch == ';':
                newline = text.find('\n', i); require(newline >= 0, 'Unterminated embedded comment'); i = newline; continue
            elif ch == '(':
                depth += 1; require(depth <= 128, 'SMT nesting bound')
            elif ch == ')':
                depth -= 1; require(depth >= 0, 'SMT delimiter mismatch')
                if depth == 0: i += 1; break
            elif depth == 0 and ch.isspace(): break
            i += 1
        require(depth == 0 and not string and not quoted, 'Incomplete SMT frame')
        result.append(text[start:i]); require(len(result) <= MAX_FORMS, 'SMT form bound')
    return result


def analyze(data, ownership, images, solver_path, solver_sha, check_ids, worker_path, dotnet_path, request_bytes, completion):
    calls, unfinished = calls_from_trace(data)
    forks = {}; groups = {}; root_exec = next((x for x in calls if x['name'] == 'execve' and x['result'] == 0), None)
    require(root_exec is not None, 'No traced root exec')
    groups[root_exec['tid']] = root_exec['tid']
    for call in calls:
        if call['name'] not in ('fork', 'vfork', 'clone', 'clone3') or call['result'] <= 0: continue
        child = call['result']; require(child not in forks and child not in groups, 'Trace PID reuse/duplicate fork')
        flags = '|'.join(call['args'])
        require(not any(x in flags for x in ('CLONE_UNTRACED', 'CLONE_NEW')), 'Unsupported clone namespace/trace escape')
        require('set_tid=' not in flags or ('set_tid=NULL' in flags and 'set_tid_size=0' in flags), 'Unsupported explicit clone TID')
        forks[child] = {'parent': call['tid'], 'thread': 'CLONE_THREAD' in flags, 'call': call}
    for _ in range(MAX_PIDS):
        progress = False
        for child, fork in forks.items():
            if child not in groups and fork['parent'] in groups:
                groups[child] = groups[fork['parent']] if fork['thread'] else child; progress = True
        if not progress: break
    require(all(x['tid'] in groups for x in calls), 'Trace process/thread lineage is incomplete')
    pins = {}
    for entry in ownership['ownedProcesses']:
        pin = entry['identity']; pid = pin['pid']; require(pid not in pins, 'Owned numeric identity reuse')
        pins[pid] = pin
    missing = sorted(set(groups.values()) - set(pins))
    require(not missing, 'Traced processes missed live pidfd ownership: ' + repr(missing))
    for child,fork in forks.items():
        if not fork['thread']:
            require(pins[child]['parent'] == groups[fork['parent']], 'Trace fork does not match pinned process parent')
    roots = [x['identity']['pid'] for x in ownership['ownedProcesses'] if x.get('isRoot')]
    require(len(roots) == 1 and pins[groups[root_exec['tid']]]['parent'] == roots[0], 'Traced root does not belong to pinned tracer root')
    worker_execs = [x for x in calls if x['name'] == 'execve' and x['result'] == 0 and
      cstring(x['args'][0]) == dotnet_path and
      [cstring(y) for y in split_arguments(x['args'][1][1:-1])] == [dotnet_path,worker_path]]
    require(len(worker_execs) == 1, 'Expected exactly one frozen-package worker exec')
    worker_exec = worker_execs[0]; worker_pid = groups[worker_exec['tid']]
    interactive = [x for x in calls if x['name'] == 'execve' and x['result'] == 0 and cstring(x['args'][0]) == solver_path]
    interactive = [x for x in interactive if [cstring(y) for y in split_arguments(x['args'][1][1:-1])] == [solver_path, '-in', '-smt2']]
    require(len(interactive) == 1, 'Expected exactly one interactive solver exec')
    solver = interactive[0]; pid = groups[solver['tid']]
    require(pins[pid]['parent'] == worker_pid, 'Solver is not the captured frozen worker child')
    image = [x for x in images if x['identity']['pid'] == pid and x['identity']['startTime'] == pins[pid]['startTime'] and
             x['sha256'] == solver_sha and x['argv'] == [solver_path, '-in', '-smt2']]
    require(len(image) == 1, 'Fast/missed interactive exec image pin: capture incomplete')
    tables = {groups[root_exec['tid']]: {}}; eof = False; delivered = bytearray(); produced = bytearray(); transitions = []
    pipe_owners = {}; channels = {}; worker_input = bytearray(); worker_output = bytearray()
    # Fork descriptor inheritance occurs at the entry point, before child calls,
    # even when a vfork return is printed after the child's successful exec.
    schedule = sorted(calls, key=lambda x: x['begin'] if x['name'] in ('fork','vfork','clone','clone3') else x['end'])
    for call in schedule:
        group = groups[call['tid']]
        if call['name'] in ('fork','vfork','clone','clone3') and call['result'] > 0:
            child = call['result']; child_group = groups[child]
            if child_group != group:
                require(group in tables, 'Missing parent descriptor table')
                tables[child_group] = tables[group] if 'CLONE_FILES' in '|'.join(call['args']) else dict(tables[group])
            continue
        require(group in tables, 'Missing descriptor inheritance')
        table = tables[group]; name = call['name']; args = call['args']; returned = call['result']
        if name in ('pipe', 'pipe2') and returned == 0:
            ends = re.findall(r'(\d+)<pipe:\[(\d+)\]>', args[0]); require(len(ends) == 2 and ends[0][1] == ends[1][1], 'Missing pipe inode')
            for fd, inode in ends: table[int(fd)] = int(inode)
            require(int(ends[0][1]) not in pipe_owners, 'Pipe inode reuse in capture')
            pipe_owners[int(ends[0][1])] = group
        elif name in ('dup', 'dup2', 'dup3') and returned >= 0:
            source = descriptor(args[0])
            if source:
                require(table.get(source[0]) == source[1], 'Unmapped source pipe descriptor')
                table[returned] = source[1]
            else: table.pop(returned, None)
        elif name == 'fcntl' and returned >= 0 and len(args) > 1 and args[1] in ('F_DUPFD','F_DUPFD_CLOEXEC'):
            source = descriptor(args[0])
            if source:
                require(table.get(source[0]) == source[1], 'Unmapped fcntl pipe duplication')
                table[returned] = source[1]
            else: table.pop(returned, None)
        elif name == 'close' and returned == 0:
            table.pop(int(re.match(r'\d+', args[0])[0]), None)
        elif name == 'close_range' and returned == 0:
            require('CLOSE_RANGE_UNSHARE' not in '|'.join(args), 'Unsupported descriptor unshare')
            if 'CLOSE_RANGE_CLOEXEC' not in '|'.join(args):
                first = int(args[0]); last = 4294967295 if args[1] == '~0U' else int(args[1])
                for fd in list(table):
                    if first <= fd <= last: table.pop(fd)
        if group not in (pid,worker_pid) or name not in ('read','readv','write','writev'): continue
        if call['end'] < (solver['end'] if group == pid else worker_exec['end']): continue
        raw_fd = re.match(r'\d+', args[0]); require(raw_fd is not None, 'Unrecognized solver I/O descriptor')
        fd = int(raw_fd[0])
        if (name.startswith('read') and fd == 0) or (name.startswith('write') and fd == 1):
            parsed = descriptor(args[0]); require(parsed is not None, 'Solver I/O lacks decoded pipe inode')
            _, inode = parsed
            require(table.get(fd) == inode, 'Solver stream FD does not match inherited pipe topology')
            require('bytes' in call, 'Selected solver I/O dump missing')
            channels.setdefault(group,{})
            require(fd not in channels[group] or channels[group][fd] == inode, 'Stream descriptor was rebound')
            channels[group][fd] = inode
            if group == pid:
                require(pipe_owners.get(inode) == worker_pid, 'Solver pipe was not created by the captured worker')
                target = delivered if fd == 0 else produced
            else: target = worker_input if fd == 0 else worker_output
            target.extend(call['bytes']); require(len(target) <= MAX_STREAM, 'Reconstructed stream bound')
            require(len(transitions) < 20000,'I/O transition evidence bound')
            transitions.append({'line': call['end'], 'processId':group, 'fd': fd, 'pipeInode': inode, 'call': name, 'returned': returned, 'deliveredBytes': len(call['bytes'])})
            eof |= group == pid and fd == 0 and returned == 0
    require(not any(groups.get(tid) in (pid,worker_pid) and re.match(r'(read|readv|write|writev)\([01](?:<|,)', text[0]) for tid,text in unfinished.items()),
            'Worker/solver stream ended with unfinished I/O')
    require(bytes(worker_input) == request_bytes + b'\n', 'Worker did not consume the exact frozen request and newline')
    records = bytes(worker_output).splitlines()
    require(len(records) == 2, 'Frozen worker output record count differs')
    started = json.loads(records[0]); wire_completion = json.loads(records[1])
    request = json.loads(request_bytes)
    require(started == {'version':2,'requestId':request['requestId'],'sequence':0,'processId':worker_pid,'isolatedProcessGroup':True}, 'Worker startup is not bound to its traced exec identity')
    require(wire_completion == completion, 'Traced worker completion differs from validated replay receipt')
    require(channels[pid][0] != channels[pid][1], 'Solver command and response pipes alias')
    commands = forms(bytes(delivered)); responses = forms(bytes(produced))
    require(len(commands) == len(responses), 'Command/response framing is incomplete')
    attempts = completion['attempts']
    require([x['obligationId'] for x in attempts] == check_ids, 'Original linear check/attempt partition differs')
    queries = [(i,response) for i,(command,response) in enumerate(zip(commands,responses)) if command == '(check-sat)']
    require(len(queries) <= len(check_ids), 'Extra solver check-sat query')
    query_bindings = []
    for index,(command_index,response) in enumerate(queries):
        mathematical = {'unsat':'Verified','sat':'Failed','unknown':'Inconclusive'}.get(response)
        require(mathematical is not None or response.startswith('(error '), 'Unknown check-sat response shape')
        actual = attempts[index]['outcome']
        require(actual == 'ToolError' or actual == mathematical, 'Solver response and validated attempt differ')
        query_bindings.append({'obligationId':check_ids[index],'commandIndex':command_index,'response':response,'attemptOutcome':actual})
    require(all(x['outcome'] == 'ToolError' for x in attempts[len(queries):]), 'Unqueried original check is not an explicit ToolError')
    error_indices = [i for i,x in enumerate(responses) if x.startswith('(error ')]
    require(not error_indices or error_indices == [len(responses)-1], 'Commands issued after the native solver session failure latch')
    require('(set-option :smt.arith.solver 2)' in commands and '(set-option :rlimit 200000)' in commands and
            '(set-option :timeout 20000)' in commands, 'Captured solver pins/config differ')
    return {'captureComplete': True, 'solverIdentity': pins[pid], 'solverExec': solver_path,
            'solverImage': image[0], 'commandBytes': bytes(delivered), 'responseBytes': bytes(produced),
            'commands': commands, 'responses': responses, 'ioTransitions': transitions, 'stdinEofObserved': eof,
            'emittedCheckQueries':query_bindings, 'unqueriedToolErrorCheckIds':check_ids[len(queries):],
            'workerProcessId':worker_pid, 'workerInputSha256':hashlib.sha256(bytes(worker_input)).hexdigest(),
            'workerOutputSha256':hashlib.sha256(bytes(worker_output)).hexdigest(), 'orderedCheckIds': check_ids, 'workerResponseConsumptionClaimed': False,
            'noQuantifiedCommandObserved': not any(re.search(r'\b(forall|exists)\b', x) for x in commands)}


def weak_observations(data, solver_path):
    """Retain trace-line observations without any live-image/ownership/FD claim.

    A reported TID is scoped to one successful exec line, never reassociated
    with a later process or joined across an observed PID-reuse/exec boundary.
    Other threads are intentionally not folded into these observation-only bytes.
    """
    calls,unfinished = calls_from_trace(data)
    result = []
    for candidate in calls:
        if candidate['name'] != 'execve' or candidate['result'] != 0: continue
        if cstring(candidate['args'][0]) != solver_path: continue
        if [cstring(x) for x in split_arguments(candidate['args'][1][1:-1])] != [solver_path,'-in','-smt2']: continue
        reads = bytearray(); writes = bytearray(); stop = None; count = 0
        for call in calls:
            if call['end'] <= candidate['end']: continue
            if ((call['name'] == 'execve' and call['result'] == 0 and call['tid'] == candidate['tid']) or
              (call['name'] in ('fork','vfork','clone','clone3') and call['result'] == candidate['tid'])):
                stop = call['end']; break
            if call['tid'] != candidate['tid'] or 'bytes' not in call: continue
            fd = int(re.match(r'\d+',call['args'][0])[0])
            target = reads if call['name'].startswith('read') and fd == 0 else \
              writes if call['name'].startswith('write') and fd == 1 else None
            if target is not None:
                target.extend(call['bytes']); count += 1
                require(len(target) <= MAX_STREAM,'Weak observed stream bound')
        row = {'observationOnly':True,'kernelImageQualified':False,'ownershipQualified':False,'fdTopologyQualified':False,
          'traceReportedTid':candidate['tid'],'successfulExecLine':candidate['end'],'execPath':solver_path,
          'stopAtExecOrPidReuseLine':stop,'otherThreadsIncluded':False,'completedIoCalls':count,
          'unfinishedSameTidIoObserved':candidate['tid'] in unfinished,
          'commandBytes':bytes(reads),'responseBytes':bytes(writes)}
        for key,value in [('observedCommandForms',reads),('observedResponseForms',writes)]:
            try: row[key] = forms(bytes(value))
            except ValueError as error: row[key+'Error'] = str(error)
        result.append(row); require(len(result) <= 16,'Weak exec observation bound')
    return result
