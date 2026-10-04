"""Parser regressions for future CI; these controls never invoke a solver or worker."""
from pathlib import Path
import unittest

# Test loading is deliberately source compile/exec, avoiding an untrusted pycache.
import types
capture = types.ModuleType('capture_tested')
capture.__file__ = str(Path(__file__).with_name('capture.py'))
exec(compile(Path(capture.__file__).read_bytes(),capture.__file__,'exec'),capture.__dict__)


def dump(data, offset=0):
    column = ''.join((f'{b:02x}' if i < len(data) else '  ') + ' ' + (' ' if (i + 1) % 8 == 0 else '')
      for i,b in enumerate(data + bytes(16-len(data))))
    return ' | '+f'{offset:05x}'+'  '+column+''.join(chr(x) if 32<=x<127 else '.' for x in data).ljust(16)+' |\n'


class CaptureControls(unittest.TestCase):
    def test_partial_write_uses_return_length(self):
        trace = '10 1.0 write(1<pipe:[2]>, "\\x61\\x62\\x63\\x64\\x65", 5) = 3\n'+dump(b'abcde')
        calls,_ = capture.calls_from_trace(trace.encode()); self.assertEqual(calls[0]['bytes'],b'abc')

    def test_read_uses_exact_returned_buffer(self):
        trace = '10 1.0 read(0<pipe:[2]>, "\\x61\\x62", 8) = 2\n'+dump(b'ab')
        calls,_ = capture.calls_from_trace(trace.encode()); self.assertEqual(calls[0]['bytes'],b'ab')

    def test_error_write_delivers_no_attempted_bytes(self):
        trace = '10 1.0 write(1<pipe:[2]>, "\\x61\\x62", 2) = -1 EPIPE (Broken pipe)\n'+dump(b'ab')
        calls,_ = capture.calls_from_trace(trace.encode()); self.assertEqual(calls[0]['bytes'],b'')

    def test_unfinished_then_resumed_read_keeps_original_tid(self):
        trace = '10 1.0 read(0<pipe:[2]>,  <unfinished ...>\n10 1.1 <... read resumed>"\\x61", 8) = 1\n'+dump(b'a')
        calls,remaining = capture.calls_from_trace(trace.encode())
        self.assertEqual(calls[0]['tid'],10); self.assertEqual(calls[0]['bytes'],b'a'); self.assertEqual(remaining,{})

    def test_partial_writev_flattens_all_buffers_then_slices(self):
        trace = '10 1.0 writev(1<pipe:[2]>, [{iov_base="\\x61\\x62\\x63", iov_len=3}, {iov_base="\\x64\\x65", iov_len=2}], 2) = 4\n'
        trace += ' * 3 bytes in buffer 0\n'+dump(b'abc')+' * 2 bytes in buffer 1\n'+dump(b'de')
        calls,_ = capture.calls_from_trace(trace.encode()); self.assertEqual(calls[0]['bytes'],b'abcd')

    def test_missing_successful_dump_is_rejected(self):
        with self.assertRaises(ValueError): capture.calls_from_trace(b'10 1.0 read(0<pipe:[2]>, "\\x61", 8) = 1\n')

    def test_gap_in_dump_offsets_is_rejected(self):
        trace = '10 1.0 read(0<pipe:[2]>, "\\x61", 8) = 1\n'+dump(b'a',16)
        with self.assertRaises(ValueError): capture.calls_from_trace(trace.encode())

    def test_orphan_dump_is_rejected(self):
        with self.assertRaises(ValueError): capture.calls_from_trace(dump(b'ab').encode())

    def test_missing_pid_prefix_is_rejected(self):
        with self.assertRaises(ValueError): capture.calls_from_trace(b'1.0 write(1, "x", 1) = 1\n')

    def test_truncated_final_line_is_rejected(self):
        with self.assertRaises(ValueError): capture.calls_from_trace(b'10 1.0 read(0, "x", 1) = 1')

    def test_smt_framing_retains_strings_comments_and_atoms(self):
        self.assertEqual(capture.forms(b'(error "push canceled")\n; note\nunknown\n'),['(error "push canceled")','unknown'])

    def test_absent_live_image_is_capture_incomplete(self):
        trace,ownership = minimal_exec_trace()
        with self.assertRaisesRegex(ValueError,'Fast/missed interactive exec image'):
            capture.analyze(trace,ownership,[], '/solver','f'*64,['sCheck'],'/worker.dll','/dotnet',b'{}',{},['/dotnet','/replay.dll'])

    def test_detached_fork_parent_is_rejected(self):
        trace,ownership = minimal_exec_trace()
        ownership['ownedProcesses'][-1]['identity']['parent'] = 10
        with self.assertRaisesRegex(ValueError,'pinned process parent'):
            capture.analyze(trace,ownership,[], '/solver','f'*64,['sCheck'],'/worker.dll','/dotnet',b'{}',{},['/dotnet','/replay.dll'])

    def test_reused_owned_numeric_pid_is_rejected(self):
        trace,ownership = minimal_exec_trace()
        ownership['ownedProcesses'].append({'identity':{'pid':30,'parent':20,'startTime':99}})
        with self.assertRaisesRegex(ValueError,'numeric identity reuse'):
            capture.analyze(trace,ownership,[], '/solver','f'*64,['sCheck'],'/worker.dll','/dotnet',b'{}',{},['/dotnet','/replay.dll'])

    def test_exact_three_executable_epochs_are_admitted(self):
        validate_epochs(minimal_exec_trace()[0])

    def test_replay_argv_must_equal_the_actual_launch(self):
        with self.assertRaisesRegex(ValueError,'exact launch argv'):
            validate_epochs(minimal_exec_trace()[0],root_argv=['/dotnet','/different.dll'])

    def test_later_successful_exec_cannot_reuse_the_solver_image_pin(self):
        trace=minimal_exec_trace()[0]+exec_line(30,'/other',['/other']).encode()
        with self.assertRaisesRegex(ValueError,'executable epoch'):
            validate_epochs(trace)

    def test_unexpected_successful_descendant_executable_is_rejected(self):
        trace=minimal_exec_trace()[0]+b'20 2.0 vfork() = 40\n'+exec_line(40,'/other',['/other']).encode()
        with self.assertRaisesRegex(ValueError,'Unexpected successful executable'):
            validate_epochs(trace,extra={40:{'pid':40,'parent':20,'startTime':40}})

    def test_successful_execveat_is_explicitly_unsupported(self):
        trace=minimal_exec_trace()[0]+b'30 2.0 execveat(4, "", [], 0x123, AT_EMPTY_PATH) = 0\n'
        with self.assertRaisesRegex(ValueError,'execveat'):
            validate_epochs(trace)

    def test_weak_observation_stops_at_a_successful_execveat(self):
        trace=minimal_exec_trace()[0]
        trace+=('30 2.0 read(0<pipe:[2]>, "\\x61", 8) = 1\n'+dump(b'a')).encode()
        trace+=b'30 3.0 execveat(4, "", [], 0x123, AT_EMPTY_PATH) = 0\n'
        trace+=('30 4.0 read(0<pipe:[2]>, "\\x62", 8) = 1\n'+dump(b'b')).encode()
        observed=capture.weak_observations(trace,'/solver')
        self.assertEqual(len(observed),1); self.assertEqual(observed[0]['commandBytes'],b'a')
        self.assertIsNotNone(observed[0]['stopAtExecOrPidReuseLine'])
        self.assertFalse(observed[0]['kernelImageQualified'])

    def test_incomplete_smt_string_is_rejected(self):
        with self.assertRaises(ValueError): capture.forms(b'(error "truncated)')


def minimal_exec_trace():
    trace=exec_line(10,'/dotnet',['/dotnet','/replay.dll'])
    trace+='10 1.0 vfork() = 20\n'+exec_line(20,'/dotnet',['/dotnet','/worker.dll'])
    trace+='20 1.0 vfork() = 30\n'+exec_line(30,'/solver',['/solver','-in','-smt2'])
    ownership={'ownedProcesses':[{'identity':{'pid':pid,'parent':parent,'startTime':pid},'isRoot':pid==100}
      for pid,parent in [(100,1),(10,100),(20,10),(30,20)]]}
    return trace.encode(),ownership


def quoted(value): return '"'+''.join('\\x'+f'{x:02x}' for x in value.encode())+'"'


def exec_line(pid,path,argv):
    return f'{pid} 1.0 execve({quoted(path)}, ['+', '.join(quoted(x) for x in argv)+'], 0x123 /* 8 vars */) = 0\n'


def validate_epochs(trace,root_argv=None,extra=None):
    calls,_=capture.calls_from_trace(trace)
    groups={10:10,20:20,30:30}
    pins={row['identity']['pid']:row['identity'] for row in minimal_exec_trace()[1]['ownedProcesses']}
    for pid,pin in (extra or {}).items(): groups[pid]=pid; pins[pid]=pin
    capture.validate_exec_epochs(calls,groups,pins,calls[0],calls[2],calls[4],
      ['/dotnet','/replay.dll'] if root_argv is None else root_argv,'/dotnet','/worker.dll','/solver')


# Archive admission controls use only synthetic ZIP data; no process is launched.
import io
import stat
import tempfile
from types import SimpleNamespace

archive_gate=types.ModuleType('archive_gate_tested')
archive_gate.__file__=str(Path(__file__).with_name('gate.py'))
exec(compile(Path(archive_gate.__file__).read_bytes(),archive_gate.__file__,'exec'),archive_gate.__dict__)


def archive_require(condition,message):
    if not condition: raise ValueError(message)


def archive_members(expected):
    entries=[]
    for root,count in expected['archiveMembers'].items():
        for i in range(count):
            entry=archive_gate.zipfile.ZipInfo(root+'/f'+str(i))
            entry.external_attr=(stat.S_IFREG|0o644)<<16
            entries.append(entry)
    return entries


class ArchiveControls(unittest.TestCase):
    def preflight(self,entries,expected):
        return archive_gate.preflight_members(SimpleNamespace(require=archive_require),entries,expected)

    def test_original_archive_admits_exact_compile_and_binary_roots(self):
        expected=archive_gate.PUBLIC[0]
        self.assertEqual(expected['run'],37232113837)
        self.assertEqual(expected['archiveMembers'],{'out/b3-native-compile':677,'Binaries/net8.0':335})
        rows=self.preflight(archive_members(expected),expected)
        self.assertEqual(len(rows),1012)

    def test_prerequisite_archive_also_admits_exact_host_test_root(self):
        expected=archive_gate.PUBLIC[1]
        self.assertEqual(expected['run'],37221479537)
        self.assertEqual(expected['archiveMembers'],{'out/b3-native-compile':1186,'Binaries/net8.0':344,'build/b3-host-tests':9})
        rows=self.preflight(archive_members(expected),expected)
        self.assertEqual(len(rows),1539)

    def test_late_unknown_root_creates_no_extraction_output(self):
        expected=archive_gate.PUBLIC[0]; entries=archive_members(expected)
        entries[-1].filename='build/b3-host-tests/extra'
        buffer=io.BytesIO()
        with archive_gate.zipfile.ZipFile(buffer,'w') as zipped:
            for entry in entries: zipped.writestr(entry,b'')
        inner=SimpleNamespace(require=archive_require,read=lambda path,maximum:buffer.getvalue())
        with tempfile.TemporaryDirectory() as directory:
            destination=Path(directory)/'fresh'
            with self.assertRaisesRegex(ValueError,'Unsafe/unexpected public archive path'):
                archive_gate.extract(inner,Path('unused.zip'),destination,expected)
            self.assertFalse(destination.exists())

    def test_unsafe_paths_encryption_and_nonregular_modes_fail_preflight(self):
        expected=archive_gate.PUBLIC[0]
        cases=[('../escape',stat.S_IFREG,0),('/escape',stat.S_IFREG,0),
          ('Binaries/net8.0/../escape',stat.S_IFREG,0),('Binaries\\net8.0/escape',stat.S_IFREG,0),
          ('Binaries/net8.0//escape',stat.S_IFREG,0),('Binaries/net8.0/extra',stat.S_IFLNK,0),
          ('Binaries/net8.0/extra',stat.S_IFIFO,0),('Binaries/net8.0/extra',stat.S_IFREG,1)]
        for name,mode,flags in cases:
            with self.subTest(name=name,mode=mode,flags=flags):
                entries=archive_members(expected); entry=entries[-1]
                entry.filename=name; entry.external_attr=(mode|0o644)<<16; entry.flag_bits=flags
                with self.assertRaises(ValueError): self.preflight(entries,expected)

    def test_canonical_duplicates_and_file_ancestors_fail_preflight(self):
        expected=archive_gate.PUBLIC[0]
        entries=archive_members(expected)
        entries[-1].filename=entries[-2].filename+'/'
        entries[-1].external_attr=(stat.S_IFDIR|0o755)<<16
        with self.assertRaisesRegex(ValueError,'Duplicate canonical'):
            self.preflight(entries,expected)
        entries=archive_members(expected)
        entries[-1].filename=entries[-2].filename+'/child'
        with self.assertRaisesRegex(ValueError,'file is an ancestor'):
            self.preflight(entries,expected)

    def test_member_file_aggregate_and_path_bounds_fail_preflight(self):
        expected=archive_gate.PUBLIC[0]
        entries=archive_members(expected); entries.append(entries[-1])
        with self.assertRaisesRegex(ValueError,'member count'):
            self.preflight(entries,expected)
        entries=archive_members(expected); entries[-1].file_size=256*1024*1024+1
        with self.assertRaisesRegex(ValueError,'file mode/size bound'):
            self.preflight(entries,expected)
        entries=archive_members(expected)
        for entry in entries[:5]: entry.file_size=256*1024*1024
        with self.assertRaisesRegex(ValueError,'aggregate bound'):
            self.preflight(entries,expected)
        entries=archive_members(expected); entries[-1].filename='Binaries/net8.0/'+'x'*4096
        with self.assertRaisesRegex(ValueError,'path bound'):
            self.preflight(entries,expected)
        entries=archive_members(expected); entries[-1].filename='Binaries/net8.0/'+'/'.join(['x']*64)
        with self.assertRaisesRegex(ValueError,'path bound'):
            self.preflight(entries,expected)


if __name__ == '__main__': unittest.main()


# Five controls use only coordinator-owned FIFOs/data. The sixth runs one fixed
# disposable Python child (no verifier/worker/solver), with expected owned-pidfd
# timeout cleanup. Its poisoned receipt must contain zero residual children.
import hashlib
import json
import os
import resource
import sys
import time
from unittest import mock

sink_module=types.ModuleType('trace_sink_tested')
sink_module.__file__=str(Path(__file__).with_name('trace-sink.py'))
exec(compile(Path(sink_module.__file__).read_bytes(),sink_module.__file__,'exec'),sink_module.__dict__)
owned_module=types.ModuleType('trace_sink_owned_tested')
owned_module.__file__=str(Path(__file__).with_name('owned-process.py'))
exec(compile(Path(owned_module.__file__).read_bytes(),owned_module.__file__,'exec'),owned_module.__dict__)


class TraceSinkControls(unittest.TestCase):
    def test_fragmented_fifo_bytes_preserve_order_and_wait_for_owned_exit(self):
        with tempfile.TemporaryDirectory() as directory:
            sink=sink_module.TraceSink(Path(directory).resolve()/'trace',16)
            writer=os.open(sink.fifo,os.O_WRONLY|os.O_NONBLOCK)
            try:
                os.write(writer,b'ab');sink.pump()
                os.write(writer,b'cde');sink.pump()
            finally:os.close(writer)
            sink.pump()
            self.assertFalse(sink.complete(False,time.monotonic()+10))
            self.assertTrue(sink.complete(True,time.monotonic()+10))
            self.assertEqual(sink.path.read_bytes(),b'abcde')
            self.assertEqual(sink.receipt()['completeTraceSha256'],hashlib.sha256(b'abcde').hexdigest())
            self.assertFalse(sink.fifo.exists())
            sink.abort();sink.pump()
            self.assertTrue(sink.receipt()['complete'])

    def test_exact_byte_cap_requires_nonempty_eof_and_natural_completion(self):
        with tempfile.TemporaryDirectory() as directory:
            sink=sink_module.TraceSink(Path(directory).resolve()/'trace',8)
            writer=os.open(sink.fifo,os.O_WRONLY|os.O_NONBLOCK)
            try:
                os.write(writer,b'12345678');sink.pump()
                self.assertFalse(sink.complete(True,time.monotonic()+10))
            finally:os.close(writer)
            sink.pump()
            self.assertTrue(sink.complete(True,time.monotonic()+10))
            self.assertEqual(sink.receipt()['prefixBytes'],8)
            self.assertFalse(sink.receipt()['overflowObserved'])
            self.assertEqual(sink.receipt()['prefixSha256'],sink.receipt()['completeTraceSha256'])

    def test_one_byte_and_large_block_overflow_never_write_beyond_cap(self):
        for data in [b'123456789',b'x'*1024]:
            with self.subTest(bytes=len(data)),tempfile.TemporaryDirectory() as directory:
                sink=sink_module.TraceSink(Path(directory).resolve()/'trace',8)
                writer=os.open(sink.fifo,os.O_WRONLY|os.O_NONBLOCK)
                try:os.write(writer,data)
                finally:os.close(writer)
                with self.assertRaisesRegex(sink_module.SinkError,'byte cap'):sink.pump()
                first=sink.failure;sink.abort();sink.pump();sink.abort()
                self.assertEqual(sink.failure,first)
                self.assertEqual(sink.path.read_bytes(),data[:8])
                self.assertEqual(sink.receipt()['prefixBytes'],8)
                self.assertEqual(sink.receipt()['observedBytes'],9)
                self.assertTrue(sink.receipt()['overflowObserved'])
                self.assertFalse(sink.complete(True,time.monotonic()+10))
                self.assertIsNone(sink.receipt()['completeTraceSha256'])
                self.assertFalse(sink.fifo.exists())

    def test_empty_unconnected_and_expired_sinks_never_complete(self):
        with tempfile.TemporaryDirectory() as directory:
            sink=sink_module.TraceSink(Path(directory).resolve()/'trace',8)
            try:
                sink.pump();self.assertFalse(sink.complete(True,time.monotonic()+10))
                writer=os.open(sink.fifo,os.O_WRONLY|os.O_NONBLOCK)
                try:
                    os.write(writer,b'a');sink.pump()
                    self.assertFalse(sink.complete(True,time.monotonic()+10))
                finally:os.close(writer)
                sink.pump()
                with self.assertRaisesRegex(sink_module.SinkError,'deadline'):sink.complete(True,time.monotonic()-1)
                self.assertFalse(sink.receipt()['complete'])
            finally:sink.abort()

    def test_read_write_and_fifo_identity_faults_close_without_hiding_first_failure(self):
        for kind in ['read','write','identity']:
            with self.subTest(fault=kind),tempfile.TemporaryDirectory() as directory:
                sink=sink_module.TraceSink(Path(directory).resolve()/'trace',8)
                writer=os.open(sink.fifo,os.O_WRONLY|os.O_NONBLOCK)
                try:os.write(writer,b'a')
                finally:os.close(writer)
                if kind=='identity':
                    sink.fifo.unlink();os.mkfifo(sink.fifo,0o600)
                    context=mock.patch.object(sink_module.os,'read',wraps=os.read)
                else:context=mock.patch.object(sink_module.os,kind,side_effect=OSError('injected-'+kind))
                with context,self.assertRaises(sink_module.SinkError):sink.pump()
                first=sink.failure;sink.abort();sink.pump();sink.abort()
                self.assertEqual(sink.failure,first)
                self.assertTrue(sink.closed)
                self.assertIsNone(sink.reader);self.assertIsNone(sink.target)
                self.assertFalse(sink.complete(True,time.monotonic()+10))
                self.assertIsNone(sink.receipt()['completeTraceSha256'])

    def test_owned_nonverifier_writer_timeout_remains_poisoned_and_reaped(self):
        ownership=owned_module.enable_subreaper()
        before=resource.getrlimit(resource.RLIMIT_FSIZE)
        with tempfile.TemporaryDirectory() as directory:
            sink=sink_module.TraceSink(Path(directory).resolve()/'trace',64)
            # Fixed source-only fixture: write a small prefix, then hold the FIFO.
            # This child has no descendants and invokes no verifier or solver.
            command=[sys.executable,'-B','-c',
              'import os,sys,time;f=os.open(sys.argv[1],os.O_WRONLY);os.write(f,b"held");time.sleep(30)',str(sink.fifo)]
            try:
                with (Path(directory)/'child.log').open('xb') as log:
                    row=owned_module.run_owned(command,log,{},1,io_pump=sink.pump,io_complete=sink.complete,io_abort=sink.abort)
                self.assertFalse(row['passed']);self.assertTrue(row['poisoned'])
                self.assertFalse(row['ioCompleted']);self.assertGreaterEqual(row['signalCount'],1)
                self.assertEqual(row['remainingDirectChildren'],[])
                self.assertEqual(owned_module.child_ids(os.getpid()),[])
                self.assertTrue(all(x['exitObserved'] and x['reaped'] for x in row['ownedProcesses']))
                self.assertEqual(sink.receipt()['prefixBytes'],4)
                self.assertFalse(sink.receipt()['complete']);self.assertIsNone(sink.receipt()['completeTraceSha256'])
                self.assertEqual(resource.getrlimit(resource.RLIMIT_FSIZE),before)
                print('SINK_OWNED_NONVERIFIER_TIMEOUT='+json.dumps({'ownership':ownership,'stage':row,'sink':sink.receipt()},sort_keys=True),flush=True)
            finally:
                sink.abort()
                if owned_module.child_ids(os.getpid()):owned_module.drain_exclusive_children(10)


def hex_fd(fd,label):
    # Actual strace6.8 -xx -yy annotation spelling; no mixed plain/hex fragments.
    return str(fd)+'<'+''.join('\\x'+f'{x:02x}' for x in label.encode())+'>'


def hex_pipe(fd,inode):
    return hex_fd(fd,'pipe:['+str(inode)+']')


def complete_hex_pipe_fixture():
    trace=exec_line(10,'/dotnet',['/dotnet','/replay.dll'])
    def pipe(pid,left,right,inode):
        return f'{pid} 1.0 pipe2([{hex_pipe(left,inode)}, {hex_pipe(right,inode)}], O_CLOEXEC) = 0\n'
    def duplicate(pid,old,new,inode):
        return f'{pid} 1.0 dup2({hex_pipe(old,inode)}, {new}) = {new}\n'
    def io_call(pid,name,fd,inode,data):
        text=f'{pid} 1.0 {name}({hex_pipe(fd,inode)}, {quoted(data.decode())}, {len(data)}) = {len(data)}\n'
        return text+''.join(dump(data[i:i+16],i) for i in range(0,len(data),16))
    trace+=pipe(10,3,4,1)+pipe(10,5,6,2)
    trace+='10 1.0 vfork() = 20\n'+exec_line(20,'/dotnet',['/dotnet','/worker.dll'])
    trace+=duplicate(20,3,0,1)+duplicate(20,6,1,2)
    request=b'{"requestId":"r"}'
    trace+=io_call(20,'read',0,1,request+b'\n')
    trace+=pipe(20,7,8,3)+pipe(20,9,10,4)
    trace+='20 1.0 vfork() = 30\n'+exec_line(30,'/solver',['/solver','-in','-smt2'])
    trace+=duplicate(30,7,0,3)+duplicate(30,10,1,4)
    commands=b'(set-option :smt.arith.solver 2)\n(set-option :rlimit 200000)\n(set-option :timeout 20000)\n(check-sat)\n'
    responses=b'success\nsuccess\nsuccess\nunsat\n'
    trace+=io_call(30,'read',0,3,commands)+io_call(30,'write',1,4,responses)
    trace+=f'30 1.0 read({hex_pipe(0,3)}, "", 8) = 0\n'
    completion={'attempts':[{'obligationId':'sCheck','outcome':'Verified'}]}
    output=(json.dumps({'version':2,'requestId':'r','sequence':0,'processId':20,'isolatedProcessGroup':True})+'\n'+json.dumps(completion)+'\n').encode()
    trace+=io_call(20,'write',1,2,output)
    ownership=minimal_exec_trace()[1]
    images=[{'identity':ownership['ownedProcesses'][-1]['identity'],'sha256':'f'*64,'argv':['/solver','-in','-smt2']}]
    return trace.encode(),ownership,images,request,completion


class HexFdAnnotationControls(unittest.TestCase):
    def test_observed_all_hex_fd_annotations_decode_exact_pipe_ends(self):
        # This literal was retained in run37242135132, including its inode.
        observed=r'3<\x70\x69\x70\x65\x3a\x5b\x31\x37\x36\x32\x38\x5d>'
        self.assertEqual(capture.descriptor(observed),(3,17628))
        self.assertEqual(capture.pipe_ends('['+observed+', '+hex_pipe(4,17628)+']'),[(3,17628),(4,17628)])
        self.assertIsNone(capture.descriptor(hex_fd(3,'/regular/file')))
        self.assertEqual(capture.descriptor('3<pipe:[17628]>'),(3,17628))

    def test_mixed_truncated_suffix_and_annotation_bounds_are_rejected(self):
        bad=[r'3<pipe:\x5b2]>',r'3<\x70\x6>',hex_pipe(3,2)+'suffix',
             hex_pipe(3,2)[:-1],hex_fd(3,'pipe:[2]suffix'),hex_fd(3,'pipe:[0]'),
             hex_fd(3,'pipe:[18446744073709551616]'),hex_fd(3,'x'*(capture.MAX_FD_LABEL_BYTES+1))]
        for value in bad:
            with self.subTest(label=value[:80]),self.assertRaises(ValueError):capture.descriptor(value)
        with self.assertRaises(ValueError):
            capture.pipe_ends('['+hex_pipe(3,2)+', 4<pipe:[2]>]')

    def test_pipe_creation_requires_two_distinct_fds_with_one_complete_inode(self):
        bad=['['+hex_pipe(3,2)+']','['+hex_pipe(3,2)+', '+hex_pipe(4,3)+']',
             '['+hex_pipe(3,2)+', '+hex_pipe(3,2)+']',
             '['+hex_pipe(3,2)+', '+hex_pipe(4,2)+', '+hex_pipe(5,2)+']',
             '['+hex_pipe(3,2)+', '+hex_fd(4,'/regular/file')+']',
             '['+hex_pipe(3,2)+', '+hex_pipe(4,2)+']suffix']
        for value in bad:
            with self.subTest(ends=value[:80]),self.assertRaises(ValueError):capture.pipe_ends(value)

    def test_full_hex_fd_fixture_retains_owner_inheritance_and_wrong_inode_failures(self):
        trace,ownership,images,request,completion=complete_hex_pipe_fixture()
        def analyze(data,owned):
            return capture.analyze(data,owned,images,'/solver','f'*64,['sCheck'],'/worker.dll','/dotnet',request,completion,['/dotnet','/replay.dll'])
        result=analyze(trace,ownership)
        self.assertTrue(result['captureComplete'])
        self.assertEqual(result['emittedCheckQueries'],[{'obligationId':'sCheck','commandIndex':3,'response':'unsat','attemptOutcome':'Verified'}])
        self.assertEqual(result['workerInputSha256'],hashlib.sha256(request+b'\n').hexdigest())
        wrong=trace.replace(('read('+hex_pipe(0,3)).encode(),('read('+hex_pipe(0,99)).encode(),1)
        with self.assertRaisesRegex(ValueError,'inherited pipe topology'):analyze(wrong,ownership)
        other=json.loads(json.dumps(ownership));other['ownedProcesses'][-1]['identity']['parent']=10
        with self.assertRaisesRegex(ValueError,'pinned process parent'):analyze(trace,other)
