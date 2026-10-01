namespace Gabonet.Hubble.Services;

using Gabonet.Hubble.Models;
using System.Threading;
using System.Threading.Channels;

/// <summary>
/// Cola en memoria y acotada de logs pendientes de guardar en MongoDB.
/// Las solicitudes HTTP y el proveedor de ILogger solo encolan; un servicio en segundo plano
/// los persiste por lotes. Así la aplicación nunca espera a MongoDB, aunque esté lento o caído.
/// </summary>
public sealed class HubbleLogQueue
{
    /// <summary>
    /// Número máximo de logs que se mantienen en memoria a la espera de ser guardados.
    /// </summary>
    public const int DefaultCapacity = 5_000;

    private readonly Channel<GeneralLog> _channel;
    private long _droppedCount;

    /// <summary>
    /// Constructor de la cola.
    /// </summary>
    /// <param name="capacity">Capacidad máxima; al llenarse, los logs nuevos se descartan</param>
    public HubbleLogQueue(int capacity = DefaultCapacity)
    {
        _channel = Channel.CreateBounded<GeneralLog>(new BoundedChannelOptions(capacity)
        {
            // Con Wait, TryWrite devuelve false cuando la cola está llena: el log se descarta sin bloquear
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <summary>
    /// Total de logs descartados porque la cola estaba llena (por ejemplo, con MongoDB caído).
    /// </summary>
    public long DroppedCount => Interlocked.Read(ref _droppedCount);

    /// <summary>
    /// Encola un log sin bloquear. Devuelve false si la cola está llena y el log se descartó.
    /// </summary>
    public bool TryEnqueue(GeneralLog log)
    {
        if (_channel.Writer.TryWrite(log))
        {
            return true;
        }

        Interlocked.Increment(ref _droppedCount);
        return false;
    }

    internal ChannelReader<GeneralLog> Reader => _channel.Reader;
}
