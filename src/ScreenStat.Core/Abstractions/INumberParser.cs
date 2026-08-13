using ScreenStat.Core.Models;

namespace ScreenStat.Core.Abstractions;

public interface INumberParser
{
    IReadOnlyList<NumberValue> Parse(string text);
}
