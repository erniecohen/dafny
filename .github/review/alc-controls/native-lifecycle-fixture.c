#define _POSIX_C_SOURCE 200809L
#include <errno.h>
#include <signal.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <sys/types.h>
#include <sys/wait.h>
#include <time.h>
#include <unistd.h>

/* CI-only fixed native ELF fixture. No shell or arbitrary executable mode. */
static int write_fragments(int fd, const unsigned char *data, size_t size) {
  while (size != 0) {
    size_t fragment = size < 37 ? size : 37;
    ssize_t count = write(fd, data, fragment);
    if (count < 0 && errno == EINTR) { continue; }
    if (count <= 0) { return -1; }
    data += count;
    size -= (size_t)count;
  }
  return 0;
}
static int pattern(int fd, size_t size, unsigned int factor, unsigned int offset) {
  unsigned char block[4093];
  for (size_t index = 0; index < size;) {
    size_t count = size - index < sizeof(block) ? size - index : sizeof(block);
    for (size_t at = 0; at < count; at++) { block[at] = (unsigned char)((index + at) * factor + offset); }
    if (write_fragments(fd, block, count) != 0) { return -1; }
    index += count;
  }
  return 0;
}
static void hold(void) { for (;;) { pause(); } }

int main(int argc, char **argv) {
  if (argc < 2) { return 2; }
  if (strcmp(argv[1], "seal-check") == 0 && argc == 6) {
    char *python[] = {"/usr/bin/python3", "-I", argv[2], argv[3], argv[4], argv[5], NULL};
    execv(python[0], python);
    return 3;
  }
  if (argc != 2) { return 2; }
  if (strcmp(argv[1], "version") == 0) {
    return write_fragments(1, (const unsigned char *)"NativeLifecycleFixture 1\n", 25) == 0 ? 0 : 3;
  }
  if (strcmp(argv[1], "streams") == 0) {
    if (pattern(2, 196613, 37, 11) != 0) { return 3; }
    unsigned char input[4093];
    for (;;) {
      ssize_t count = read(0, input, sizeof(input));
      if (count < 0 && errno == EINTR) { continue; }
      if (count < 0) { return 3; }
      if (count == 0) { break; }
      if (write_fragments(1, input, (size_t)count) != 0) { return 3; }
    }
    return pattern(1, 4097, 17, 19) == 0 ? 0 : 3;
  }
  if (strcmp(argv[1], "close-input") == 0) {
    close(0);
    if (write_fragments(1, (const unsigned char *)"input-closed\n", 13) != 0 ||
        write_fragments(2, (const unsigned char *)"fixture-stderr-on-stdin-close\n", 30) != 0) { return 3; }
    struct timespec pause_time = {0, 300000000};
    while (nanosleep(&pause_time, &pause_time) != 0 && errno == EINTR) { }
    return 0;
  }
  if (strcmp(argv[1], "reparented") == 0) {
    int identity_pipe[2];
    if (pipe(identity_pipe) != 0) { return 3; }
    pid_t child = fork();
    if (child < 0) { return 3; }
    if (child == 0) {
      close(identity_pipe[0]);
      pid_t descendant = fork();
      if (descendant < 0) { _exit(3); }
      if (descendant == 0) {
        close(identity_pipe[1]);
        if (setsid() < 0) { _exit(3); }
        hold();
      }
      ssize_t sent = write(identity_pipe[1], &descendant, sizeof(descendant));
      _exit(sent == (ssize_t)sizeof(descendant) ? 0 : 3);
    }
    close(identity_pipe[1]);
    pid_t descendant = 0;
    ssize_t got = read(identity_pipe[0], &descendant, sizeof(descendant));
    close(identity_pipe[0]);
    int status = 0;
    if (waitpid(child, &status, 0) != child || got != (ssize_t)sizeof(descendant) ||
        !WIFEXITED(status) || WEXITSTATUS(status) != 0) { return 3; }
    char output[96];
    int count = snprintf(output, sizeof(output), "descendant %ld\n", (long)descendant);
    if (count <= 0 || count >= (int)sizeof(output) || write_fragments(1, (unsigned char *)output, (size_t)count) != 0) { return 3; }
    hold();
  }
  if (strcmp(argv[1], "cap") == 0) {
    /* The trusted caller must configure pids.max=300; never exceed257 forks. */
    for (int index = 0; index < 257; index++) {
      pid_t child = fork();
      if (child < 0) {
        const unsigned char short_cap[] = "cap-short\n";
        (void)write_fragments(1, short_cap, sizeof(short_cap) - 1);
        return 3;
      }
      if (child == 0) { hold(); }
    }
    const unsigned char ready[] = "cap-ready 257\n";
    if (write_fragments(1, ready, sizeof(ready) - 1) != 0) { return 3; }
    hold();
  }
  return 2;
}
