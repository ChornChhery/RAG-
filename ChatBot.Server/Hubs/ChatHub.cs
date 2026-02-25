using ChatBot.Server.Services;
using ChatBot.Share.Constants;
using ChatBot.Share.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace ChatBot.Server.Hubs;

public class ChatHub(RagService ragService, ILogger<ChatHub> logger) : Hub
{
    /// <summary>
    /// Client invokes this method to send a question.
    /// The hub streams tokens back one by one via ReceiveToken,
    /// then fires ChatComplete when done.
    /// </summary>
public async Task SendMessage(ChatRequest request, string messageId)
{
    logger.LogInformation("Chat request received: {Q}", request.Question);

    try
    {
        var sources = await ragService.StreamAnswerAsync(
            request,
            onToken: async token =>
            {
                await Clients.Caller.SendAsync(
                    HubMethods.ReceiveToken,
                    new StreamToken
                    {
                        Token     = token,
                        IsFinal   = false,
                        MessageId = messageId  // ← use client's messageId
                    });
            },
            cancellationToken: Context.ConnectionAborted);

        await Clients.Caller.SendAsync(
            HubMethods.ReceiveToken,
            new StreamToken
            {
                Token     = string.Empty,
                IsFinal   = true,
                MessageId = messageId  // ← same here
            });

        await Clients.Caller.SendAsync(HubMethods.ChatComplete, sources);
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("Chat stream cancelled by client.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error in ChatHub.SendMessage");
        await Clients.Caller.SendAsync(HubMethods.ReceiveError, ex.Message);
    }
}

    public override Task OnConnectedAsync()
    {
        logger.LogInformation("Client connected: {Id}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("Client disconnected: {Id}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}