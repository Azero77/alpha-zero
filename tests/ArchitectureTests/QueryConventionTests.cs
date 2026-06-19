using System.Reflection;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Queries;
using AlphaZero.Shared.Infrastructure.Repositores;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace AlphaZero.ArchitectureTests;

public class QueryConventionTests
{
    private static readonly Assembly[] Assemblies = 
    [
        typeof(AlphaZero.Modules.VideoUploading.Application.IVideoUploadingApplicationMarker).Assembly,
        typeof(AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourse.GetCourseQuery).Assembly,
        typeof(AlphaZero.Modules.Assessments.Application.IAssessmentsApplicationMarker).Assembly,
        typeof(AlphaZero.Modules.Library.Application.Libraries.Queries.ListLibraries.ListLibrariesQuery).Assembly,
        typeof(AlphaZero.Modules.Tenants.Application.Tenants.Queries.ListTenants.ListTenantsQuery).Assembly,
        // Add other module application markers here as they are created
    ];

    [Fact]
    public void DomainLayer_ShouldNot_ReferenceApplicationOrInfrastructure()
    {
        var domainAssemblies = new[]
        {
            typeof(AlphaZero.Modules.VideoUploading.Domain.Models.VideoSecret).Assembly,
            typeof(AlphaZero.Modules.Courses.Domain.Aggregates.Courses.Course).Assembly,
            typeof(AlphaZero.Modules.Assessments.Domain.Aggregates.Submissions.AssessmentSubmission).Assembly,
            typeof(AlphaZero.Modules.Library.Domain.Library).Assembly,
            typeof(AlphaZero.Modules.Tenants.Domain.Tenant).Assembly,
        };

        foreach (var assembly in domainAssemblies)
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOn("AlphaZero.Modules.*.Application")
                .And()
                .HaveDependencyOn("AlphaZero.Modules.*.Infrastructure")
                .GetResult();

            result.IsSuccessful.Should().BeTrue($"Domain layer in {assembly.GetName().Name} must not reference Application or Infrastructure layers.");
        }
    }

    [Fact]
    public void CommandHandlers_ShouldNot_ReferenceQueryServices()
    {
        foreach (var assembly in Assemblies)
        {
            var result = Types.InAssembly(assembly)
                .That()
                .ImplementInterface(typeof(MediatR.IRequestHandler<,>))
                .And()
                .HaveNameEndingWith("CommandHandler")
                .ShouldNot()
                .HaveDependencyOn("IQueryService")
                .GetResult();

            result.IsSuccessful.Should().BeTrue($"Command handlers in {assembly.GetName().Name} must not reference IQueryService interfaces.");
        }
    }

    [Fact]
    public void QueryHandlers_ShouldNot_ReferenceRepositories()
    {
        foreach (var assembly in Assemblies)
        {
            var result = Types.InAssembly(assembly)
                .That()
                .ImplementInterface(typeof(MediatR.IRequestHandler<,>))
                .And()
                .HaveNameEndingWith("QueryHandler")
                .ShouldNot()
                .HaveDependencyOn("IRepository")
                .GetResult();

            result.IsSuccessful.Should().BeTrue($"Query handlers in {assembly.GetName().Name} must not reference IRepository interfaces.");
        }
    }
}
