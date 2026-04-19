using AlphaZero.Shared.Domain;
using ErrorOr;

namespace AlphaZero.Modules.Library.Domain;

public class Library : AggregateRoot, IDomainTenantOwned
{
    public string Name { get; private set; } = default!;
    public string Address { get; private set; } = default!;
    public string ContactNumber { get; private set; } = default!;
    public Guid TenantId { get; private set; }
    
    private readonly List<ResourcePattern> _allowedResources = new();
    public IReadOnlyCollection<ResourcePattern> AllowedResources => _allowedResources.AsReadOnly();

    private Library() { }

    private Library(Guid id, string name, string address, string contactNumber, Guid tenantId) : base(id)
    {
        Name = name;
        Address = address;
        ContactNumber = contactNumber;
        TenantId = tenantId;
    }

    public static Library Create(string name, string address, string contactNumber, Guid tenantId)
    {
        return new Library(Guid.NewGuid(), name, address, contactNumber, tenantId);
    }

    public void Update(string name, string address, string contactNumber)
    {
        Name = name;
        Address = address;
        ContactNumber = contactNumber;
    }

    public ErrorOr<Success> AuthorizeResource(ResourceArn resource)
    {
        var pattern = ResourcePattern.Create(resource.Value).Value;
        if (_allowedResources.Contains(pattern))
            return Error.Conflict("Library.ResourceAlreadyAuthorized", "Library is already authorized for this resource.");

        _allowedResources.Add(pattern);
        AddDomainEvent(new ResourceAccessAddedToLibraryDomainEvent(this.Id, pattern));
        return Result.Success;
    }

    public ErrorOr<Success> DeauthorizeResource(ResourceArn resource)
    {
        var pattern = ResourcePattern.Create(resource.Value).Value;

        if (!_allowedResources.Contains(pattern))
            return Error.NotFound("Library.ResourceNotAuthorized", "Library is not authorized for this resource.");

        _allowedResources.Remove(pattern);
        AddDomainEvent(new ResourceAccessRemovedFromLibraryDomainEvent(this.Id, pattern));
        return Result.Success;
    }
}

public class ResourceAccessAddedToLibraryDomainEvent(Guid LibraryId, ResourcePattern ResourcePattern) : DomainEvent()
{
    public Guid LibraryId { get; } = LibraryId;
    public ResourcePattern ResourcePattern { get; } = ResourcePattern;
}

public class ResourceAccessRemovedFromLibraryDomainEvent(Guid LibraryId, ResourcePattern ResourcePattern) : DomainEvent()
{
    public Guid LibraryId { get; } = LibraryId;
    public ResourcePattern ResourcePattern { get; } = ResourcePattern;
}