using Selecta;

namespace Lql;

/// <summary>
/// Shared pipeline processor that converts pipelines to SQL using any ISqlContext implementation.
/// Eliminates duplication between platform-specific extensions.
/// </summary>
public static class PipelineProcessor
{
    /// <summary>
    /// Converts a pipeline to SQL using the provided context
    /// </summary>
    /// <param name="pipeline">The pipeline to convert</param>
    /// <param name="context">The SQL context to use for generation</param>
    /// <param name="filterConditionProcessor">Optional filter condition processor for platform-specific transformations</param>
    /// <returns>SQL string</returns>
    public static string ConvertPipelineToSql(
        Pipeline pipeline,
        ISqlContext context,
        Func<string, string>? filterConditionProcessor = null
    )
    {
        if (pipeline.Steps.Count == 0)
        {
            return "-- Empty pipeline";
        }

        // Process each step in the pipeline
        foreach (var step in pipeline.Steps)
        {
            ProcessStep(step, context, filterConditionProcessor);
        }

        return context.GenerateSQL();
    }

    /// <summary>
    /// Processes a pipeline step and updates the SQL context
    /// </summary>
    /// <param name="step">The step to process</param>
    /// <param name="context">The SQL generation context</param>
    /// <param name="filterConditionProcessor">Optional filter condition processor for platform-specific transformations</param>
    private static void ProcessStep(
        IStep step,
        ISqlContext context,
        Func<string, string>? filterConditionProcessor
    )
    {
        switch (step)
        {
            case IdentityStep identityStep:
                if (identityStep.Base is Identifier id)
                {
                    context.SetBaseTable(id.Name);
                }
                else if (identityStep.Base is Pipeline pipeline)
                {
                    // Process the pipeline steps normally - let the visitor handle unions
                    foreach (var subStep in pipeline.Steps)
                    {
                        ProcessStep(subStep, context, filterConditionProcessor);
                    }
                }
                break;

            case JoinStep joinStep:
                var join = joinStep.JoinRelationship;
                context.AddJoin(join.JoinType, join.RightTable, join.Condition);
                break;

            case FilterStep filterStep:
                // FilterStep now stores WhereCondition directly - no conversion needed
                var condition = filterStep.Condition;

                // Apply platform-specific processing if needed (e.g., PostgreSQL column reference processing)
                if (
                    filterConditionProcessor != null
                    && condition is ExpressionCondition exprCondition
                )
                {
                    var processedExpression = filterConditionProcessor(exprCondition.Expression);
                    condition = WhereCondition.FromExpression(processedExpression);
                }

                context.AddWhereCondition(condition);
                break;

            case SelectStep selectStep:
                context.SetSelectColumns(selectStep.Columns);
                break;

            case SelectDistinctStep selectDistinctStep:
                context.SetSelectColumns(selectDistinctStep.Columns, distinct: true);
                break;

            case GroupByStep groupByStep:
                context.AddGroupBy(groupByStep.Columns.Select(col => ColumnInfo.Named(col)));
                break;

            case OrderByStep orderByStep:
                context.AddOrderBy(orderByStep.OrderItems);
                break;

            case HavingStep havingStep:
                context.AddHaving(havingStep.Condition);
                break;

            case LimitStep limitStep:
                context.SetLimit(limitStep.Count);
                break;

            case OffsetStep offsetStep:
                context.SetOffset(offsetStep.Count);
                break;

            case UnionStep unionStep:
                context.AddUnion(unionStep.OtherQuery, false);
                break;

            case UnionAllStep unionAllStep:
                context.AddUnion(unionAllStep.OtherQuery, true);
                break;

            case InsertStep insertStep:
                context.SetInsertTarget(insertStep.Table, insertStep.Columns);
                break;
        }
    }
}
