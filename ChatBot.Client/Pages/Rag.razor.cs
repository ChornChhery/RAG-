using ChatBot.Share.DTOs;
using ChatBot.Share.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.Net.Http.Json;

namespace ChatBot.Client.Pages;

public partial class Rag : ComponentBase, IAsyncDisposable
{
    [Inject] private HttpClient Http { get; set; } = null!;

    // ── State ──────────────────────────────────────────────────────────────
    private List<DocumentDto> _documents = [];
    private bool _isLoading = false;
    private bool _isUploading = false;
    private string? _uploadMessage = null;
    private bool _uploadSuccess = false;
    private string? _errorMessage = null;
    private int _selectedChunkingStrategy = 0; // Default to FixedSize

    // ── Pending Upload State ───────────────────────────────────────────────
    private List<IBrowserFile> _pendingFiles = [];
    private bool _hasPendingFiles = false;
    private CancellationTokenSource? _refreshCts;

    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB
    private readonly string[] _allowedExtensions = [".pdf", ".txt", ".md"];

    // ── Lifecycle ──────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        await LoadDocumentsAsync();
        _ = StartAutoRefreshAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
    }

    // ── Auto Refresh Polling ───────────────────────────────────────────────

    private async Task StartAutoRefreshAsync()
    {
        _refreshCts = new CancellationTokenSource();
        try
        {
            while (!_refreshCts.Token.IsCancellationRequested)
            {
                await Task.Delay(2000, _refreshCts.Token);
                await LoadDocumentsAsync();
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException) { }
    }

    // ── Load Documents ─────────────────────────────────────────────────────

    private async Task LoadDocumentsAsync()
    {
        if (_isLoading) return;

        _isLoading = true;
        _errorMessage = null;

        try
        {
            _documents = await Http.GetFromJsonAsync<List<DocumentDto>>("api/documents") ?? [];
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load documents: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    // ── File Selection ─────────────────────────────────────────────────────

    private Task OnFilesSelected(InputFileChangeEventArgs e)
    {
        _errorMessage = null;
        _pendingFiles = [];

        var files = e.GetMultipleFiles(10).ToList();
        if (files.Count == 0)
        {
            _hasPendingFiles = false;
            return Task.CompletedTask;
        }

        foreach (var file in files)
        {
            var ext = Path.GetExtension(file.Name).ToLowerInvariant();
            if (!_allowedExtensions.Contains(ext))
            {
                _errorMessage = $"'{file.Name}' is not supported. Allowed: PDF, TXT, MD.";
                _hasPendingFiles = false;
                return Task.CompletedTask;
            }

            if (file.Size > MaxFileSizeBytes)
            {
                _errorMessage = $"'{file.Name}' exceeds the 50 MB limit.";
                _hasPendingFiles = false;
                return Task.CompletedTask;
            }

            _pendingFiles.Add(file);
        }

        _hasPendingFiles = _pendingFiles.Count > 0;
        return Task.CompletedTask;
    }

    // ── Upload Confirmation ────────────────────────────────────────────────

    private async Task UploadConfirmedAsync()
    {
        _uploadMessage = null;
        _errorMessage  = null;
        _isUploading   = true;
        int successCount = 0;

        foreach (var file in _pendingFiles)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                using var stream  = file.OpenReadStream(MaxFileSizeBytes);
                using var sc      = new StreamContent(stream);
                sc.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                    file.ContentType ?? "application/octet-stream");

                content.Add(sc, "file", file.Name);

                var response = await Http.PostAsync(
                    $"api/documents/upload?strategy={_selectedChunkingStrategy}", content);
                var result = await response.Content.ReadFromJsonAsync<UploadResponse>();

                if (result?.Success == true)
                    successCount++;
                else
                    _errorMessage = result?.ErrorDetail ?? "Upload failed.";
            }
            catch (Exception ex)
            {
                _errorMessage = $"Error uploading '{file.Name}': {ex.Message}";
            }
        }

        _isUploading = false;

        if (successCount > 0)
        {
            _uploadSuccess  = true;
            _uploadMessage  = $"{successCount} file(s) uploaded. Processing in background...";
            _pendingFiles   = [];
            _hasPendingFiles = false;
            await LoadDocumentsAsync();

            _ = Task.Delay(5000).ContinueWith(_ =>
            {
                _uploadMessage = null;
                _uploadSuccess = false;
                InvokeAsync(StateHasChanged);
            });
        }

        StateHasChanged();
    }

    // ── Cancel ─────────────────────────────────────────────────────────────

    private void CancelUpload()
    {
        _pendingFiles    = [];
        _hasPendingFiles = false;
        _errorMessage    = null;
    }

    // ── Delete ─────────────────────────────────────────────────────────────

    private async Task DeleteDocumentAsync(Guid id, string fileName)
    {
        if (!await ConfirmAsync($"Delete '{fileName}'?"))
            return;

        try
        {
            var response = await Http.DeleteAsync($"api/documents/{id}");
            if (response.IsSuccessStatusCode)
                await LoadDocumentsAsync();
            else
                _errorMessage = "Failed to delete document.";
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error: {ex.Message}";
        }
    }

    // ── Refresh ────────────────────────────────────────────────────────────

    private async Task RefreshStatusAsync()
        => await LoadDocumentsAsync();

    // ── Helpers ────────────────────────────────────────────────────────────

    private Task<bool> ConfirmAsync(string message)
        => Task.FromResult(true);

    public string GetStatusClass(DocumentStatus status) => status switch
    {
        DocumentStatus.Ready      => "status-ready",
        DocumentStatus.Processing => "status-processing",
        DocumentStatus.Failed     => "status-failed",
        _                         => "status-uploading"
    };

    public string GetStatusIcon(DocumentStatus status) => status switch
    {
        DocumentStatus.Ready      => "check_circle",
        DocumentStatus.Processing => "sync",
        DocumentStatus.Failed     => "cancel",
        _                         => "upload"
    };

    public string GetMethodDisplay(string method) => method switch
    {
        "FixedSize"    => "Fixed Size",
        "ContentAware" => "Content Aware",
        "Semantic"     => "Semantic",
        _              => "Unknown"
    };

    public string GetMethodClass(string method) => method switch
    {
        "FixedSize"    => "method-fixed",
        "ContentAware" => "method-aware",
        "Semantic"     => "method-semantic",
        _              => "method-unknown"
    };

    public string GetMethodIcon(string method) => method switch
    {
        "FixedSize"    => "view_module",
        "ContentAware" => "article",
        "Semantic"     => "psychology",
        _              => "help"
    };
}