namespace MpcplBuilder.Infrastructure.Files;

internal sealed class DirectoryTraversal
{
    private readonly Func<string, IEnumerable<string>> _enumerateDirectories;
    private readonly Func<string, IEnumerable<string>> _enumerateFiles;

    public DirectoryTraversal()
        : this(Directory.EnumerateDirectories, Directory.EnumerateFiles)
    {
    }

    internal DirectoryTraversal(
        Func<string, IEnumerable<string>> enumerateDirectories,
        Func<string, IEnumerable<string>> enumerateFiles)
    {
        _enumerateDirectories = enumerateDirectories;
        _enumerateFiles = enumerateFiles;
    }

    public IReadOnlyList<string> EnumerateFiles(string rootPath, CancellationToken cancellationToken)
    {
        var files = new List<string>();
        var pending = new Stack<(string Path, bool IsRoot)>();
        pending.Push((rootPath, true));

        while (pending.TryPop(out var item))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                foreach (var file in _enumerateFiles(item.Path))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    files.Add(file);
                }
                foreach (var child in _enumerateDirectories(item.Path))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    pending.Push((child, false));
                }
            }
            catch (Exception exception) when (!item.IsRoot && IsSkippable(exception))
            {
                // A single inaccessible directory must not invalidate the whole scan.
            }
        }

        return files;
    }

    public bool AnyFile(string rootPath, Func<string, bool> predicate, CancellationToken cancellationToken)
    {
        var pending = new Stack<(string Path, bool IsRoot)>();
        pending.Push((rootPath, true));
        while (pending.TryPop(out var item))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                foreach (var file in _enumerateFiles(item.Path))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (predicate(file)) return true;
                }
                foreach (var child in _enumerateDirectories(item.Path))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    pending.Push((child, false));
                }
            }
            catch (Exception exception) when (!item.IsRoot && IsSkippable(exception)) { }
        }
        return false;
    }

    public IReadOnlyList<string> EnumerateFilesInDirectory(
        string directoryPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var files = new List<string>();
        foreach (var file in _enumerateFiles(directoryPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            files.Add(file);
        }
        return files;
    }

    private static bool IsSkippable(Exception exception) =>
        exception is UnauthorizedAccessException or DirectoryNotFoundException or IOException;
}
