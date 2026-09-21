using Menulux.Api.DTOs;
using Menulux.Api.Models;
using Menulux.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Menulux.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TokenService _tokenService;

    public AuthController(UserManager<ApplicationUser> userManager, TokenService tokenService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new { message = "E-posta veya şifre hatalı." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = _tokenService.CreateToken(user, roles);

        return Ok(new LoginResponse(token, user.Email!, user.FullName, user.RestaurantId, roles));
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpPost("register-restaurant-admin")]
    public async Task<IActionResult> RegisterRestaurantAdmin(RegisterRestaurantAdminRequest request)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            RestaurantId = request.RestaurantId,
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        await _userManager.AddToRoleAsync(user, "RestaurantAdmin");
        return Ok(new { user.Id, user.Email });
    }

    private static readonly string[] AllowedStaffRoles = ["Kitchen", "Waiter", "Cashier"];

    [Authorize(Roles = "RestaurantAdmin")]
    [HttpPost("register-staff")]
    public async Task<IActionResult> RegisterStaff(RegisterStaffRequest request)
    {
        if (!AllowedStaffRoles.Contains(request.Role))
        {
            return BadRequest(new { message = "Geçersiz rol." });
        }

        var restaurantIdClaim = User.FindFirst("restaurantId")?.Value;
        if (!Guid.TryParse(restaurantIdClaim, out var restaurantId))
        {
            return Forbid();
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            RestaurantId = restaurantId,
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        await _userManager.AddToRoleAsync(user, request.Role);
        return Ok(new { user.Id, user.Email, request.Role });
    }
}
