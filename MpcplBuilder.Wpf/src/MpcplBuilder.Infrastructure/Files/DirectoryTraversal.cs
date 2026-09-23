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
        var pending = new Stack<string>();
        pending.Push(rootPath);

        while (pending.TryPop(out var directory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                files.AddRange(_enumerateFiles(directory).ToArray());
                foreach (var child in _enumerateDirectories(directory).ToArray())
                    pending.Push(child);
            }
            catch (Exception exception) when (IsSkippable(exception))
            {
                // A single inaccessible directory must not invalidate the whole scan.
            }
        }

        return files;
    }

    public IReadOnlyList<string> EnumerateFilesInDirectory(
        string directoryPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return _enumerateFiles(directoryPath).ToArray();
        }
        catch (Exception exception) when (IsSkippable(exception))
        {
            return [];
        }
    }

    private static bool IsSkippable(Exception exception) =>
        exception is UnauthorizedAccessException or DirectoryNotFoundException or IOException;
}
