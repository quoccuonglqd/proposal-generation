using Cherry.Core.Dtos;
using Cherry.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cherry.Api.Controllers
{
    [ApiController]
    [Route("api/v1/users")]
    [Authorize(Roles = "Admin")]
    public class UsersController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public UsersController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserDto>>> List()
        {
            var users = await _userManager.Users.ToListAsync();
            var result = new List<UserDto>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                result.Add(new UserDto(user.Id, user.Email!, user.DisplayName, roles));
            }

            return Ok(result);
        }

        [HttpPut("{id}/roles")]
        public async Task<IActionResult> UpdateRoles(Guid id, [FromBody] UpdateUserRolesRequest request)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null) return NotFound();

            var currentRoles = await _userManager.GetRolesAsync(user);
            
            // Remove from existing roles
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            
            // Add to new roles
            await _userManager.AddToRolesAsync(user, request.Roles);

            return NoContent();
        }
    }
}
