namespace Forest.Minimal.Account.Models;

public sealed record Profile(string Email, string UserName, string? FirstName, string? LastName, string? PhotoId, string? BannerId);