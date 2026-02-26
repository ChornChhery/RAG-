using ChatBot.Server.Services;
using ChatBot.Share.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace ChatBot.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EvaluationController(
    EvaluationService evaluationService,
    ILogger<EvaluationController> logger) : ControllerBase
{
    /// <summary>
    /// POST /api/evaluation
    /// Runs the full evaluation pipeline:
    ///   1. Retrieves chunks via hybrid search
    ///   2. Auto-generates reference answer from chunks
    ///   3. Generates RAG answer
    ///   4. Computes BLEU, GLEU, F1, LLM Judge scores
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<EvaluationResult>> Evaluate(
        [FromBody] EvaluationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest("Question is required.");

        logger.LogInformation(
            "Evaluation request — Q: {Q} | DocId: {D}",
            request.Question, request.DocumentId);

        var result = await evaluationService.EvaluateAsync(request, cancellationToken);

        if (result.HasError)
            return StatusCode(500, result);

        return Ok(result);
    }
}