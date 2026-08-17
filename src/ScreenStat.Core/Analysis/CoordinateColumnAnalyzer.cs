using ScreenStat.Core.Models;
using ScreenStat.Core.Statistics;

namespace ScreenStat.Core.Analysis;

public sealed class CoordinateColumnAnalyzer
{
    public IReadOnlyList<NumericColumn> Analyze(IReadOnlyList<NumericToken> tokens)
    {
        if (tokens is null || tokens.Count == 0)
        {
            return Array.Empty<NumericColumn>();
        }

        var heights = tokens
            .Select(token => token.Bounds.Height)
            .Where(height => height > 0)
            .OrderBy(height => height)
            .ToArray();
        var medianHeight = heights.Length == 0 ? 12 : heights[heights.Length / 2];
        var anchorTolerance = Math.Max(4, medianHeight * 0.75);

        var clusters = new List<TokenCluster>();
        foreach (var token in tokens.OrderBy(token => token.Bounds.CenterX).ThenBy(token => token.Bounds.Top))
        {
            var match = clusters
                .Where(cluster => !cluster.HasSameRowConflict(token))
                .Select(cluster => new
                {
                    Cluster = cluster,
                    Distance = cluster.AnchorDistance(token)
                })
                .Where(candidate => candidate.Distance <= anchorTolerance)
                .OrderBy(candidate => candidate.Distance)
                .FirstOrDefault();

            if (match is null)
            {
                clusters.Add(new TokenCluster(token));
            }
            else
            {
                match.Cluster.Add(token);
            }
        }

        return clusters
            .OrderBy(cluster => cluster.HorizontalCenter)
            .Select((cluster, index) =>
            {
                var ordered = cluster.Tokens
                    .OrderBy(token => token.Bounds.Top)
                    .ThenBy(token => token.Bounds.Left)
                    .ThenBy(token => token.SourceOrder)
                    .ToArray();
                return new NumericColumn
                {
                    Index = index + 1,
                    Tokens = ordered,
                    Statistics = StatisticsCalculator.Calculate(ordered.Select(token => token.Value).ToArray())
                };
            })
            .ToArray();
    }

    private sealed class TokenCluster
    {
        private readonly List<NumericToken> _tokens = new();

        public TokenCluster(NumericToken token) => _tokens.Add(token);

        public IReadOnlyList<NumericToken> Tokens => _tokens;
        public double HorizontalCenter => Median(_tokens.Select(token => token.Bounds.CenterX));

        public void Add(NumericToken token) => _tokens.Add(token);

        public bool HasSameRowConflict(NumericToken candidate) =>
            _tokens.Any(existing => existing.Bounds.VerticalOverlapRatio(candidate.Bounds) >= 0.35);

        public double AnchorDistance(NumericToken candidate)
        {
            var left = Math.Abs(candidate.Bounds.Left - Median(_tokens.Select(token => token.Bounds.Left)));
            var center = Math.Abs(candidate.Bounds.CenterX - Median(_tokens.Select(token => token.Bounds.CenterX)));
            var right = Math.Abs(candidate.Bounds.Right - Median(_tokens.Select(token => token.Bounds.Right)));
            return Math.Min(left, Math.Min(center, right));
        }

        private static double Median(IEnumerable<double> source)
        {
            var values = source.OrderBy(value => value).ToArray();
            if (values.Length == 0)
            {
                return 0;
            }

            var middle = values.Length / 2;
            return values.Length % 2 == 0
                ? (values[middle - 1] + values[middle]) / 2
                : values[middle];
        }
    }
}
