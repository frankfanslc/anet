﻿using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Anet.Web.Jwt;

public class JwtProvider
{
    private readonly JwtTokenOptions _options;
    private readonly IRefreshTokenStore _refreshTokenStore;

    public JwtProvider(JwtTokenOptions options, IRefreshTokenStore refreshTokenStore)
    {
        _options = options;
        _refreshTokenStore = refreshTokenStore;
    }

    public async Task<JwtResult> GenerateToken(IEnumerable<Claim> claims)
    {
        var jwtSecurityToken = new JwtSecurityToken(
            claims: claims,
            issuer: _options.Issuer,
            audience: _options.Audience,
            expires: _options.Expiration > 0
                ? DateTime.UtcNow.AddSeconds(_options.Expiration)
                : default(DateTime?),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
                SecurityAlgorithms.HmacSha256)
        );

        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken);

        var result = new JwtResult
        {
            AccessToken = accessToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(_options.Expiration).ToTimestamp()
        };

        await _refreshTokenStore.SaveTokenAsync(result);

        return result;
    }

    public async Task<JwtResult> RefreshToken(string refreshToken)
    {
        var token = await _refreshTokenStore.GetTokenAsync(refreshToken);
        if (token == null) return null;

        var securityToken = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var newToken = await GenerateToken(securityToken.Claims.ToList());

        await _refreshTokenStore.DeleteTokenAsync(refreshToken);
        await _refreshTokenStore.SaveTokenAsync(newToken);

        return newToken;
    }
}

