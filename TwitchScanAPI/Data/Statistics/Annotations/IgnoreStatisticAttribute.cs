using System;

namespace TwitchScanAPI.Data.Statistics.Annotations
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class IgnoreStatisticAttribute : Attribute
    {
    }
}