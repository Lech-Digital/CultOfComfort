using CultOfComfort.Server.Application;
using Google.Apis.Auth;

namespace CultOfComfort.Server.Infrastructure;

/// <summary>Validates Google ID tokens (signature against Google's keys, issuer, expiry, audience) with Google.Apis.Auth.</summary>
internal sealed class GoogleTokenVerifier : IGoogleTokenVerifier
{
    public async Task<string?> SubjectAsync(string idToken, IReadOnlyList<string> audiences, CancellationToken ct)
    {
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings { Audience = audiences });
            return payload.Subject;
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}
