namespace AccessControl.Core.Interfaces;

using AccessControl.Core.Models;

public interface INotificationDispatcher
{
    Task DispatchAccessEventAsync(AccessLog log, CancellationToken cancellationToken = default);
    Task DispatchAlertAsync(string title, string message, CancellationToken cancellationToken = default);
}
