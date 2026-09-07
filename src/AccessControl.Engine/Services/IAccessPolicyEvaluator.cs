using AccessControl.Core.DTOs;
using AccessControl.Core.Models;

namespace AccessControl.Engine.Services;

public interface IAccessPolicyEvaluator
{
    Task<AccessEvaluationResult> EvaluateAsync(
        KeypadEventDto keypadEvent,
        AccessPoint accessPoint,
        DateTime? evaluationTimeUtc = null,
        CancellationToken cancellationToken = default);
}
