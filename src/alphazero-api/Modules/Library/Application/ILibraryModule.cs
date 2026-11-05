namespace AlphaZero.Modules.Library.Application;

/// <summary>
/// Application-owned module handle (issue #59). Lets infrastructure components such as the
/// analytics refresh worker resolve scoped services from the Library module's own container
/// (mirrors <c>ICoursesModule</c>).
/// </summary>
public interface ILibraryModule : IModule
{
}
