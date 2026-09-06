namespace CodeMaster.Core.Models;

public record AccessEvaluationResult(
    bool IsValid,
    User? User = null,
    AccessPolicy? Policy = null,
    string? Reason = null
);
