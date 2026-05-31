using AlphaZero.Modules.Identity.Domain.Models;

namespace AlphaZero.Modules.Identity.Infrastructure.Models;

public class JoiningTenantPrinicpal
{

    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? Role { get; set; }
    public string? Description { get; set; }
    public PrincipalTemplate AssignedPrincipal { get; set; } = null!; //could be Principal or Principal Template
}
