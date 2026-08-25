using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace BaseCore.AuthService.Controllers
{
    /// <summary>
    /// Roles API Controller
    /// Teaching: Role-based Authorization (Bài 10, 11)
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class RolesController : ControllerBase
    {
        // Static list of available roles
        // In production, this would be stored in database
        private static readonly List<RoleDto> _roles = new()
        {
            new RoleDto { Id = 1, Name = "Admin", Description = "Administrator with full access", UserType = 1 },
            new RoleDto { Id = 2, Name = "User", Description = "Regular user with limited access", UserType = 0 },
            new RoleDto { Id = 3, Name = "Manager", Description = "Manager with moderate access", UserType = 2 }
        };

        /// <summary>
        /// Get all roles
        /// </summary>
        [HttpGet]
        public IActionResult GetAll()
        {
            return Ok(_roles);
        }

        /// <summary>
        /// Get role by ID
        /// </summary>
        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            var role = _roles.Find(r => r.Id == id);
            if (role == null)
                return NotFound(new { message = "Role not found" });

            return Ok(role);
        }

        /// <summary>
        /// Get role by UserType
        /// </summary>
        [HttpGet("by-usertype/{userType}")]
        public IActionResult GetByUserType(int userType)
        {
            var role = _roles.Find(r => r.UserType == userType);
            if (role == null)
                return NotFound(new { message = "Role not found for this UserType" });

            return Ok(role);
        }

        /// <summary>
        /// Get permissions for a role
        /// </summary>
        [HttpGet("{id}/permissions")]
        public IActionResult GetPermissions(int id)
        {
            var role = _roles.Find(r => r.Id == id);
            if (role == null)
                return NotFound(new { message = "Role not found" });

            // Define permissions based on role
            var permissions = role.UserType switch
            {
                1 => new[] { "users.read", "users.write", "users.delete", "products.read", "products.write", "products.delete", "orders.read", "orders.write", "orders.delete", "categories.read", "categories.write", "categories.delete", "roles.read", "roles.write" },
                2 => new[] { "users.read", "products.read", "products.write", "orders.read", "orders.write", "categories.read" },
                _ => new[] { "products.read", "orders.read", "categories.read" }
            };

            return Ok(new
            {
                role = role.Name,
                permissions
            });
        }
    }

    public class RoleDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public int UserType { get; set; }
    }
}
