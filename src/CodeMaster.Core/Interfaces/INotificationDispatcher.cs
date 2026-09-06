namespace CodeMaster.Core.Interfaces;

using CodeMaster.Core.Models;

public interface INotificationDispatcher
{
    Task DispatchAccessEventAsync(AccessLog log, CancellationToken cancellationToken = default);
    Task DispatchAlertAsync(string title, string message, CancellationToken cancellationToken = default);
}
