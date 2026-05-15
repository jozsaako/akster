using backend.Identity.Dtos;
using backend.Identity.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace backend.Identity.Services
{
    public interface IAuthService
    {
        Task<AuthResponse> RegisterAsync(RegisterRequest request);
        Task<AuthResponse> LoginAsync(LoginRequest request);
        Task<AuthResponse> RefreshAsync(RefreshRequest request);
        Task<bool> RevokeRefreshTokenAsync(string refreshToken);
    }

    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AuthService> _logger;
        private readonly IConfiguration _configuration;

        public AuthService(AppDbContext context, ILogger<AuthService> logger, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Email) ||
                    string.IsNullOrWhiteSpace(request.Password) ||
                    string.IsNullOrWhiteSpace(request.FirstName) ||
                    string.IsNullOrWhiteSpace(request.LastName))
                {
                    return new AuthResponse { Success = false, Message = "All fields are required." };
                }

                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
                if (existingUser != null)
                {
                    return new AuthResponse { Success = false, Message = "User with this email already exists." };
                }

                if (!IsValidEmail(request.Email))
                {
                    return new AuthResponse { Success = false, Message = "Invalid email format." };
                }

                if (request.Password.Length < 8)
                {
                    return new AuthResponse { Success = false, Message = "Password must be at least 8 characters long." };
                }

                var passwordHash = HashPassword(request.Password);

                var user = new User
                {
                    Email = request.Email,
                    PasswordHash = passwordHash,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsEmailConfirmed = false
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"User registered successfully: {user.Email}");

                // create refresh token
                var refreshToken = CreateRefreshTokenString();
                var refreshExpiresDays = _configuration.GetSection("Jwt").GetValue<int?>("RefreshTokenExpiresDays") ?? 7;
                var refresh = new RefreshToken
                {
                    UserId = user.Id,
                    Token = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddDays(refreshExpiresDays),
                    IsRevoked = false
                };

                _context.RefreshTokens.Add(refresh);
                await _context.SaveChangesAsync();

                return new AuthResponse
                {
                    Success = true,
                    Message = "Registration successful.",
                    User = new UserDto
                    {
                        Id = user.Id,
                        Email = user.Email,
                        FirstName = user.FirstName,
                        LastName = user.LastName
                    },
                    Token = GenerateJwtToken(user),
                    RefreshToken = refreshToken
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during registration: {ex.Message}");
                return new AuthResponse { Success = false, Message = "An error occurred during registration." };
            }
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return new AuthResponse { Success = false, Message = "Email and password are required." };
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
                if (user == null)
                {
                    return new AuthResponse { Success = false, Message = "Invalid email or password." };
                }

                if (!VerifyPassword(request.Password, user.PasswordHash))
                {
                    return new AuthResponse { Success = false, Message = "Invalid email or password." };
                }

                _logger.LogInformation($"User logged in successfully: {user.Email}");

                return new AuthResponse
                {
                    Success = true,
                    Message = "Login successful.",
                    User = new UserDto
                    {
                        Id = user.Id,
                        Email = user.Email,
                        FirstName = user.FirstName,
                        LastName = user.LastName
                    },
                    Token = GenerateJwtToken(user),
                    RefreshToken = await CreateAndSaveRefreshTokenAsync(user.Id)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during login: {ex.Message}");
                return new AuthResponse { Success = false, Message = "An error occurred during login." };
            }
        }

        private string CreateRefreshTokenString()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        private async Task<string> CreateAndSaveRefreshTokenAsync(int userId)
        {
            try
            {
                var token = CreateRefreshTokenString();
                var refreshExpiresDays = _configuration.GetSection("Jwt").GetValue<int?>("RefreshTokenExpiresDays") ?? 7;

                var refresh = new RefreshToken
                {
                    UserId = userId,
                    Token = token,
                    ExpiresAt = DateTime.UtcNow.AddDays(refreshExpiresDays),
                    IsRevoked = false
                };

                _context.RefreshTokens.Add(refresh);
                await _context.SaveChangesAsync();

                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating refresh token: {ex.Message}");
                return string.Empty;
            }
        }

        public async Task<AuthResponse> RefreshAsync(RefreshRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.RefreshToken))
                {
                    return new AuthResponse { Success = false, Message = "Refresh token is required." };
                }

                var stored = await _context.RefreshTokens.Include(r => r.User).FirstOrDefaultAsync(r => r.Token == request.RefreshToken);
                if (stored == null || stored.IsRevoked || stored.ExpiresAt <= DateTime.UtcNow)
                {
                    return new AuthResponse { Success = false, Message = "Invalid or expired refresh token." };
                }

                var user = stored.User;
                if (user == null)
                {
                    return new AuthResponse { Success = false, Message = "Invalid refresh token (user not found)." };
                }

                stored.IsRevoked = true;
                await _context.SaveChangesAsync();

                var newJwt = GenerateJwtToken(user);
                var newRefresh = await CreateAndSaveRefreshTokenAsync(user.Id);

                return new AuthResponse
                {
                    Success = true,
                    Message = "Token refreshed.",
                    User = new UserDto { Id = user.Id, Email = user.Email, FirstName = user.FirstName, LastName = user.LastName },
                    Token = newJwt,
                    RefreshToken = newRefresh
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during token refresh: {ex.Message}");
                return new AuthResponse { Success = false, Message = "An error occurred during token refresh." };
            }
        }

        public async Task<bool> RevokeRefreshTokenAsync(string refreshToken)
        {
            try
            {
                if (string.IsNullOrEmpty(refreshToken)) return false;

                var stored = await _context.RefreshTokens.FirstOrDefaultAsync(r => r.Token == refreshToken);
                if (stored == null) return false;

                if (stored.IsRevoked) return false;

                stored.IsRevoked = true;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error revoking refresh token: {ex.Message}");
                return false;
            }
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        private bool VerifyPassword(string password, string hash)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashOfInput = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                var hashBytes = Convert.FromBase64String(hash);
                return hashOfInput.SequenceEqual(hashBytes);
            }
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private string? GenerateJwtToken(User user)
        {
            try
            {
                var jwtSection = _configuration.GetSection("Jwt");
                var key = jwtSection.GetValue<string>("Key");
                var issuer = jwtSection.GetValue<string>("Issuer");
                var audience = jwtSection.GetValue<string>("Audience");
                var expiresMinutes = jwtSection.GetValue<int?>("ExpiresMinutes") ?? 60;

                if (string.IsNullOrEmpty(key)) return null;

                var securityKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
                var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(securityKey, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

                var claims = new[] {
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, user.Email),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.GivenName, user.FirstName),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Surname, user.LastName)
                };

                var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
                    issuer: issuer,
                    audience: audience,
                    claims: claims,
                    expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
                    signingCredentials: credentials
                );

                return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating JWT: {ex.Message}");
                return null;
            }
        }
    }
}
