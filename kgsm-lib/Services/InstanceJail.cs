namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// The path-containment jail shared by every service that does direct <c>System.IO</c> inside an
/// instance's directories — <see cref="InstanceFiles"/> (rooted at the working dir) and
/// <see cref="InstanceBackups"/> (rooted at the backups dir).
/// </summary>
/// <remarks>
/// This lives in one place on purpose. Two jails that are "the same rules, written twice" drift, and a
/// drifted containment check is not a weaker guarantee — it is no guarantee, because the weaker of the
/// two is the one an attacker picks. The roots differ per service; the containment rule must not.
/// <para>
/// The rule: canonicalise the root and the candidate the same way — resolving <c>.</c>/<c>..</c> AND
/// symlinks at EVERY path component, not just the leaf, following chains up to
/// <see cref="MaxSymlinkHops"/> — then require the result to be the root itself or a
/// <c>root + "/"</c>-prefixed descendant, compared ordinally. A component that does not exist is
/// accepted verbatim, so a create/rename target still resolves.
/// </para>
/// </remarks>
internal static class InstanceJail
{
    /// <summary>Symlink-loop guard: the maximum number of links one resolution may follow.</summary>
    internal const int MaxSymlinkHops = 64;

    /// <summary>
    /// Resolves <paramref name="relativePath"/> against <paramref name="root"/> and reports whether the
    /// canonicalised result stays inside the jail.
    /// </summary>
    /// <param name="root">The jail root — already canonicalised via <see cref="CanonicalRealPath"/>.</param>
    /// <param name="relativePath">The candidate, relative to the root; null/empty resolves to the root.</param>
    /// <param name="realTarget">The canonicalised absolute path, when contained.</param>
    /// <param name="normRel">The normalised relative path, for display/echo.</param>
    /// <returns><c>true</c> when the target is the root or a descendant of it.</returns>
    internal static bool TryResolve(string root, string? relativePath, out string realTarget, out string normRel)
    {
        realTarget = "";
        normRel = "";

        string rel = (relativePath ?? "").Trim().Replace('\\', '/').Trim('/');
        if (rel.IndexOf('\0') >= 0) return false; // NUL byte — never a legitimate path

        string lexical = Path.GetFullPath(rel.Length == 0 ? root : Path.Combine(root, rel));
        string real;
        try { real = CanonicalRealPath(lexical); }
        catch (IOException) { return false; } // symlink loop or similar — refuse rather than hang/throw

        bool contained = string.Equals(real, root, StringComparison.Ordinal)
            || real.StartsWith(root + "/", StringComparison.Ordinal);
        if (!contained) { normRel = rel; return false; }

        realTarget = real;
        string display = Path.GetRelativePath(root, lexical);
        normRel = display is "." or "" ? "" : display.Replace('\\', '/');
        return true;
    }

    /// <summary>
    /// Canonicalises an absolute path the way POSIX <c>realpath</c> does — resolving <c>.</c>, <c>..</c>
    /// and symlinks at every component.
    /// </summary>
    /// <param name="absolutePath">The absolute path to canonicalise.</param>
    /// <returns>The fully resolved absolute path.</returns>
    /// <exception cref="IOException">Thrown when the symlink chain exceeds <see cref="MaxSymlinkHops"/>.</exception>
    internal static string CanonicalRealPath(string absolutePath)
    {
        var todo = new LinkedList<string>();
        foreach (string p in absolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
            todo.AddLast(p);

        var resolved = new List<string>();
        int hops = 0;
        while (todo.First is { } node)
        {
            todo.RemoveFirst();
            string comp = node.Value;
            if (comp == ".") continue;
            if (comp == "..")
            {
                if (resolved.Count > 0) resolved.RemoveAt(resolved.Count - 1);
                continue;
            }

            string current = resolved.Count == 0 ? "/" + comp : "/" + string.Join('/', resolved) + "/" + comp;
            string? link;
            try { link = new FileInfo(current).LinkTarget; }
            catch { link = null; }

            if (link is null)
            {
                resolved.Add(comp); // not a symlink (or doesn't exist) → accept verbatim
                continue;
            }

            if (++hops > MaxSymlinkHops)
                throw new IOException("symlink chain too long (possible loop)");

            // Expand: an absolute target restarts from root; a relative target is relative to the
            // link's PARENT directory (= the current `resolved`, since `comp` was not pushed). Prepend
            // the target's components to the work queue so they resolve against that parent.
            string[] parts = link.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (Path.IsPathRooted(link)) resolved.Clear();
            for (int i = parts.Length - 1; i >= 0; i--) todo.AddFirst(parts[i]);
        }

        return "/" + string.Join('/', resolved);
    }
}
