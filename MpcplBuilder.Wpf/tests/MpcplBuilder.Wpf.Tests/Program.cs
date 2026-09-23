using System.Reflection;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using MpcplBuilder.Wpf;

var scanCount = 0;
Func<string, bool> scanner = _ =>
{
    scanCount++;
    return false;
};

var constructor = typeof(MainWindowViewModel).GetConstructor(
    BindingFlags.Instance | BindingFlags.NonPublic,
    binder: null,
    [typeof(Func<string, bool>)],
    modifiers: null);

if (constructor is null)
    throw new Exception("MainWindowViewModel must allow the video scanner to be supplied for testing.");

var viewModel = (MainWindowViewModel)constructor.Invoke([scanner]);
var selectFolder = typeof(MainWindowViewModel).GetMethod(
    "SetRootPathFromBrowseAsync",
    BindingFlags.Instance | BindingFlags.NonPublic);

if (selectFolder is null)
    throw new Exception("The Browse flow must expose one asynchronous folder-selection operation.");

var folder = Path.Combine(Path.GetTempPath(), $"MpcplBuilder-{Guid.NewGuid():N}");
Directory.CreateDirectory(folder);

try
{
    var task = (Task?)selectFolder.Invoke(viewModel, [folder]);
    if (task is null)
        throw new Exception("Folder selection did not return a Task.");

    await task;

    if (scanCount != 1)
        throw new Exception($"Expected one video scan after selecting a folder, but observed {scanCount}.");

    if (viewModel.RootPath != folder)
        throw new Exception("The selected folder was not assigned to RootPath.");
}
finally
{
    Directory.Delete(folder, recursive: true);
}

Console.WriteLine("PASS: selecting a folder starts exactly one video scan.");

var canExecuteChangedCount = 0;
var dispatcherThread = new Thread(() =>
{
    var dispatcher = Dispatcher.CurrentDispatcher;
    var timeout = new DispatcherTimer(
        TimeSpan.FromMilliseconds(500),
        DispatcherPriority.ApplicationIdle,
        (_, _) => dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal),
        dispatcher);
    timeout.Start();

    var command = new AsyncRelayCommand(_ => Task.CompletedTask);
    command.CanExecuteChanged += (_, _) =>
    {
        canExecuteChangedCount++;
        dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
    };

    CommandManager.InvalidateRequerySuggested();
    Dispatcher.Run();
});
dispatcherThread.SetApartmentState(ApartmentState.STA);
dispatcherThread.Start();

if (!dispatcherThread.Join(TimeSpan.FromSeconds(2)))
    throw new Exception("The command notification test did not finish.");

if (canExecuteChangedCount == 0)
    throw new Exception("AsyncRelayCommand did not notify WPF after CommandManager invalidated command state.");

Console.WriteLine("PASS: CommandManager invalidation reaches AsyncRelayCommand subscribers.");
