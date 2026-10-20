using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication; 
using System.Net.Http.Headers; 
// cookies
// var builder = WebApplication.CreateBuilder(args);

//    builder.Services
//     .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
//     .AddCookie(options =>
//     {
//         options.Cookie.Name = ".MyApp.Auth";
//         options.Cookie.HttpOnly = true;
//         options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
//         options.Cookie.SameSite = SameSiteMode.Lax;
//         options.ExpireTimeSpan = TimeSpan.FromHours(8);
//         options.SlidingExpiration = true;

//         options.LoginPath = "/login";
//         options.LogoutPath = "/logout";
//         options.AccessDeniedPath = "/forbidden";

//         options.Events = new CookieAuthenticationEvents
//         {
//             OnValidatePrincipal = async ctx =>
//             {
//                 var sid = ctx.Principal?.FindFirst("sid")?.Value;
//                 if (sid is null || RevokedSessions.IsRevoked(sid))
//                 {
//                     ctx.RejectPrincipal();
//                     await ctx.HttpContext.SignOutAsync();
//                 }
//             }
//         };
//     });

// builder.Services.AddAuthorization();
// builder.Services.AddAntiforgery();

// builder.Services.AddDataProtection()
//     .PersistKeysToFileSystem(new DirectoryInfo(
//         builder.Configuration["DataProtection:KeysPath"] ?? "/var/keys"))
//     .SetApplicationName("MyApp");

// var app = builder.Build();

// app.UseHttpsRedirection();
// app.UseAuthentication();
// app.UseAuthorization();
// app.UseAntiforgery();

// app.MapGet("/csrf", (HttpContext ctx, IAntiforgery af) =>
// {
//     var pair = af.GetAndStoreTokens(ctx);
//     return Results.Ok(new { csrf = pair.RequestToken });
// });

// app.MapGet("/login", (HttpContext ctx, IAntiforgery af) =>
// {
//     var pair = af.GetAndStoreTokens(ctx);
//     return Results.Ok(new { csrf = pair.RequestToken });
// });

// app.MapPost("/login", async (HttpContext ctx, IAntiforgery af, LoginDto dto) =>
// {
//     await af.ValidateRequestAsync(ctx);

//     if (!UserStore.Verify(dto.Username, dto.Password))
//         return Results.Unauthorized();

//     var user = UserStore.Users[dto.Username];
//     var sid = Guid.NewGuid().ToString("N");

//     var claims = new List<Claim>
//     {
//         new(ClaimTypes.Name, user.Username),
//         new(ClaimTypes.NameIdentifier, user.Username),
//         new("sid", sid)
//     };
//     claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r)));

//     var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
//     var principal = new ClaimsPrincipal(identity);

//     await ctx.SignInAsync(
//         CookieAuthenticationDefaults.AuthenticationScheme,
//         principal,
//         new AuthenticationProperties
//         {
//             IsPersistent = dto.RememberMe
//         });

//     return Results.Ok(new { user = user.Username, sid });
// });

// app.MapPost("/logout", async (HttpContext ctx, IAntiforgery af) =>
// {
//     await af.ValidateRequestAsync(ctx);

//     var sid = ctx.User.FindFirst("sid")?.Value;
//     if (sid is not null) RevokedSessions.Revoke(sid);

//     await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
//     return Results.Ok();
// }).RequireAuthorization();

// app.MapGet("/me", (HttpContext ctx) =>
//     ctx.User.Identity?.IsAuthenticated == true
//         ? Results.Ok(new
//         {
//             name = ctx.User.Identity!.Name,
//             roles = ctx.User.FindAll(ClaimTypes.Role).Select(c => c.Value)
//         })
//         : Results.Unauthorized())
//    .RequireAuthorization();

// app.MapGet("/admin", () => Results.Ok("admin area"))
//    .RequireAuthorization(policy => policy.RequireRole("admin"));

// app.Run();


// record LoginDto(string Username, string Password, bool RememberMe = false);
// record User(string Username, string PasswordHash, string[] Roles);

// static class UserStore
// {
//     public static readonly Dictionary<string, User> Users = new()
//     {
//         ["alice"] = new User("alice", Hash("p@ss"), new[] { "user", "admin" }),
//         ["bob"]   = new User("bob",   Hash("p@ss"), new[] { "user" })
//     };

//     public static string Hash(string s) =>
//         Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

//     public static bool Verify(string u, string p)
//     {
//         if (!Users.TryGetValue(u, out var user)) return false;
//         var a = Encoding.UTF8.GetBytes(user.PasswordHash);
//         var b = Encoding.UTF8.GetBytes(Hash(p));
//         return CryptographicOperations.FixedTimeEquals(a, b);
//     }
// }

// static class RevokedSessions
// {
//     private static readonly ConcurrentDictionary<string, byte> _set = new();
//     public static bool IsRevoked(string sid) => _set.ContainsKey(sid);
//     public static void Revoke(string sid) => _set.TryAdd(sid, 0);
// }

// Bearer

// var builder = WebApplication.CreateBuilder(args);

// var jwtKey = builder.Configuration["Jwt:Key"]
//     ?? "SUPER_SECRET_KEY_AT_LEAST_32_BYTES_LONG_1234";
// var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "https://myapp.example";
// var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "myapp.api";

// builder.Services
//     .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//     .AddJwtBearer(options =>
//     {
//         options.RequireHttpsMetadata = true;
//         options.SaveToken = false;

//         options.TokenValidationParameters = new TokenValidationParameters
//         {
//             ValidateIssuer = true,
//             ValidIssuer = jwtIssuer,

//             ValidateAudience = true,
//             ValidAudience = jwtAudience,

//             ValidateIssuerSigningKey = true,
//             IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

//             ValidateLifetime = true,
//             ClockSkew = TimeSpan.FromSeconds(30),

//             ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },

//             NameClaimType = JwtRegisteredClaimNames.Sub,
//             RoleClaimType = "role"
//         };

//         options.Events = new JwtBearerEvents
//         {
//             OnTokenValidated = ctx =>
//             {
//                 var jti = ctx.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
//                 if (jti is not null && RevokedTokens.IsRevoked(jti))
//                     ctx.Fail("Token revoked");
//                 return Task.CompletedTask;
//             }
//         };
//     });

// builder.Services.AddAuthorization();

// var app = builder.Build();

// app.UseHttpsRedirection();
// app.UseAuthentication();
// app.UseAuthorization();

// app.MapPost("/token", (TokenRequest req) =>
// {
//     if (!UserStore.Verify(req.Username, req.Password))
//         return Results.Unauthorized();

//     var user = UserStore.Users[req.Username];
//     var access = TokenService.CreateAccessToken(
//         user.Username, user.Roles, jwtKey, jwtIssuer, jwtAudience,
//         TimeSpan.FromMinutes(15));

//     var refresh = TokenService.CreateRefreshToken();
//     RefreshStore.Save(user.Username, refresh, DateTimeOffset.UtcNow.AddDays(7));

//     return Results.Ok(new
//     {
//         access_token = access,
//         refresh_token = refresh,
//         token_type = "Bearer",
//         expires_in = 900
//     });
// });

// app.MapPost("/token/refresh", (RefreshRequest req) =>
// {
//     var entry = RefreshStore.Get(req.RefreshToken);
//     if (entry is null || entry.Expires < DateTimeOffset.UtcNow)
//         return Results.Unauthorized();

//     RefreshStore.Revoke(req.RefreshToken);

//     var user = UserStore.Users[entry.Username];
//     var access = TokenService.CreateAccessToken(
//         user.Username, user.Roles, jwtKey, jwtIssuer, jwtAudience,
//         TimeSpan.FromMinutes(15));
//     var newRefresh = TokenService.CreateRefreshToken();
//     RefreshStore.Save(user.Username, newRefresh, DateTimeOffset.UtcNow.AddDays(7));

//     return Results.Ok(new
//     {
//         access_token = access,
//         refresh_token = newRefresh,
//         token_type = "Bearer",
//         expires_in = 900
//     });
// });

// app.MapPost("/token/revoke", (HttpContext ctx, RevokeRequest req) =>
// {
//     var jti = ctx.User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
//     if (jti is not null) RevokedTokens.Revoke(jti);
//     if (req.RefreshToken is not null) RefreshStore.Revoke(req.RefreshToken);
//     return Results.Ok();
// }).RequireAuthorization();

// app.MapGet("/me", (HttpContext ctx) =>
//     Results.Ok(new
//     {
//         sub = ctx.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
//         roles = ctx.User.FindAll("role").Select(c => c.Value)
//     }))
//    .RequireAuthorization();

// app.MapGet("/admin", () => Results.Ok("admin area"))
//    .RequireAuthorization(p => p.RequireClaim("role", "admin"));

// app.Run();


// record TokenRequest(string Username, string Password);
// record RefreshRequest(string RefreshToken);
// record RevokeRequest(string? RefreshToken);
// record User(string Username, string PasswordHash, string[] Roles);

// static class UserStore
// {
//     public static readonly Dictionary<string, User> Users = new()
//     {
//         ["alice"] = new User("alice", Hash("p@ss"), new[] { "user", "admin" }),
//         ["bob"]   = new User("bob",   Hash("p@ss"), new[] { "user" })
//     };

//     public static string Hash(string s) =>
//         Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

//     public static bool Verify(string u, string p)
//     {
//         if (!Users.TryGetValue(u, out var user)) return false;
//         var a = Encoding.UTF8.GetBytes(user.PasswordHash);
//         var b = Encoding.UTF8.GetBytes(Hash(p));
//         return CryptographicOperations.FixedTimeEquals(a, b);
//     }
// }

// static class TokenService
// {
//     public static string CreateAccessToken(
//         string username,
//         IEnumerable<string> roles,
//         string key,
//         string issuer,
//         string audience,
//         TimeSpan lifetime)
//     {
//         var claims = new List<Claim>
//         {
//             new(JwtRegisteredClaimNames.Sub, username),
//             new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
//             new(JwtRegisteredClaimNames.Iat,
//                 DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
//                 ClaimValueTypes.Integer64)
//         };
//         claims.AddRange(roles.Select(r => new Claim("role", r)));

//         var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
//         var creds = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

//         var token = new JwtSecurityToken(
//             issuer: issuer,
//             audience: audience,
//             claims: claims,
//             notBefore: DateTime.UtcNow,
//             expires: DateTime.UtcNow.Add(lifetime),
//             signingCredentials: creds);

//         return new JwtSecurityTokenHandler().WriteToken(token);
//     }

//     public static string CreateRefreshToken()
//     {
//         var bytes = new byte[64];
//         RandomNumberGenerator.Fill(bytes);
//         return Convert.ToBase64String(bytes);
//     }
// }

// static class RefreshStore
// {
//     public record Entry(string Username, DateTimeOffset Expires);

//     // В реальности: БД + хэш токена. Здесь — in-memory, синхронизированный.
//     private static readonly ConcurrentDictionary<string, Entry> _map = new();

//     public static void Save(string user, string token, DateTimeOffset exp) =>
//         _map[token] = new Entry(user, exp);

//     public static Entry? Get(string token) =>
//         _map.TryGetValue(token, out var e) ? e : null;

//     public static void Revoke(string token) => _map.TryRemove(token, out _);
// }

// static class RevokedTokens
// {
//     private static readonly ConcurrentDictionary<string, byte> _set = new();
//     public static bool IsRevoked(string jti) => _set.ContainsKey(jti);
//     public static void Revoke(string jti) => _set.TryAdd(jti, 0);
// }

// Bff

// var builder = WebApplication.CreateBuilder(args);

// var jwtKey = builder.Configuration["Jwt:Key"]
//     ?? "SUPER_SECRET_KEY_AT_LEAST_32_BYTES_LONG_1234";
// var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "https://myapp.example";
// var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "myapp.api";

// // --- Cookie (для BFF-сессии браузера) + Bearer (для API) ---
// builder.Services
//     .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
//     .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, o =>
//     {
//         o.Cookie.Name = ".Bff.Session";
//         o.Cookie.HttpOnly = true;
//         o.Cookie.SameSite = SameSiteMode.Lax;
//         o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
//         o.LoginPath = "/bff/login";
//         o.ExpireTimeSpan = TimeSpan.FromHours(8);
//         o.SlidingExpiration = true;
//     })
//     .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o =>
//     {
//         o.TokenValidationParameters = new TokenValidationParameters
//         {
//             ValidateIssuer = true, ValidIssuer = jwtIssuer,
//             ValidateAudience = true, ValidAudience = jwtAudience,
//             ValidateIssuerSigningKey = true,
//             IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
//             ValidateLifetime = true,
//             ClockSkew = TimeSpan.FromSeconds(30),
//             ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
//             NameClaimType = JwtRegisteredClaimNames.Sub,
//             RoleClaimType = "role"
//         };
//     });

// builder.Services.AddAuthorization(o =>
// {
//     o.AddPolicy("Bff", p => p
//         .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme)
//         .RequireAuthenticatedUser());
//     o.AddPolicy("Api", p => p
//         .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
//         .RequireAuthenticatedUser());
// });

// builder.Services.AddAntiforgery();

// builder.Services.AddHttpClient("api", c =>
// {
//     c.BaseAddress = new Uri(builder.Configuration["Api:BaseAddress"] ?? "http://localhost:5109");
// });

// var app = builder.Build();

// app.UseHttpsRedirection();
// app.UseAuthentication();
// app.UseAuthorization();
// app.UseAntiforgery();


// app.MapPost("/token", (TokenRequest req) =>
// {
//     if (!UserStore.Verify(req.Username, req.Password))
//         return Results.Unauthorized();

//     var user = UserStore.Users[req.Username];
//     var access = TokenService.CreateAccessToken(
//         user.Username, user.Roles, jwtKey, jwtIssuer, jwtAudience,
//         TimeSpan.FromMinutes(15));
//     var refresh = TokenService.CreateRefreshToken();
//     RefreshStore.Save(user.Username, refresh, DateTimeOffset.UtcNow.AddDays(7));

//     return Results.Ok(new
//     {
//         access_token = access,
//         refresh_token = refresh,
//         token_type = "Bearer",
//         expires_in = 900
//     });
// });

// app.MapPost("/token/refresh", (RefreshRequest req) =>
// {
//     var entry = RefreshStore.Get(req.RefreshToken);
//     if (entry is null || entry.Expires < DateTimeOffset.UtcNow)
//         return Results.Unauthorized();

//     RefreshStore.Revoke(req.RefreshToken);

//     var user = UserStore.Users[entry.Username];
//     var access = TokenService.CreateAccessToken(
//         user.Username, user.Roles, jwtKey, jwtIssuer, jwtAudience,
//         TimeSpan.FromMinutes(15));
//     var newRefresh = TokenService.CreateRefreshToken();
//     RefreshStore.Save(user.Username, newRefresh, DateTimeOffset.UtcNow.AddDays(7));

//     return Results.Ok(new
//     {
//         access_token = access,
//         refresh_token = newRefresh,
//         token_type = "Bearer",
//         expires_in = 900
//     });
// });

// app.MapGet("/api/me", (HttpContext ctx) => Results.Ok(new
// {
//     sub = ctx.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
//     roles = ctx.User.FindAll("role").Select(c => c.Value)
// })).RequireAuthorization("Api");

// app.MapGet("/api/admin", () => Results.Ok("admin area"))
//    .RequireAuthorization(p => p
//        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
//        .RequireClaim("role", "admin"));


// app.MapGet("/bff/csrf", (HttpContext ctx, IAntiforgery af) =>
// {
//     var pair = af.GetAndStoreTokens(ctx);
//     return Results.Ok(new { csrf = pair.RequestToken });
// });

// app.MapPost("/bff/login", async (
//     HttpContext ctx,
//     IAntiforgery af,
//     LoginDto dto,
//     IHttpClientFactory http) =>
// {
//     await af.ValidateRequestAsync(ctx);

//     var client = http.CreateClient("api");
//     var tokenResp = await client.PostAsJsonAsync("/token", new { dto.Username, dto.Password });
//     if (!tokenResp.IsSuccessStatusCode) return Results.Unauthorized();

//     var tokens = await tokenResp.Content.ReadFromJsonAsync<TokenResponse>();
//     if (tokens is null) return Results.Unauthorized();

//     var sid = Guid.NewGuid().ToString("N");
//     SessionTokenStore.Save(
//         sid,
//         dto.Username,
//         tokens.AccessToken,
//         tokens.RefreshToken,
//         DateTimeOffset.UtcNow.AddSeconds(tokens.ExpiresIn));

//     var claims = new List<Claim>
//     {
//         new(ClaimTypes.NameIdentifier, dto.Username),
//         new(ClaimTypes.Name, dto.Username),
//         new("sid", sid)
//     };
//     var principal = new ClaimsPrincipal(
//         new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

//     await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
//     return Results.Ok(new { user = dto.Username });
// });

// app.MapPost("/bff/logout", async (HttpContext ctx, IAntiforgery af) =>
// {
//     await af.ValidateRequestAsync(ctx);

//     var sid = ctx.User.FindFirst("sid")?.Value;
//     if (sid is not null) SessionTokenStore.Remove(sid);

//     await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
//     return Results.Ok();
// }).RequireAuthorization("Bff");

// app.MapGet("/bff/me", async (HttpContext ctx, IHttpClientFactory http) =>
// {
//     var sid = ctx.User.FindFirst("sid")!.Value;
//     var access = await SessionTokenStore.GetValidAccessAsync(sid, http);
//     if (access is null) return Results.Unauthorized();

//     var client = http.CreateClient("api");
//     using var req = new HttpRequestMessage(HttpMethod.Get, "/api/me");
//     req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);

//     var resp = await client.SendAsync(req);
//     var body = await resp.Content.ReadAsStringAsync();
//     return Results.Content(body, "application/json", statusCode: (int)resp.StatusCode);
// }).RequireAuthorization("Bff");

// app.Run();


// record LoginDto(string Username, string Password);
// record TokenRequest(string Username, string Password);
// record RefreshRequest(string RefreshToken);
// record TokenResponse(string AccessToken, string RefreshToken, int ExpiresIn);
// record User(string Username, string PasswordHash, string[] Roles);

// static class UserStore
// {
//     public static readonly Dictionary<string, User> Users = new()
//     {
//         ["alice"] = new User("alice", Hash("p@ss"), new[] { "user", "admin" }),
//         ["bob"]   = new User("bob",   Hash("p@ss"), new[] { "user" })
//     };

//     public static string Hash(string s) =>
//         Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

//     public static bool Verify(string u, string p)
//     {
//         if (!Users.TryGetValue(u, out var user)) return false;
//         var a = Encoding.UTF8.GetBytes(user.PasswordHash);
//         var b = Encoding.UTF8.GetBytes(Hash(p));
//         return CryptographicOperations.FixedTimeEquals(a, b);
//     }
// }

// static class TokenService
// {
//     public static string CreateAccessToken(
//         string username,
//         IEnumerable<string> roles,
//         string key,
//         string issuer,
//         string audience,
//         TimeSpan lifetime)
//     {
//         var claims = new List<Claim>
//         {
//             new(JwtRegisteredClaimNames.Sub, username),
//             new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
//             new(JwtRegisteredClaimNames.Iat,
//                 DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
//                 ClaimValueTypes.Integer64)
//         };
//         claims.AddRange(roles.Select(r => new Claim("role", r)));

//         var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
//         var creds = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

//         var token = new JwtSecurityToken(
//             issuer: issuer,
//             audience: audience,
//             claims: claims,
//             notBefore: DateTime.UtcNow,
//             expires: DateTime.UtcNow.Add(lifetime),
//             signingCredentials: creds);

//         return new JwtSecurityTokenHandler().WriteToken(token);
//     }

//     public static string CreateRefreshToken()
//     {
//         var bytes = new byte[64];
//         RandomNumberGenerator.Fill(bytes);
//         return Convert.ToBase64String(bytes);
//     }
// }

// static class RefreshStore
// {
//     public record Entry(string Username, DateTimeOffset Expires);
//     private static readonly ConcurrentDictionary<string, Entry> _map = new();

//     public static void Save(string user, string token, DateTimeOffset exp) =>
//         _map[token] = new Entry(user, exp);

//     public static Entry? Get(string token) =>
//         _map.TryGetValue(token, out var e) ? e : null;

//     public static void Revoke(string token) => _map.TryRemove(token, out _);
// }


// static class SessionTokenStore
// {
//     public record Entry(
//         string Username,
//         string AccessToken,
//         string RefreshToken,
//         DateTimeOffset AccessExpires);

//     private static readonly ConcurrentDictionary<string, Entry> _map = new();

//     public static void Save(string sid, string user, string access, string refresh, DateTimeOffset exp) =>
//         _map[sid] = new Entry(user, access, refresh, exp);

//     public static void Remove(string sid) => _map.TryRemove(sid, out _);

//     public static async Task<string?> GetValidAccessAsync(string sid, IHttpClientFactory http)
//     {
//         if (!_map.TryGetValue(sid, out var entry)) return null;

//         // +30 сек запас — не отдавать почти истёкший токен
//         if (entry.AccessExpires > DateTimeOffset.UtcNow.AddSeconds(30))
//             return entry.AccessToken;

//         // refresh
//         var client = http.CreateClient("api");
//         var resp = await client.PostAsJsonAsync("/token/refresh",
//             new { RefreshToken = entry.RefreshToken });
//         if (!resp.IsSuccessStatusCode)
//         {
//             _map.TryRemove(sid, out _);
//             return null;
//         }

//         var tokens = await resp.Content.ReadFromJsonAsync<TokenResponse>();
//         if (tokens is null)
//         {
//             _map.TryRemove(sid, out _);
//             return null;
//         }

//         _map[sid] = entry with
//         {
//             AccessToken = tokens.AccessToken,
//             RefreshToken = tokens.RefreshToken,
//             AccessExpires = DateTimeOffset.UtcNow.AddSeconds(tokens.ExpiresIn)
//         };
//         return tokens.AccessToken;
//     }
// }

// CookiesAndBearer

var builder = WebApplication.CreateBuilder(args);

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? "SUPER_SECRET_KEY_AT_LEAST_32_BYTES_LONG_1234";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "https://myapp.example";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "myapp.api";

builder.Services
    .AddAuthentication(o =>
    {
        o.DefaultScheme = "Smart";
        o.DefaultChallengeScheme = "Smart";
    })
    .AddPolicyScheme("Smart", "Smart", o =>
    {
        o.ForwardDefaultSelector = ctx =>
            ctx.Request.Headers.Authorization.ToString()
                .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? JwtBearerDefaults.AuthenticationScheme
                : CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, o =>
    {
        o.Cookie.Name = ".Hybrid.Auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        o.LoginPath = "/login";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = jwtIssuer,
            ValidateAudience = true, ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("Html", p => p
        .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser());

    o.AddPolicy("Api", p => p
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser());
});

builder.Services.AddAntiforgery();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();


app.MapGet("/csrf", (HttpContext ctx, IAntiforgery af) =>
{
    var pair = af.GetAndStoreTokens(ctx);
    return Results.Ok(new { csrf = pair.RequestToken });
});

app.MapPost("/login", async (HttpContext ctx, IAntiforgery af, LoginDto dto) =>
{
    await af.ValidateRequestAsync(ctx);
    if (!UserStore.Verify(dto.Username, dto.Password)) return Results.Unauthorized();

    var user = UserStore.Users[dto.Username];
    var claims = new List<Claim>
    {
        new(ClaimTypes.Name, user.Username),
        new(ClaimTypes.NameIdentifier, user.Username),
        new("sid", Guid.NewGuid().ToString("N"))
    };
    claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r)));

    await ctx.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));

    return Results.Ok(new { user = user.Username });
});

app.MapPost("/logout", async (HttpContext ctx, IAntiforgery af) =>
{
    await af.ValidateRequestAsync(ctx);
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok();
});


app.MapPost("/token", (TokenRequest req) =>
{
    if (!UserStore.Verify(req.Username, req.Password)) return Results.Unauthorized();
    var user = UserStore.Users[req.Username];
    var access = TokenService.CreateAccessToken(
        user.Username, user.Roles, jwtKey, jwtIssuer, jwtAudience,
        TimeSpan.FromMinutes(15));
    return Results.Ok(new
    {
        access_token = access,
        token_type = "Bearer",
        expires_in = 900
    });
});

app.MapGet("/dashboard", (HttpContext ctx) => Results.Ok(new
{
    mode = "html",
    user = ctx.User.Identity!.Name
})).RequireAuthorization("Html");

app.MapGet("/api/data", (HttpContext ctx) => Results.Ok(new
{
    mode = "api",
    sub = ctx.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? ctx.User.Identity!.Name,
    roles = ctx.User.FindAll("role").Select(c => c.Value)
        .Concat(ctx.User.FindAll(ClaimTypes.Role).Select(c => c.Value))
        .Distinct()
})).RequireAuthorization("Api");

// "Smart": автоматически пустит по любой из двух схем
app.MapGet("/any", (HttpContext ctx) =>
{
    var scheme = ctx.User.Identity?.AuthenticationType ?? "none";
    return Results.Ok(new { scheme, user = ctx.User.Identity?.Name });
}).RequireAuthorization();

app.MapGet("/admin", () => Results.Ok("admin area"))
   .RequireAuthorization(p => p.RequireRole("admin"));

app.Run();


record LoginDto(string Username, string Password);
record TokenRequest(string Username, string Password);
record User(string Username, string PasswordHash, string[] Roles);

static class UserStore
{
    public static readonly Dictionary<string, User> Users = new()
    {
        ["alice"] = new User("alice", Hash("p@ss"), new[] { "user", "admin" }),
        ["bob"]   = new User("bob",   Hash("p@ss"), new[] { "user" })
    };

    public static string Hash(string s) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

    public static bool Verify(string u, string p)
    {
        if (!Users.TryGetValue(u, out var user)) return false;
        var a = Encoding.UTF8.GetBytes(user.PasswordHash);
        var b = Encoding.UTF8.GetBytes(Hash(p));
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}

static class TokenService
{
    public static string CreateAccessToken(
        string username,
        IEnumerable<string> roles,
        string key,
        string issuer,
        string audience,
        TimeSpan lifetime)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };
        claims.AddRange(roles.Select(r => new Claim("role", r)));

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var creds = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}