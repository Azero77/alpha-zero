using Xunit;

namespace Courses.Tests.Integration.Abstractions;

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<ApiFactory>
{
}
