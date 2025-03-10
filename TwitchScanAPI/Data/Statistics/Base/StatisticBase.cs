using System.Threading.Tasks;

namespace TwitchScanAPI.Data.Statistics.Base;

public abstract class StatisticBase : IStatistic
{
    public abstract string Name { get; }
    protected bool HasUpdated { get; set; }
    private object? _lastResult;

    /// <summary>
    /// The public API for retrieving the statistic's result.
    /// Returns last result if nothing has changed since the last retrieval.
    /// Resets the HasUpdated flag afterward.
    /// </summary>
    public object? GetResult(bool ignoreUpdatedFlag = false)
    {
        //if (!ignoreUpdatedFlag && !HasUpdated && _lastResult != null)
        //    return _lastResult;
        
        var result = ComputeResult();
        HasUpdated = false;
        _lastResult = result;
        return result;
    }

    /// <summary>
    /// Each derived statistic class should implement this method to compute its result.
    /// </summary>
    protected abstract object ComputeResult();

    public virtual void Dispose()
    {
        // Override in derived classes
    }
}
