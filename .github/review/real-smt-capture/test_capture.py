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
            capture.analyze(trace,ownership,[], '/solver','f'*64,['sCheck'],'/worker.dll','/dotnet',b'{}',{})

    def test_detached_fork_parent_is_rejected(self):
        trace,ownership = minimal_exec_trace()
        ownership['ownedProcesses'][-1]['identity']['parent'] = 10
        with self.assertRaisesRegex(ValueError,'pinned process parent'):
            capture.analyze(trace,ownership,[], '/solver','f'*64,['sCheck'],'/worker.dll','/dotnet',b'{}',{})

    def test_reused_owned_numeric_pid_is_rejected(self):
        trace,ownership = minimal_exec_trace()
        ownership['ownedProcesses'].append({'identity':{'pid':30,'parent':20,'startTime':99}})
        with self.assertRaisesRegex(ValueError,'numeric identity reuse'):
            capture.analyze(trace,ownership,[], '/solver','f'*64,['sCheck'],'/worker.dll','/dotnet',b'{}',{})

    def test_incomplete_smt_string_is_rejected(self):
        with self.assertRaises(ValueError): capture.forms(b'(error "truncated)')


def minimal_exec_trace():
    def quoted(value): return '"'+''.join('\\x'+f'{x:02x}' for x in value.encode())+'"'
    def exec_line(pid,path,argv):
        return f'{pid} 1.0 execve({quoted(path)}, ['+', '.join(quoted(x) for x in argv)+'], 0x123 /* 8 vars */) = 0\n'
    trace=exec_line(10,'/dotnet',['/dotnet','/replay.dll'])
    trace+='10 1.0 vfork() = 20\n'+exec_line(20,'/dotnet',['/dotnet','/worker.dll'])
    trace+='20 1.0 vfork() = 30\n'+exec_line(30,'/solver',['/solver','-in','-smt2'])
    ownership={'ownedProcesses':[{'identity':{'pid':pid,'parent':parent,'startTime':pid},'isRoot':pid==100}
      for pid,parent in [(100,1),(10,100),(20,10),(30,20)]]}
    return trace.encode(),ownership


if __name__ == '__main__': unittest.main()
