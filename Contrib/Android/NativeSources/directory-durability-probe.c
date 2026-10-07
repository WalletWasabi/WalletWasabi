/* ARM64 ABI diagnostic, never linked into the wallet or installed as an APK.
 * Uses only a fresh, synthetic directory under /data/local/tmp. It verifies
 * libc operations, not the managed wallet or biometric behavior.
 * Build with an Android ARM64 compiler and libc, -nostdlib -fno-stack-protector
 * -Wl,-e,_start -Wl,-dynamic-linker,/system/bin/linker64.
 * POSIX declarations keep this diagnostic independent of host fcntl headers.
 */
#if !defined(__aarch64__)
#error This diagnostic reproduces the ARM64 open-flag mismatch.
#endif
typedef void Directory;
extern Directory *opendir(const char *);
extern int dirfd(Directory *);
extern int closedir(Directory *);
extern int fsync(int);
extern int open(const char *, int, ...);
extern int close(int);
extern int mkdir(const char *, unsigned int);
extern int rmdir(const char *);
extern int getpid(void);
extern int *__errno(void);
extern int snprintf(char *, unsigned long, const char *, ...);
extern int dprintf(int, const char *, ...);
extern void _exit(int) __attribute__((noreturn));

void _start(void)
{
    char path[160];
    snprintf(path, sizeof(path), "/data/local/tmp/wasabi-directory-probe-%d", getpid());
    if (mkdir(path, 0700) != 0) _exit(1);
    int old = open(path, 0x10000);
    int error = *__errno();
    if (old >= 0) { close(old); rmdir(path); _exit(2); }
    dprintf(1, "ARM64 old open flag: errno=%d (expected EINVAL=22)\n", error);
    if (error != 22) { rmdir(path); _exit(3); }
    for (int i = 0; i < 64; i++) {
        Directory *stream = opendir(path);
        if (!stream) { rmdir(path); _exit(4); }
        int descriptor = dirfd(stream);
        int flushed = descriptor >= 0 ? fsync(descriptor) : -1;
        int closed = closedir(stream);
        if (flushed != 0 || closed != 0) { rmdir(path); _exit(5); }
    }
    if (rmdir(path) != 0) _exit(6);
    dprintf(1, "PASS: ARM64 opendir/dirfd/fsync/closedir, 64 fresh handles; synthetic directory removed\n");
    _exit(0);
}
