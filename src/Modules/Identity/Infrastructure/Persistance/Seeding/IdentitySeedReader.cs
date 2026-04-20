using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Infrastructure.Models;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AlphaZero.Modules.Identity.Infrastructure.Persistance.Seeding;

public static class IdentitySeedReader
{
    public static (List<PrincipalTemplate> principals, List<ManagedPolicy> managedPolicies, List<PrincipalPolicyAssignment> assignments) GetData()
    {
        // Explicitly get the assembly where the Domain models live
        var domainAssembly = typeof(PrincipalTemplate).Assembly;
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

        var principalTemplates = new List<PrincipalTemplate>();
        var assignments = new List<PrincipalPolicyAssignment>();

        foreach (var p in princpalTemplatesNode.AsArray())
        {
            var id = Guid.Parse(p!["Id"]!.GetValue<string>());
            var name = p["Name"]?.GetValue<string>() ?? "";
            var type = Enum.Parse<PrincipalType>(p["PrincipalType"]?.GetValue<string>() ?? "User");

            var principal = new PrincipalTemplate(id, name, type);
            principalTemplates.Add(principal);

            var policyNames = p["ManagedPolicies"]!.AsArray().Select(node => node!.GetValue<string>()).ToList();
            var matchedPolicies = managedPolicies.Where(mp => policyNames.Contains(mp.Name)).ToList();

            foreach (var policy in matchedPolicies)
            {
                assignments.Add(new PrincipalPolicyAssignment 
                { 
                    PrincipalId = id, 
                    ManagedPolicyId = policy.Id 
                });
            }
        }

        return (principalTemplates, managedPolicies, assignments);
    }
}
