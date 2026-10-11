using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace MIN.Desktop.Infrastructure.Services;

/// <summary>
/// Обеспечивает единственный экземпляр приложения (release):
/// мутекс определяет первую инстанцию, named pipe передаёт сигнал "покажи окно".
/// </summary>
internal sealed class SingleInstanceService : IDisposable
{
    public const string MutexName = "Global\\MIN-SingleInstance";
    public const string PipeName = "MIN-SingleInstance-Signal";
    public const string PipeShowCommand = "SHOW";

    private Mutex? mutex;
    private CancellationTokenSource? cts;
    private Task? listenerTask;

    /// <summary>
    /// Вызывается при получении сигнала показать окно.
    /// </summary>
    public event Action? ShowRequested;

    /// <summary>
    /// Попытаться стать единственным экземпляром.
    /// false — значит уже запущено: отправляем сигнал и выходим.
    /// </summary>
    public bool TryAcquire()
    {
        mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);

        if (!createdNew)
        {
            mutex.Dispose();
            mutex = null;
            SignalExistingInstance();
            return false;
        }

        cts = new CancellationTokenSource();
        listenerTask = Task.Run(() => ListenAsync(cts.Token));
        return true;
    }

    private static void SignalExistingInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(timeout: 2000);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.Write(PipeShowCommand);
        }
        catch { }
    }

    private async Task ListenAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName, PipeDirection.In, maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(token);

                using var reader = new StreamReader(server);
                var message = await reader.ReadToEndAsync(token);

                if (message == PipeShowCommand)
                {
                    ShowRequested?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch { }
        }
    }

    public void Dispose()
    {
        cts?.Cancel();
        try
        {
            listenerTask?.Wait(1000);
        }
        catch
        {
        }
        mutex?.ReleaseMutex();
        mutex?.Dispose();
    }
}
