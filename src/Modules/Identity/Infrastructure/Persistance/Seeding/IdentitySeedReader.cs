using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;
using AlphaZero.Modules.Identity.Infrastructure.Models;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AlphaZero.Modules.Identity.Infrastructure.Persistance.Seeding;

public static class IdentitySeedReader
{
    public static (List<Principal> principals, List<ManagedPolicy> managedPolicies) GetData()
    {
        var domainAssembly = typeof(Principal).Assembly;
        var assemblyPath = Path.GetDirectoryName(domainAssembly.Location) ?? "";
        
        var managedPoliciesPath = Path.Combine(assemblyPath, "SeedData", "ManagedPolicies.json");
        var principalTemplatesPath = Path.Combine(assemblyPath, "SeedData", "PrincipalTemplates.json");

        if (!File.Exists(managedPoliciesPath))
        {
            managedPoliciesPath = Path.Combine(Directory.GetCurrentDirectory(), "src", "Modules", "Identity", "Domain", "SeedData", "ManagedPolicies.json");
            principalTemplatesPath = Path.Combine(Directory.GetCurrentDirectory(), "src", "Modules", "Identity", "Domain", "SeedData", "PrincipalTemplates.json");
        }

        var managedPoliciesJson = File.ReadAllText(managedPoliciesPath);
        var managedPolicies = JsonSerializer.Deserialize<List<ManagedPolicy>>(managedPoliciesJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

        var principalTemplateJson = File.ReadAllText(principalTemplatesPath);
        JsonNode? princpalTemplatesNode = JsonNode.Parse(principalTemplateJson);
        if (princpalTemplatesNode is null) throw new InvalidDataException("Could not parse Identity seed data.");

        var principals = new List<Principal>();

        foreach (var p in princpalTemplatesNode.AsArray())
        {
            var id = Guid.Parse(p!["Id"]!.GetValue<string>());
            var name = p["Name"]?.GetValue<string>() ?? "";
            var type = Enum.Parse<PrincipalType>(p["PrincipalType"]?.GetValue<string>() ?? "User", true);
            // Seeding global principals (roles) for now with null tenant and null scope
            var principalResult = Principal.Create(id, name.ToLowerInvariant(), "system-role", name, type, null, Guid.Empty);
            
            if (principalResult.IsError) continue;

            var principal = principalResult.Value;
            
            var policyNames = p["ManagedPolicies"]!.AsArray().Select(node => node!.GetValue<string>()).ToList();
            var matchedPolicies = managedPolicies.Where(mp => policyNames.Contains(mp.Name)).ToList();

            foreach (var policy in matchedPolicies)
            {
                principal.AddPolicy(policy);
            }
            
            principals.Add(principal);
        }

        return (principals, managedPolicies);
    }
}
