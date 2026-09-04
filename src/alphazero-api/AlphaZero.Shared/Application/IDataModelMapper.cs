namespace AlphaZero.Shared.Application;

/// <summary>
/// Maps between a rich domain model and its flat persistence (data) model.
/// Implement per-aggregate to control how domain state serializes to/from the DB.
/// </summary>
public interface IDataModelMapper<TDomainModel, TDataModel>
    where TDomainModel : Domain.Entity
    where TDataModel : class
{
    TDomainModel ToDomain(TDataModel dataModel);
    TDataModel ToData(TDomainModel domainModel);

    /// <summary>
    /// Apply changes from the domain model onto an existing (tracked) data model.
    /// Used for Updates to avoid re-creating the EF entity (preserves change tracking on the data model side).
    /// </summary>
    void ApplyChanges(TDomainModel domainModel, TDataModel existingDataModel);
}
