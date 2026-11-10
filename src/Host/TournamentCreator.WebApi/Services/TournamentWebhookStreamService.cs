using System.Collections.Concurrent;
using System.Threading.Channels;
using Tournaments.Contracts.Tournaments;

namespace TournamentCreator.WebApi.Services;

/// <summary>
/// Gerenciador leve em memória para Server-Sent Events (SSE).
/// Funciona como um Webhook Push HTTP unidirecional (0 bytes de tráfego ocioso, sem overhead de WebSocket).
/// Suporta inscrições por torneio específico (para a página do torneio) ou globais (para o Sininho de notificações).
/// </summary>
public class TournamentWebhookStreamService
{
    private static readonly Guid GlobalStreamKey = Guid.Empty;
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Channel<TournamentWebhookEventDto>>> _subscribers = new();

    public (Guid SubscriptionId, ChannelReader<TournamentWebhookEventDto> Reader) Subscribe(Guid? tournamentId = null)
    {
        var key = tournamentId ?? GlobalStreamKey;
        var subId = Guid.NewGuid();
        var channel = Channel.CreateBounded<TournamentWebhookEventDto>(new BoundedChannelOptions(16)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        var group = _subscribers.GetOrAdd(key, _ => new ConcurrentDictionary<Guid, Channel<TournamentWebhookEventDto>>());
        group[subId] = channel;

        return (subId, channel.Reader);
    }

    public void Unsubscribe(Guid? tournamentId, Guid subscriptionId)
    {
        var key = tournamentId ?? GlobalStreamKey;
        if (_subscribers.TryGetValue(key, out var group))
        {
            if (group.TryRemove(subscriptionId, out var channel))
            {
                channel.Writer.TryComplete();
            }
        }
    }

    public void Publish(TournamentWebhookEventDto evt)
    {
        // 1. Notifica quem está na página específica deste torneio
        if (_subscribers.TryGetValue(evt.TournamentId, out var tournamentGroup))
        {
            foreach (var kvp in tournamentGroup)
            {
                kvp.Value.Writer.TryWrite(evt);
            }
        }

        // 2. Notifica quem está escutando o Sininho Global em qualquer página do sistema
        if (_subscribers.TryGetValue(GlobalStreamKey, out var globalGroup))
        {
            foreach (var kvp in globalGroup)
            {
                kvp.Value.Writer.TryWrite(evt);
            }
        }
    }

    public async Task StreamToResponseAsync(HttpResponse response, Guid? tournamentId, CancellationToken ct)
    {
        response.Headers.Append("Content-Type", "text/event-stream");
        response.Headers.Append("Cache-Control", "no-cache");
        response.Headers.Append("Connection", "keep-alive");

        var (subId, reader) = Subscribe(tournamentId);
        try
        {
            await foreach (var evt in reader.ReadAllAsync(ct))
            {
                var json = System.Text.Json.JsonSerializer.Serialize(evt);
                await response.WriteAsync($"data: {json}\n\n", ct);
                await response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Cliente fechou a aba/desconectou normalmente
        }
        finally
        {
            Unsubscribe(tournamentId, subId);
        }
    }
}
