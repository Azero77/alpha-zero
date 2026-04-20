using AlphaZero.Modules.Library.Domain;
using AlphaZero.Modules.Library.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Library.Infrastructure.Repositories;

public class LibraryRepository : BaseRepository<AppDbContext, Domain.Library>, ILibraryRepository
{
    public LibraryRepository(AppDbContext context) : base(context)
    {
    }
}
