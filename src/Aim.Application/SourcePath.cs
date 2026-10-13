namespace Aim.Application;

public static class SourcePath
{
    public static StringComparer Comparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    public static string Validate(string root, string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || relative.Length > 500 || relative.Contains('\\') || relative.Contains(':') ||
            Path.IsPathRooted(relative) || !relative.EndsWith(".ts", StringComparison.Ordinal))
            throw new InputException("Informe um caminho relativo .ts usando barras /.");
        var parts = relative.Split('/');
        string[] denied = ["node_modules", "dist", "bin", "obj", "secrets"];
        if (parts.Any(p => p.Length == 0 || p.StartsWith('.') || p.EndsWith('.') || p.EndsWith(' ') || p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || denied.Contains(p, StringComparer.OrdinalIgnoreCase)))
            throw new InputException("Caminho de fonte não permitido.");
        var current = Path.GetFullPath(root);
        for (var parent = new DirectoryInfo(current); parent is not null; parent = parent.Parent)
            if ((parent.Attributes & FileAttributes.ReparsePoint) != 0) throw new InputException("Links de diretório não são permitidos.");
        foreach (var part in parts)
        {
            var directory = current;
            current = Path.Combine(current, part);
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InputException("Links de arquivo não são permitidos.");
            if (OperatingSystem.IsWindows() && Directory.Exists(directory))
            {
                // Only enumerate names in this path's parent, never recursively read sources.
                var matches = Directory.EnumerateFileSystemEntries(directory)
                    .Where(entry => Comparer.Equals(Path.GetFileName(entry), part)).Take(2).ToArray();
                if (matches.Length > 1) throw new InputException("Diretórios Windows com nomes distintos apenas por casing não são suportados.");
                if (matches.Length == 1) current = matches[0];
            }
        }
        if (File.Exists(current) && new FileInfo(current).Length > 1024 * 1024) throw new InputException("Fonte excede o limite de 1 MiB.");
        return Path.GetRelativePath(root, current).Replace('\\', '/');
    }
}
