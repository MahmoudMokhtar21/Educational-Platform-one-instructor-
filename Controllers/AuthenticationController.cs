using Educatinal_Platform.DTOs;
using Educatinal_Platform.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

[Route("api/[controller]")]
[ApiController]
public class AuthenticationController : ControllerBase
{
    private readonly IMongoCollection<User> _usersCollection;
    private readonly IConfiguration _configuration;

    public AuthenticationController(IMongoDatabase database, IConfiguration configuration)
    {
        _usersCollection = database.GetCollection<User>("Users");
        _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDTO dto)
    {
        // Check if user already exists by email
        var filter = Builders<User>.Filter.Eq(x => x.Email, dto.Email);
        var isExisting = await _usersCollection.Find(filter).AnyAsync();

        if (isExisting)
            return BadRequest(new { message = "Email is already registered" });

        var user = new User
        {
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Bio = dto.Bio,
            ProfilePictureUrl = dto.ProfilePictureUrl,
            Role = "student", // Default role
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = false,
            EmailVerificationToken = Guid.NewGuid().ToString(),
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24)
    
        };

        var passwordHasher = new PasswordHasher<User>();
        user.PasswordHash = passwordHasher.HashPassword(user, dto.Password);

        await _usersCollection.InsertOneAsync(user);

        return Ok(new
        {
            message = "User registered successfully",
            userId = user.Id,
            email = user.Email
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDTO dto)
    {
        var filter = Builders<User>.Filter.Eq(x => x.Email, dto.Email);
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

        // Generate JWT token
        var claims = new List<Claim>()
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("FirstName", user.FirstName),
            new Claim("LastName", user.LastName)
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["JWT:SecretKey"]!));

        var creds = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["JWT:Issuer"],
            audience: _configuration["JWT:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                Convert.ToDouble(_configuration["JWT:ExpiryMinutes"] ?? "60")),
            signingCredentials: creds);

        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtToken = tokenHandler.WriteToken(token);

        return Ok(new
        {
            token = jwtToken,
            userId = user.Id,
            email = user.Email,
            firstName = user.FirstName,
            lastName = user.LastName,
            role = user.Role,
            expiresIn = Convert.ToInt32(_configuration["JWT:ExpiryMinutes"]) * 60 // in seconds
    });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> forgotPassword([FromBody] string email)
    {
        var filter = Builders<User>.Filter.Eq(x=>x.Email, email);
        var user = await _usersCollection.Find(filter).FirstOrDefaultAsync();

        if(user == null)
            return Ok(new { message = "If the email exists, a reset link will be sent" });
        var reserToken = Guid.NewGuid().ToString();
        var updateFilter =  Builders<User>.Filter.Eq(x => x.Id, user.Id);
        var updateDefination = Builders<User>.Update.
            Set(x => x.PasswordResetToken, reserToken).
            Set(x => x.PasswordResetTokenExpiry, DateTime.UtcNow.AddHours(1));
        await _usersCollection.UpdateOneAsync(updateFilter, updateDefination);

        // Send email with reset link
        // ... email logic here

        return Ok(new { message = "If the email exists, a reset link will be sent" });
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

        return Ok(new
        {
            message = "Password reset successfully."
        });
    }



}
