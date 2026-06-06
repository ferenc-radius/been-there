namespace BeenThere.Core.Interfaces;

/// <summary>
/// Enqueues a route ID for background duplicate detection after a successful import (ADR-0008).
/// M3 ships a no-op implementation; the real Channel-based implementation is wired in Milestone 6.
/// </summary>
public interface IDuplicateDetectionChannel
{
    /// <summary>Enqueues the given route for duplicate detection. Fire-and-forget.</summary>
    void Enqueue(Guid routeId);
}
