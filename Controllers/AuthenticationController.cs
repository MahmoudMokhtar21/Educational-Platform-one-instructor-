using Educatinal_Platform.DTOs;
using Educatinal_Platform.Models;
using Educatinal_Platform.Options;
using Educatinal_Platform.Services;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

[Route("api/[controller]")]
[ApiController]
public class AuthenticationController : ControllerBase
{
    private readonly IMongoCollection<User> _usersCollection;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;
    private readonly IFileService _fileService;
    private readonly ILogger<AuthenticationController> _logger;
    private readonly IAccessTokenService _accessTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly RefreshTokenOptions _refreshTokenOptions;

    public AuthenticationController(
        IMongoDatabase database,
        IConfiguration configuration,
        IEmailService emailService,
        IFileService fileService,
        ILogger<AuthenticationController> logger,
        IAccessTokenService accessTokenService,
        IRefreshTokenService refreshTokenService,
        IOptions<RefreshTokenOptions> refreshTokenOptions)
    {
        _usersCollection = database.GetCollection<User>("Users");
        _configuration = configuration;
        _emailService = emailService;
        _fileService = fileService;
        _logger = logger;
        _accessTokenService = accessTokenService;
        _refreshTokenService = refreshTokenService;
        _refreshTokenOptions = refreshTokenOptions.Value;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDTO dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        // Check if user already exists by email
        var filter = Builders<User>.Filter.Eq(x => x.Email, email);
        var isExisting = await _usersCollection.Find(filter).AnyAsync();

        if (isExisting)
            return BadRequest(new { message = "Email is already registered" });

        var user = new User
        {
            Email = email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Bio = dto.Bio,
            ProfilePictureUrl = dto.ProfilePictureUrl,
            Role = "student", // Default role
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = false,
            EmailVerificationToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32)),
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24)
    
        };

        var passwordHasher = new PasswordHasher<User>();
        user.PasswordHash = passwordHasher.HashPassword(user, dto.Password);

        var verificationLink = CreateVerificationLink(user.EmailVerificationToken!);
        if (verificationLink == null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Email verification is not configured. Please try again later." });

        await _usersCollection.InsertOneAsync(user);
        if (!await _emailService.SendEmailVerificationAsync(user.Email, verificationLink))
        {
            await _usersCollection.DeleteOneAsync(Builders<User>.Filter.Eq(existingUser => existingUser.Id, user.Id));
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "We could not send the verification email. Please try again later." });
        }

        return Ok(new
        {
            message = "User registered successfully",
            userId = user.Id,
            email = user.Email
        });
    }

    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification([FromBody] string email, CancellationToken cancellationToken)
    {
        const string genericMessage = "If that account needs verification, a new verification email has been sent.";
        if (string.IsNullOrWhiteSpace(email)) return Ok(new { message = genericMessage });

        var user = await _usersCollection.Find(Builders<User>.Filter.Eq(candidate => candidate.Email, email.Trim().ToLowerInvariant())).FirstOrDefaultAsync(cancellationToken);
        if (user == null || user.IsEmailVerified) return Ok(new { message = genericMessage });

        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var verificationLink = CreateVerificationLink(token);
        if (verificationLink == null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Email verification is not configured. Please try again later." });

        if (!await _emailService.SendEmailVerificationAsync(user.Email, verificationLink, cancellationToken))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "We could not send the verification email. Please try again later." });

        var update = Builders<User>.Update
            .Set(candidate => candidate.EmailVerificationToken, token)
            .Set(candidate => candidate.EmailVerificationTokenExpiry, DateTime.UtcNow.AddHours(24));
        await _usersCollection.UpdateOneAsync(candidate => candidate.Id == user.Id, update, cancellationToken: cancellationToken);
        return Ok(new { message = genericMessage });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDTO dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var filter = Builders<User>.Filter.Eq(x => x.Email, email);
        var user = await _usersCollection.Find(filter).FirstOrDefaultAsync();

        if (user == null)
            return Unauthorized(new { message = "Invalid email or password" });

        var passwordHasher = new PasswordHasher<User>();
        var result = passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            dto.Password);

        if (result == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Invalid email or password" });
        if (!user.IsEmailVerified)
        {
            return Unauthorized(new
            {
                message = "Please verify your email before logging in."
            });
        }

        // Update last login
        var updateFilter = Builders<User>.Filter.Eq(x => x.Id, user.Id);
        var updateDefinition = Builders<User>.Update
            .Set(x => x.LastLoginAt, DateTime.UtcNow);
        await _usersCollection.UpdateOneAsync(updateFilter, updateDefinition);

        var refreshToken = await _refreshTokenService.IssueAsync(user.Id, HttpContext.RequestAborted);
        AppendRefreshCookie(refreshToken.Token, refreshToken.ExpiresAt);
        Response.Headers.CacheControl = "no-store";

        var accessToken = _accessTokenService.Create(user);
        return Ok(ToAuthResponse(user, accessToken, refreshToken.CsrfToken));
    }

    [HttpPost("refresh")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(_refreshTokenOptions.CookieName, out var rawRefreshToken) ||
            string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return Unauthorized(new { message = "Your session has expired. Please sign in again." });
        }

        var csrfToken = Request.Headers["X-CSRF-Token"].ToString();
        var rotated = await _refreshTokenService.RotateAsync(rawRefreshToken, csrfToken, cancellationToken);
        if (rotated is null)
            return Unauthorized(new { message = "Your session has expired. Please sign in again." });

        var user = await _usersCollection.Find(candidate => candidate.Id == rotated.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null || !user.IsEmailVerified)
        {
            await _refreshTokenService.RevokeAllForUserAsync(rotated.UserId, "Account is no longer eligible for refresh.", cancellationToken);
            ClearRefreshCookie();
            return Unauthorized(new { message = "Your session has expired. Please sign in again." });
        }

        AppendRefreshCookie(rotated.Token, rotated.ExpiresAt);
        Response.Headers.CacheControl = "no-store";
        return Ok(ToAuthResponse(user, _accessTokenService.Create(user), rotated.CsrfToken));
    }

    [HttpPost("logout")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(_refreshTokenOptions.CookieName, out var rawRefreshToken) ||
            string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return NoContent();
        }

        var csrfToken = Request.Headers["X-CSRF-Token"].ToString();
        var wasRevoked = await _refreshTokenService.RevokeAsync(rawRefreshToken, csrfToken, "Logout", cancellationToken);
        if (!wasRevoked)
            return Unauthorized(new { message = "Your session has expired. Please sign in again." });

        ClearRefreshCookie();
        return NoContent();
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> forgotPassword([FromBody] string email, CancellationToken cancellationToken)
    {
        const string genericMessage = "If the email exists, a reset link will be sent";
        if (string.IsNullOrWhiteSpace(email))
            return Ok(new { message = genericMessage });

        var filter = Builders<User>.Filter.Eq(x=>x.Email, email);
        var user = await _usersCollection.Find(filter).FirstOrDefaultAsync();

        if(user == null)
            return Ok(new { message = genericMessage });

        var frontendUrl = _configuration["Frontend:BaseUrl"]?.TrimEnd('/');
        if (!Uri.TryCreate(frontendUrl, UriKind.Absolute, out _))
        {
            _logger.LogError("Password-reset email was not sent because Frontend:BaseUrl is missing or invalid.");
            return Ok(new { message = genericMessage });
        }

        var resetToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var resetLink = $"{frontendUrl}/reset-password?token={Uri.EscapeDataString(resetToken)}";
        var wasSent = await _emailService.SendPasswordResetAsync(user.Email, resetLink, cancellationToken);
        if (!wasSent)
            return Ok(new { message = genericMessage });

        var updateFilter =  Builders<User>.Filter.Eq(x => x.Id, user.Id);
        var updateDefination = Builders<User>.Update.
            Set(x => x.PasswordResetToken, resetToken).
            Set(x => x.PasswordResetTokenExpiry, DateTime.UtcNow.AddHours(1));
        await _usersCollection.UpdateOneAsync(updateFilter, updateDefination);

        return Ok(new { message = genericMessage });
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] string token)
    {
        var filter = Builders<User>.Filter.Eq(x => x.EmailVerificationToken, token);
        var user = await _usersCollection.Find(filter).FirstOrDefaultAsync();

        if (user == null)
            return BadRequest(new { message = "Invalid verification token" });

        if (!user.EmailVerificationTokenExpiry.HasValue ||
            user.EmailVerificationTokenExpiry.Value < DateTime.UtcNow)
        {
            return BadRequest(new
            {
                message = "Verification token has expired."
            });
        }

        var updateFilter = Builders<User>.Filter.Eq(x => x.Id, user.Id);
        var updateDefinition = Builders<User>.Update
            .Set(x => x.IsEmailVerified, true)
            .Set(x => x.EmailVerificationToken, null)
            .Set( x => x.EmailVerificationTokenExpiry,null);
    
   

        await _usersCollection.UpdateOneAsync(updateFilter, updateDefinition);

        return Ok(new { message = "Email verified successfully" });
    }

    private string? CreateVerificationLink(string token)
    {
        var frontendUrl = _configuration["Frontend:BaseUrl"]?.TrimEnd('/');
        if (!Uri.TryCreate(frontendUrl, UriKind.Absolute, out _))
        {
            _logger.LogError("Verification email was not sent because Frontend:BaseUrl is missing or invalid.");
            return null;
        }

        return $"{frontendUrl}/verify-email?token={Uri.EscapeDataString(token)}";
    }
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
     [FromBody] ResetPasswordDTO dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Token))
        {
            return BadRequest(new
            {
                message = "Reset token is required."
            });
        }

        if (string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            return BadRequest(new
            {
                message = "New password is required."
            });
        }

        if (dto.NewPassword.Length < 6)
        {
            return BadRequest(new
            {
                message = "Password must be at least 6 characters."
            });
        }

        var filter =
            Builders<User>.Filter.Eq(
                x => x.PasswordResetToken,
                dto.Token);

        var user =
            await _usersCollection
                .Find(filter)
                .FirstOrDefaultAsync();

        if (user == null)
        {
            return BadRequest(new
            {
                message = "Invalid reset token."
            });
        }

        if (!user.PasswordResetTokenExpiry.HasValue ||
            user.PasswordResetTokenExpiry.Value < DateTime.UtcNow)
        {
            return BadRequest(new
            {
                message = "Reset token has expired."
            });
        }

        var passwordHasher =
            new PasswordHasher<User>();

        var newPasswordHash =
            passwordHasher.HashPassword(
                user,
                dto.NewPassword);

        var updateFilter =
            Builders<User>.Filter.Eq(
                x => x.Id,
                user.Id);

        var updateDefinition =
            Builders<User>.Update
                .Set(
                    x => x.PasswordHash,
                    newPasswordHash)
                .Set(
                    x => x.PasswordResetToken,
                    null)
                .Set(
                    x => x.PasswordResetTokenExpiry,
                    null);

        await _usersCollection.UpdateOneAsync(
            updateFilter,
            updateDefinition);
        await _refreshTokenService.RevokeAllForUserAsync(user.Id, "Password reset", HttpContext.RequestAborted);

        return Ok(new
        {
            message = "Password reset successfully."
        });
    }

    [Authorize]
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var user = await GetAuthenticatedUserAsync();
        return user == null ? Unauthorized() : Ok(ToProfileResponse(user));
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FirstName) || string.IsNullOrWhiteSpace(dto.LastName))
            return BadRequest(new { message = "First and last name are required." });
        if (dto.FirstName.Trim().Length > 100 || dto.LastName.Trim().Length > 100 || dto.Bio?.Length > 1000)
            return BadRequest(new { message = "One or more profile fields are too long." });

        var user = await GetAuthenticatedUserAsync();
        if (user == null) return Unauthorized();

        var bio = string.IsNullOrWhiteSpace(dto.Bio) ? null : dto.Bio.Trim();
        var update = Builders<User>.Update
            .Set(candidate => candidate.FirstName, dto.FirstName.Trim())
            .Set(candidate => candidate.LastName, dto.LastName.Trim())
            .Set(candidate => candidate.Bio, bio);

        // The text-profile endpoint intentionally leaves an existing uploaded
        // photo untouched unless a legacy caller explicitly provides this field.
        if (dto.ProfilePictureUrl is not null)
        {
            var legacyPhotoUrl = string.IsNullOrWhiteSpace(dto.ProfilePictureUrl) ? null : dto.ProfilePictureUrl.Trim();
            update = update.Set(candidate => candidate.ProfilePictureUrl, legacyPhotoUrl);
            user.ProfilePictureUrl = legacyPhotoUrl;
        }

        await _usersCollection.UpdateOneAsync(candidate => candidate.Id == user.Id, update);
        user.FirstName = dto.FirstName.Trim();
        user.LastName = dto.LastName.Trim();
        user.Bio = bio;
        return Ok(ToProfileResponse(user));
    }

    [Authorize]
    [HttpPost("profile/photo")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadProfilePhoto([FromForm] ProfilePhotoUploadDto dto)
    {
        var file = dto.File;
        if (file is null)
            return BadRequest(new { message = "A profile image file is required." });

        var user = await GetAuthenticatedUserAsync();
        if (user is null) return Unauthorized();

        string? uploadedPhotoUrl = null;
        try
        {
            uploadedPhotoUrl = await _fileService.UploadImageAsync(file, "profiles");
            var result = await _usersCollection.UpdateOneAsync(
                candidate => candidate.Id == user.Id,
                Builders<User>.Update.Set(candidate => candidate.ProfilePictureUrl, uploadedPhotoUrl),
                cancellationToken: HttpContext.RequestAborted);

            if (result.MatchedCount != 1)
            {
                await DeleteFileQuietlyAsync(uploadedPhotoUrl);
                return Unauthorized();
            }
        }
        catch
        {
            if (uploadedPhotoUrl is not null)
                await DeleteFileQuietlyAsync(uploadedPhotoUrl);
            throw;
        }

        var previousPhotoUrl = user.ProfilePictureUrl;
        user.ProfilePictureUrl = uploadedPhotoUrl;
        if (!string.Equals(previousPhotoUrl, uploadedPhotoUrl, StringComparison.Ordinal))
            await DeleteFileQuietlyAsync(previousPhotoUrl);

        return Ok(ToProfileResponse(user));
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CurrentPassword) || string.IsNullOrWhiteSpace(dto.NewPassword) || string.IsNullOrWhiteSpace(dto.ConfirmNewPassword))
            return BadRequest(new { message = "Current password, new password, and password confirmation are required." });
        if (dto.NewPassword.Length < 6)
            return BadRequest(new { message = "New password must be at least 6 characters." });
        if (!string.Equals(dto.NewPassword, dto.ConfirmNewPassword, StringComparison.Ordinal))
            return BadRequest(new { message = "New password and confirmation do not match." });

        var user = await GetAuthenticatedUserAsync();
        if (user == null) return Unauthorized();
        var hasher = new PasswordHasher<User>();
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, dto.CurrentPassword) == PasswordVerificationResult.Failed)
            return BadRequest(new { message = "Current password is incorrect." });

        await _usersCollection.UpdateOneAsync(candidate => candidate.Id == user.Id,
            Builders<User>.Update.Set(candidate => candidate.PasswordHash, hasher.HashPassword(user, dto.NewPassword)));
        await _refreshTokenService.RevokeAllForUserAsync(user.Id, "Password changed", HttpContext.RequestAborted);
        ClearRefreshCookie();
        return Ok(new { message = "Password changed successfully." });
    }

    private async Task DeleteFileQuietlyAsync(string? fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl)) return;

        try
        {
            await _fileService.DeleteAsync(fileUrl);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not remove a superseded profile image.");
        }
    }

    private async Task<User?> GetAuthenticatedUserAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(userId)
            ? null
            : await _usersCollection.Find(candidate => candidate.Id == userId).FirstOrDefaultAsync();
    }

    private static object ToProfileResponse(User user) => new
    {
        userId = user.Id,
        email = user.Email,
        firstName = user.FirstName,
        lastName = user.LastName,
        bio = user.Bio,
        profilePictureUrl = user.ProfilePictureUrl,
        role = user.Role
    };

    private static object ToAuthResponse(User user, IssuedAccessToken accessToken, string csrfToken) => new
    {
        token = accessToken.Token,
        userId = user.Id,
        email = user.Email,
        firstName = user.FirstName,
        lastName = user.LastName,
        profilePictureUrl = user.ProfilePictureUrl,
        role = user.Role,
        expiresIn = (int)Math.Round((accessToken.ExpiresAt - DateTime.UtcNow).TotalSeconds),
        csrfToken
    };

    private void AppendRefreshCookie(string rawRefreshToken, DateTime expiresAt)
    {
        Response.Cookies.Append(_refreshTokenOptions.CookieName, rawRefreshToken, CreateRefreshCookieOptions(expiresAt));
    }

    private void ClearRefreshCookie()
    {
        Response.Cookies.Delete(_refreshTokenOptions.CookieName, CreateRefreshCookieOptions(DateTime.UnixEpoch));
    }

    private CookieOptions CreateRefreshCookieOptions(DateTime expiresAt) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = ResolveRefreshCookieSameSite(),
        Path = "/api/Authentication",
        IsEssential = true,
        Expires = new DateTimeOffset(expiresAt)
    };

    private SameSiteMode ResolveRefreshCookieSameSite() =>
        Enum.TryParse<SameSiteMode>(_refreshTokenOptions.CookieSameSite, true, out var sameSite)
            ? sameSite
            : SameSiteMode.Lax;
}
