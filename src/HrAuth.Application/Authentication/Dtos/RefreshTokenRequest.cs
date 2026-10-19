namespace HrAuth.Application.Authentication.Dtos;

/// <summary>An expired access token paired with its refresh token.</summary>
public sealed record RefreshTokenRequest(string Token, string RefreshToken);
