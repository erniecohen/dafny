"""Bounded single-threaded FIFO sink for the fixed diagnostic tracer.

The reader is never inherited by a child. No process-wide resource limit changes.
Incomplete prefixes remain data only, never complete trace evidence.
"""
import hashlib
import os
from pathlib import Path
import stat
import time

MAX_TRACE = 64 * 1024 * 1024
CHUNK = 65536
PUMP_BYTES = 4 * CHUNK


class SinkError(ValueError):
    pass


class TraceSink:
    def __init__(self, path, maximum=MAX_TRACE):
        if not isinstance(maximum,int) or not 0 < maximum <= MAX_TRACE:
            raise SinkError('Trace sink cap is invalid')
        self.path = Path(path)
        self.fifo = self.path.with_name(self.path.name + '.fifo')
        self.maximum = maximum
        self.reader = self.target = None
        self.fifo_identity = self.target_identity = None
        self.original_fifo_identity = None
        self.prefix_bytes = self.observed_bytes = 0
        self.digest = hashlib.sha256()
        self.eof = self.complete_trace = self.overflow = self.closed = False
        self.failure = None
        self.secondary_failures = []
        self.completed_at = None
        self.eof_after_owned_exit = False
        self.removal_attempted = False
        try:
            if self.path.parent.resolve(strict=True) != self.path.parent:
                raise SinkError('Trace sink directory traverses a symlink')
            os.mkfifo(self.fifo,0o600)
            info = self.fifo.lstat()
            if not stat.S_ISFIFO(info.st_mode) or stat.S_IMODE(info.st_mode) != 0o600:
                raise SinkError('Trace sink FIFO is not private')
            self.fifo_identity = (info.st_dev,info.st_ino)
            self.original_fifo_identity = self.fifo_identity
            self.reader = os.open(self.fifo,os.O_RDONLY|os.O_NONBLOCK|os.O_NOFOLLOW|os.O_CLOEXEC)
            self.target = os.open(self.path,os.O_WRONLY|os.O_CREAT|os.O_EXCL|os.O_NOFOLLOW|os.O_CLOEXEC,0o600)
            info = os.fstat(self.target)
            if not stat.S_ISREG(info.st_mode): raise SinkError('Trace sink target is not regular')
            self.target_identity = (info.st_dev,info.st_ino)
            self.check_identity()
        except BaseException as error:
            self.fail(error)
            raise SinkError(self.failure) from error

    def fail(self, error):
        message = type(error).__name__ + ': ' + str(error)
        if self.failure is None: self.failure = message[:4096]
        elif len(self.secondary_failures) < 8 and message[:4096] not in self.secondary_failures:
            self.secondary_failures.append(message[:4096])
        self.abort()

    def check_identity(self):
        if self.failure is not None or self.closed: return
        for fd,path,pin,kind in [(self.reader,self.fifo,self.fifo_identity,stat.S_IFIFO),
                                 (self.target,self.path,self.target_identity,stat.S_IFREG)]:
            current = path.lstat(); opened = os.fstat(fd)
            if (current.st_dev,current.st_ino) != pin or (opened.st_dev,opened.st_ino) != pin or \
               stat.S_IFMT(current.st_mode) != kind or stat.S_IFMT(opened.st_mode) != kind:
                raise SinkError('Trace sink identity changed')
        if os.fstat(self.target).st_size != self.prefix_bytes:
            raise SinkError('Trace sink retained prefix size changed')

    def pump(self):
        # Idempotent after a fault: the first exception remains the primary failure,
        # while cleanup continues without repeating a broken callback indefinitely.
        if self.failure is not None or self.closed: return
        try:
            self.check_identity()
            remaining_work = PUMP_BYTES
            while remaining_work:
                amount = min(CHUNK,remaining_work,self.maximum-self.prefix_bytes+1)
                try: block = os.read(self.reader,amount)
                except BlockingIOError:
                    self.eof = False
                    break
                if not block:
                    # Before any writer connects this can be zero too. Completion
                    # separately requires nonempty bytes and every owned process gone.
                    self.eof = True
                    break
                self.eof = False
                self.observed_bytes += len(block)
                remaining_work -= len(block)
                retained = block[:self.maximum-self.prefix_bytes]
                offset = 0
                while offset < len(retained):
                    written = os.write(self.target,retained[offset:])
                    if not 0 < written <= len(retained)-offset:
                        raise SinkError('Trace sink write did not progress')
                    self.digest.update(retained[offset:offset+written])
                    self.prefix_bytes += written; offset += written
                if len(retained) != len(block):
                    self.overflow = True
                    raise SinkError('Trace sink exceeded its byte cap')
        except BaseException as error:
            self.fail(error)
            raise SinkError(self.failure) from error

    def complete(self, owned_done, deadline):
        if self.failure is not None: return False
        if self.complete_trace: return True
        if not owned_done: return False
        try:
            if time.monotonic() >= deadline: raise SinkError('Trace sink completion exceeded stage deadline')
            self.pump()
            if not self.eof or self.prefix_bytes == 0: return False
            self.eof_after_owned_exit = True
            self.check_identity()
            os.fsync(self.target)
            # Validate the retained regular file against the rolling prefix before
            # attaching a complete hash. This is not an atomic payload attestation.
            h = hashlib.sha256(); size = 0
            fd = os.open(self.path,os.O_RDONLY|os.O_NOFOLLOW|os.O_NONBLOCK|os.O_CLOEXEC)
            try:
                info = os.fstat(fd)
                if (info.st_dev,info.st_ino) != self.target_identity: raise SinkError('Trace sink changed at final read')
                while True:
                    block = os.read(fd,min(CHUNK,self.maximum+1-size))
                    if not block: break
                    size += len(block)
                    if size > self.maximum: raise SinkError('Trace sink final read exceeded cap')
                    h.update(block)
            finally: os.close(fd)
            if size != self.prefix_bytes or h.hexdigest() != self.digest.hexdigest():
                raise SinkError('Trace sink retained bytes differ from rolling prefix')
            self.abort()
            if self.failure is not None: return False
            self.completed_at = time.monotonic()
            if self.completed_at >= deadline: raise SinkError('Trace sink completion exceeded stage deadline')
            self.complete_trace = True
            return True
        except BaseException as error:
            self.fail(error)
            raise SinkError(self.failure) from error

    def abort(self):
        # This closes only coordinator-owned descriptors and its pinned FIFO.
        # It never signals a process; the owned pidfd supervisor handles failures.
        self.closed = True
        for name in ('reader','target'):
            fd = getattr(self,name,None); setattr(self,name,None)
            if fd is not None:
                try: os.close(fd)
                except OSError as error:
                    if self.failure is None: self.failure = 'Sink close: '+str(error)[:4096]
                    elif len(self.secondary_failures) < 8: self.secondary_failures.append('Sink close: '+str(error)[:4096])
        if self.fifo_identity is not None and not self.removal_attempted:
            self.removal_attempted = True
            try:
                info = self.fifo.lstat()
                if (info.st_dev,info.st_ino) != self.fifo_identity or not stat.S_ISFIFO(info.st_mode):
                    raise SinkError('Trace sink FIFO changed before removal')
                self.fifo.unlink()
                self.fifo_identity = None
            except FileNotFoundError:
                if self.failure is None: self.failure = 'Trace sink FIFO disappeared before removal'
            except OSError as error:
                if self.failure is None: self.failure = 'Sink removal: '+str(error)[:4096]
                elif len(self.secondary_failures) < 8: self.secondary_failures.append('Sink removal: '+str(error)[:4096])
            except SinkError as error:
                if self.failure is None: self.failure = str(error)
                elif len(self.secondary_failures) < 8: self.secondary_failures.append(str(error))

    def receipt(self):
        return {'transport':'coordinator-owned-private-fifo','maximumBytes':self.maximum,
          'fifoDeviceAndInode':list(self.original_fifo_identity) if self.original_fifo_identity is not None else None,
          'fifoModeOctal':'0600','readerCloseOnExec':True,
          'prefixBytes':self.prefix_bytes,'prefixSha256':self.digest.hexdigest(),'observedBytes':self.observed_bytes,
          'nonempty':self.prefix_bytes>0,'eofObserved':self.eof,'eofObservedAfterOwnedExitAndReaping':self.eof_after_owned_exit,'overflowObserved':self.overflow,
          'complete':self.complete_trace,'completeTraceSha256':self.digest.hexdigest() if self.complete_trace else None,
          'completedAtMonotonic':self.completed_at,'closed':self.closed,'failure':self.failure,
          'secondaryFailures':list(self.secondary_failures),'globalFileLimitChanged':False}
