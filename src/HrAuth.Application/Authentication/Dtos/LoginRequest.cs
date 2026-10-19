namespace HrAuth.Application.Authentication.Dtos;

/// <summary>Credentials submitted to authenticate a user.</summary>
public sealed record LoginRequest(string UserName, string Password);
