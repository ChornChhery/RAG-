using ChatBot.Share.Constants;
using ChatBot.Share.DTOs;
using ChatBot.Share.Enums;
using Markdig;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace ChatBot.Client.Pages;

public partial class Chat : ComponentBase, IAsyncDisposable
{
    [Inject] private NavigationManager Navigation { get; set; } = null!;

    // ── State ──────────────────────────────────────────────────────────────
    private List<ChatMessageDto> _messages = [];
    private string _userInput = string.Empty;
    private bool _isConnected = false;
    private bool _isStreaming = false;
    private string? _errorMessage = null;
    private string? _currentStreamingId = null;

    private HubConnection? _hubConnection;
    private readonly MarkdownPipeline _markdownPipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    // ── Lifecycle ──────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        await ConnectToHubAsync();
    }

    private async Task ConnectToHubAsync()
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5087" + HubRoutes.Chat)
            .WithAutomaticReconnect()
            .Build();

        // ── Incoming token ─────────────────────────────────────────────────
        _hubConnection.On<StreamToken>(HubMethods.ReceiveToken, token =>
        {
            var msg = _messages.FirstOrDefault(m => m.Id == token.MessageId)
                ?? _messages.LastOrDefault(m => m.Role == MessageRole.Assistant && m.IsStreaming);

            if (msg is null) return;

            msg.Content    += token.Token;
            msg.IsStreaming = !token.IsFinal;

            if (token.IsFinal)
            {
                _isStreaming        = false;
                _currentStreamingId = null;
            }

            InvokeAsync(StateHasChanged);
        });

        // ── Chat complete with sources ─────────────────────────────────────
        _hubConnection.On<List<DocumentChunkResult>>(HubMethods.ChatComplete, sources =>
        {
            var lastMsg = _messages.LastOrDefault(m => m.Role == MessageRole.Assistant);
            if (lastMsg is not null)
                lastMsg.SourceDocuments = sources.Select(s => s.DocumentName).Distinct().ToList();

            _isStreaming = false;
            InvokeAsync(StateHasChanged);
        });

        // ── Error ──────────────────────────────────────────────────────────
        _hubConnection.On<string>(HubMethods.ReceiveError, error =>
        {
            _errorMessage = error;
            _isStreaming   = false;
            InvokeAsync(StateHasChanged);
        });

        await _hubConnection.StartAsync();
        _isConnected = true;
        StateHasChanged();
    }

    // ── Send message ───────────────────────────────────────────────────────

    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(_userInput) || !_isConnected || _isStreaming)
            return;

        _errorMessage = null;

        _messages.Add(new ChatMessageDto
        {
            Role      = MessageRole.User,
            Content   = _userInput,
            Timestamp = DateTime.UtcNow
        });

        var assistantMsg = new ChatMessageDto
        {
            Role        = MessageRole.Assistant,
            Content     = string.Empty,
            IsStreaming = true,
            Timestamp   = DateTime.UtcNow
        };
        _messages.Add(assistantMsg);
        _currentStreamingId = assistantMsg.Id;
        _isStreaming        = true;

        var request = new ChatRequest
        {
            Question   = _userInput,
            DocumentId = null, // always search all documents
            TopK       = 5,
            History    = _messages.SkipLast(2).ToList()
        };

        var messageId = assistantMsg.Id;
        _userInput = string.Empty;
        StateHasChanged();

        await _hubConnection!.InvokeAsync(HubMethods.SendMessage, request, messageId);
    }

    private async Task HandleKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !e.ShiftKey)
            await SendMessageAsync();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    public string RenderMarkdown(string markdown)
        => Markdown.ToHtml(markdown ?? string.Empty, _markdownPipeline);

    public string GetConnectionStatus()
        => _hubConnection?.State switch
        {
            HubConnectionState.Connected    => "Connected",
            HubConnectionState.Connecting   => "Connecting...",
            HubConnectionState.Reconnecting => "Reconnecting...",
            _                               => "Disconnected"
        };

    // ── Dispose ────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
            await _hubConnection.DisposeAsync();
    }
}