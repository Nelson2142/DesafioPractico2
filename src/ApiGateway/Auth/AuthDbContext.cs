using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Auth;

/// <summary>
/// Contexto de Identity. Al heredar de IdentityDbContext se obtienen las tablas
/// AspNetUsers, AspNetRoles, AspNetUserRoles, AspNetUserClaims, AspNetRoleClaims,
/// AspNetUserLogins y AspNetUserTokens.
/// </summary>
public class AuthDbContext(DbContextOptions<AuthDbContext> options)
    : IdentityDbContext<Usuario, IdentityRole, string>(options)
{
}
