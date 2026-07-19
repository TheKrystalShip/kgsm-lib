using System.Runtime.InteropServices;

namespace TheKrystalShip.KGSM.Native;

/// <summary>The Unix file-type of a path, as <c>lstat(2)</c> sees it (i.e. WITHOUT following a final
/// symlink — a symlink reports <see cref="Symlink"/>, never its target's type). Internal: the public
/// DTO shape a listing exposes is <see cref="Core.Models.FileKind"/> (which has no <see cref="Missing"/>
/// case — a missing path is a <see cref="Core.Models.FileOpOutcome"/>, not a listed entry).</summary>
internal enum LstatKind
{
    /// <summary>The path does not exist (lstat <c>ENOENT</c>, or any path-component error).</summary>
    Missing,

    /// <summary>A regular file — the only kind <see cref="Services.InstanceFiles"/> opens for read/write.</summary>
    Regular,

    /// <summary>A directory.</summary>
    Directory,

    /// <summary>A symbolic link (not followed here — jail containment resolves symlink targets
    /// separately via <c>FileSystemInfo.LinkTarget</c>).</summary>
    Symlink,

    /// <summary>Anything else — FIFO, socket, char/block device. Never opened.</summary>
    Special,
}

/// <summary>
/// A minimal <c>lstat(2)</c>/<c>stat(2)</c> wrapper — kgsm-lib's first P/Invoke, and the honest
/// file-type oracle the instance-file jail depends on. .NET exposes no managed API for the Unix file-type
/// bits (a socket/FIFO is indistinguishable from a regular file through <c>File.Exists</c>/<c>FileInfo</c>),
/// so <c>st_mode</c> is read directly.
/// </summary>
/// <remarks>
/// Native-AOT-safe: <c>[LibraryImport]</c> (source-generated at compile time, no reflection, no
/// runtime IL emission) rather than <c>DllImport</c>. <c>&lt;AllowUnsafeBlocks&gt;</c> IS enabled on
/// this project for this file alone — confirmed empirically that the LibraryImport source generator
/// unconditionally emits its P/Invoke stub as an <c>unsafe extern</c> local function, even for a
/// zero-parameter signature, so there is no parameter shape that avoids the requirement. This is
/// orthogonal to AOT-safety (unsafe/pointer code is fully Native-AOT-compatible; it is reflection and
/// dynamic codegen that AOT forbids), and no hand-written code in this file uses a pointer — only the
/// generator's boilerplate needs the block. The struct layout read is the stable Linux x86-64 glibc
/// <c>struct stat</c> (144 bytes total, <c>st_mode</c> at offset 24) — the same layout kgsm-api's
/// JIT-only <c>PosixFile</c> reads via a raw byte buffer; declaring <see cref="StatBuf"/> with
/// <see cref="LayoutKind.Explicit"/> and a 144-byte <c>Size</c> makes the blittable marshaller copy the
/// full native struct while only <see cref="StatBuf.StMode"/> is projected into managed code. On any
/// other platform/architecture (or if libc can't be resolved), the native call is skipped in favour of a
/// managed best-effort classification — a non-Linux-x64 dev box can't tell a socket from a regular file;
/// the deploy target is always Linux x64.
/// </remarks>
internal static partial class LibC
{
    private const int StModeOffset = 24; // offsetof(struct stat, st_mode), Linux x86-64 glibc
    private const int StatBufSize = 144;  // sizeof(struct stat), Linux x86-64 glibc

    // S_IFMT mask + the type values (sys/stat.h). mode_t is 32-bit.
    private const uint S_IFMT = 0xF000;
    private const uint S_IFREG = 0x8000;
    private const uint S_IFDIR = 0x4000;
    private const uint S_IFLNK = 0xA000;

    private static readonly bool _native = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
        && RuntimeInformation.ProcessArchitecture == Architecture.X64;

    [StructLayout(LayoutKind.Explicit, Size = StatBufSize)]
    private struct StatBuf
    {
        [FieldOffset(StModeOffset)]
        public uint StMode;
    }

    [LibraryImport("libc", EntryPoint = "lstat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int NativeLstat(string path, out StatBuf buf);

    [LibraryImport("libc", EntryPoint = "stat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int NativeStat(string path, out StatBuf buf);

    /// <summary>Classifies <paramref name="path"/> by its own (un-followed) type — the <c>lstat</c> view,
    /// which is what the jail's file-type gate needs (a symlink must be seen as itself, not its target).</summary>
    public static LstatKind Lstat(string path) => Classify(path, NativeLstat);

    /// <summary>Classifies <paramref name="path"/> FOLLOWING a final symlink — the <c>stat</c> view.
    /// Not used by the jail today (every containment decision needs the un-followed view), kept for
    /// parity with the ported authority and any future caller that needs the followed type.</summary>
    public static LstatKind Stat(string path) => Classify(path, NativeStat);

    private delegate int NativeCall(string path, out StatBuf buf);

    private static LstatKind Classify(string path, NativeCall call)
    {
        if (_native)
        {
            try
            {
                if (call(path, out StatBuf buf) != 0)
                    return LstatKind.Missing; // ENOENT (or EACCES on a path component) — treat as not-present
                uint type = buf.StMode & S_IFMT;
                return type switch
                {
                    S_IFREG => LstatKind.Regular,
                    S_IFDIR => LstatKind.Directory,
                    S_IFLNK => LstatKind.Symlink,
                    _ => LstatKind.Special, // FIFO / socket / device — never openable
                };
            }
            catch (DllNotFoundException) { /* fall through to managed */ }
            catch (EntryPointNotFoundException) { /* fall through to managed */ }
        }

        // Managed best-effort (non-Linux-x64 only). Cannot distinguish a socket/FIFO from a regular
        // file, so this path is for dev boxes; the deploy target always takes the native branch.
        try
        {
            string? link = new FileInfo(path).LinkTarget;
            if (link is not null) return LstatKind.Symlink;
        }
        catch { /* ignore */ }
        if (Directory.Exists(path)) return LstatKind.Directory;
        if (File.Exists(path)) return LstatKind.Regular;
        return LstatKind.Missing;
    }
}
