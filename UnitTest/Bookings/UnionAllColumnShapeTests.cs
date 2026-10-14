using System.Text.RegularExpressions;

namespace UnitTest.Bookings;

// Postgres matches UNION ALL arms by POSITION and rejects the whole query when
// the arms disagree on column count ("42601: each UNION query must have the
// same number of columns"). The repositories build these as raw SQL, which the
// unit tests never execute, so a column added to some arms but not all ships
// green and fails only in production. It did: PR #65 added
// "CompletedWithActual" to five of six P&L terminal arms and the P&L page
// failed to load from 2026-10-09 until this was caught.
//
// This test reads every repository's raw SQL from the source tree and checks
// that every arm of every UNION ALL names the same columns in the same order.
public partial class UnionAllColumnShapeTests
{
  [GeneratedRegex("\"\"\"([\\s\\S]*?)\"\"\"")]
  private static partial Regex RawString();

  [GeneratedRegex(@"\bUNION ALL\b")]
  private static partial Regex UnionAll();

  [GeneratedRegex("AS\\s+\"(\\w+)\"")]
  private static partial Regex Alias();

  private static string RepoRoot()
  {
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "App", "Modules")))
      dir = dir.Parent;
    return dir?.FullName ?? throw new InvalidOperationException("App/Modules not found above test output");
  }

  public static IEnumerable<object[]> UnionQueries()
  {
    var modules = Path.Combine(RepoRoot(), "App", "Modules");
    foreach (var file in Directory.EnumerateFiles(modules, "*Repository.cs", SearchOption.AllDirectories))
    {
      var blocks = RawString()
        .Matches(File.ReadAllText(file))
        .Select(m => m.Groups[1].Value)
        .Where(b => UnionAll().IsMatch(b))
        .ToList();
      for (var i = 0; i < blocks.Count; i++)
        yield return [$"{Path.GetFileName(file)} #{i}", blocks[i]];
    }
  }

  [Fact]
  public void The_scan_finds_the_known_union_queries()
  {
    // guards the test itself: if the source layout changes and nothing is
    // scanned, every other case would pass vacuously
    UnionQueries().Select(q => (string)q[0]).Should().Contain(n => n.StartsWith("BookingAnalysisRepository.cs"))
      .And.Contain(n => n.StartsWith("InvoiceInputRepository.cs"));
  }

  [Theory]
  [MemberData(nameof(UnionQueries))]
  public void Every_union_arm_selects_the_same_columns_in_the_same_order(string query, string sql)
  {
    var arms = UnionAll()
      .Split(sql)
      .Select(a => Alias().Matches(a).Select(m => m.Groups[1].Value).ToArray())
      .ToArray();

    var first = arms[0];
    for (var i = 1; i < arms.Length; i++)
      arms[i].Should().Equal(first, $"{query}: arm {i} must select the same columns, in the same order, as arm 0");
  }
}
