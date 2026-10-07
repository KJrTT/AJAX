using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace SecureFilesMvc.Security;

public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    private readonly IConfiguration _config;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration config) : base(options, logger, encoder)
    {
        _config = config;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var provided))
            return Task.FromResult(AuthenticateResult.NoResult());

        var key = provided.ToString();
        if (string.IsNullOrWhiteSpace(key))
            return Task.FromResult(AuthenticateResult.Fail("Empty API key"));

        var readKey = _config["ApiKeys:Read"];
        var writeKey = _config["ApiKeys:Write"];

        var claims = new List<Claim>();
        if (!string.IsNullOrEmpty(readKey) && key == readKey)
            claims.Add(new Claim(ClaimTypes.Role, "Files.Read"));
        if (!string.IsNullOrEmpty(writeKey) && key == writeKey)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Files.Read"));
            claims.Add(new Claim(ClaimTypes.Role, "Files.Write"));
        }

        if (claims.Count == 0)
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
