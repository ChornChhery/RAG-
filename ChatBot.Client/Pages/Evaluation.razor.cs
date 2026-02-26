using ChatBot.Share.DTOs;
using ChatBot.Share.Enums;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace ChatBot.Client.Pages;

public partial class Evaluation : ComponentBase
{
    [Inject] private HttpClient Http { get; set; } = null!;

    // ── State ──────────────────────────────────────────────────────────────
    private List<DocumentDto> _documents          = [];
    private Guid?             _selectedDocumentId = null;
    private string            _question           = string.Empty;
    private bool              _isRunning          = false;
    private bool              _loadingDocs        = true;
    private string            _runningStep        = "Running...";
    private EvaluationResult? _result             = null;
    private string?           _errorMessage       = null;

    private bool CanRun => !_isRunning && !string.IsNullOrWhiteSpace(_question);

    // ── Lifecycle ──────────────────────────────────────────────────────────
    protected override async Task OnInitializedAsync()
        => await LoadDocumentsAsync();

    // ── Load only Ready documents from Rag page ────────────────────────────
    private async Task LoadDocumentsAsync()
    {
        _loadingDocs = true;
        try
        {
            var all = await Http.GetFromJsonAsync<List<DocumentDto>>("api/documents") ?? [];
            _documents = all.Where(d => d.Status == DocumentStatus.Ready).ToList();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load documents: {ex.Message}";
        }
        finally
        {
            _loadingDocs = false;
            StateHasChanged();
        }
    }

    // ── Run Evaluation ─────────────────────────────────────────────────────
    private async Task RunEvaluationAsync()
    {
        if (!CanRun) return;

        _isRunning    = true;
        _errorMessage = null;
        _result       = null;

        // Show progress steps to user
        var steps = new[]
        {
            "Searching document chunks...",
            "Generating reference answer...",
            "Generating RAG answer...",
            "Computing scores...",
            "Running LLM judge..."
        };

        // Cycle through step labels while waiting
        using var cts = new CancellationTokenSource();
        var stepTask = Task.Run(async () =>
        {
            int i = 0;
            while (!cts.Token.IsCancellationRequested)
            {
                _runningStep = steps[i % steps.Length];
                await InvokeAsync(StateHasChanged);
                await Task.Delay(3000, cts.Token).ContinueWith(_ => { });
                i++;
            }
        });

        try
        {
            var request = new EvaluationRequest
            {
                Question   = _question.Trim(),
                DocumentId = _selectedDocumentId,
                TopK       = 5
            };

            var response = await Http.PostAsJsonAsync("api/evaluation", request);

            if (response.IsSuccessStatusCode)
            {
                _result = await response.Content.ReadFromJsonAsync<EvaluationResult>();
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _errorMessage = $"Evaluation failed: {error}";
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            cts.Cancel();
            _isRunning = false;
            StateHasChanged();
        }
    }

    // ── Score Helpers ──────────────────────────────────────────────────────
    public string GetScoreClass(double score) => score switch
    {
        >= 0.7 => "score-excellent",
        >= 0.4 => "score-good",
        >= 0.2 => "score-fair",
        _      => "score-poor"
    };

    public string GetScoreIcon(double score) => score switch
    {
        >= 0.7 => "check_circle",
        >= 0.4 => "info",
        >= 0.2 => "warning",
        _      => "cancel"
    };

    public string GetScoreLabel(double score) => score switch
    {
        >= 0.7 => "Excellent",
        >= 0.4 => "Good",
        >= 0.2 => "Fair",
        _      => "Poor"
    };
}