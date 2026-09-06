using CodeMaster.Core.DTOs;
using CodeMaster.Core.Models;

namespace CodeMaster.Engine.Services;

public interface IAccessPolicyEvaluator
{
    Task<AccessEvaluationResult> EvaluateAsync(
        KeypadEventDto keypadEvent,
        AccessPoint accessPoint,
        DateTime? evaluationTimeUtc = null,
        CancellationToken cancellationToken = default);
}
