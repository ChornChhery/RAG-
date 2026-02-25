using ChatBot.Share.DTOs;
using ChatBot.Share.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.Net.Http.Json;

namespace ChatBot.Client.Pages;

public partial class Rag : ComponentBase
{
    [Inject] private HttpClient Http { get; set; } = null!;

    // ── State ──────────────────────────────────────────────────────────────
    private List<DocumentDto> _documents = [];
    private bool _isLoading = false;
    private bool _isUploading = false;
    private string? _uploadMessage = null;
    private bool _uploadSuccess = false;
    private string? _errorMessage = null;

    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB
    private readonly string[] _allowedExtensions = [".pdf", ".txt", ".md"];

    // ── Lifecycle ──────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
        => await LoadDocumentsAsync();

    // ── Load Documents ─────────────────────────────────────────────────────

    private async Task LoadDocumentsAsync()
    {
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

    // ── Upload ─────────────────────────────────────────────────────────────

    private async Task OnFilesSelected(InputFileChangeEventArgs e)
    {
        _uploadMessage = null;
        _errorMessage  = null;

        var files = e.GetMultipleFiles(10);
        _isUploading = true;
        int successCount = 0;

        foreach (var file in files)
        {
            var ext = Path.GetExtension(file.Name).ToLowerInvariant();
            if (!_allowedExtensions.Contains(ext))
            {
                _errorMessage = $"'{file.Name}' is not supported. Allowed: PDF, TXT, MD.";
                continue;
            }

            if (file.Size > MaxFileSizeBytes)
            {
                _errorMessage = $"'{file.Name}' exceeds the 50 MB limit.";
                continue;
            }

            try
            {
                using var content   = new MultipartFormDataContent();
                using var stream    = file.OpenReadStream(MaxFileSizeBytes);
                using var sc        = new StreamContent(stream);
                sc.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                    file.ContentType ?? "application/octet-stream");

                content.Add(sc, "file", file.Name);

                var response = await Http.PostAsync("api/documents/upload", content);
                var result   = await response.Content.ReadFromJsonAsync<UploadResponse>();

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
            _uploadMessage  = $"{successCount} file(s) uploaded. Embedding in progress...";
            await LoadDocumentsAsync();

            // Auto-clear the success message after 5 seconds
            _ = Task.Delay(5000).ContinueWith(_ =>
            {
                _uploadMessage = null;
                _uploadSuccess = false;
                InvokeAsync(StateHasChanged);
            });
        }

        StateHasChanged();
    }

    // ── Delete ─────────────────────────────────────────────────────────────

    private async Task DeleteDocumentAsync(Guid id, string fileName)
    {
        if (!await ConfirmAsync($"Delete '{fileName}'? This will remove all its embeddings."))
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

    // ── Refresh polling for Processing documents ───────────────────────────

    private async Task RefreshStatusAsync()
        => await LoadDocumentsAsync();

    // ── Helpers ────────────────────────────────────────────────────────────

    private Task<bool> ConfirmAsync(string message)
    {
        // In Blazor WASM, use JS interop for confirm — simplified here as always true
        // Wire up IJSRuntime in a real project: await JS.InvokeAsync<bool>("confirm", message)
        return Task.FromResult(true);
    }

    public string GetStatusClass(DocumentStatus status) => status switch
    {
        DocumentStatus.Ready      => "status-ready",
        DocumentStatus.Processing => "status-processing",
        DocumentStatus.Failed     => "status-failed",
        _                         => "status-uploading"
    };
}