using BeenThere.Core.Interfaces;

namespace BeenThere.Infrastructure.Services;

/// <summary>
/// No-op stub for M3. The real Channel-based hosted service is wired in Milestone 6 (ADR-0008).
/// </summary>
public sealed class NullDuplicateDetectionChannel : IDuplicateDetectionChannel
{
    public void Enqueue(Guid routeId) { }
}
