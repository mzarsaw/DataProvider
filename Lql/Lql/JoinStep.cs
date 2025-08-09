using Selecta;

namespace Lql;

/// <summary>
/// Represents a JOIN operation (INNER, LEFT, CROSS, etc.).
/// </summary>
public sealed class JoinStep : StepBase
{
    /// <summary>
    /// Gets the join relationship containing table, condition, and join type.
    /// </summary>
    public required JoinRelationship JoinRelationship { get; init; }
}
