namespace Sorterra.Core.Interfaces;

public interface ICurrentUserService
{
    string? CognitoSub { get; }
    string? Email { get; }
}
