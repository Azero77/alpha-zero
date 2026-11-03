namespace AlphaZero.Shared.Application;

/// <summary>
/// Marker interface indicating that this command requires an isolated database transaction 
/// to support row-level pessimistic locking (e.g. SELECT FOR UPDATE).
/// </summary>
public interface ITransactionalCommand { }
