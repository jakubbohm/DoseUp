using Shouldly;

namespace DoseUp.ArchitectureTests;

public sealed class HygieneTests {
  [Test]
  public void Rule_15_the_banned_symbols_file_exists_and_bans_the_four_time_apis() {
    // testing.md §5 rule 15: time APIs banned outside Platform — owner of record is
    // BannedApiAnalyzers; this hygiene check only pins that its input file stays present.
    // (Rule 16 — no EF InMemory/SQLite — is deliberately convention + review, no test.)
    string bannedSymbolsPath = Path.Combine(FindRepositoryRoot(), "BannedSymbols.txt");

    File.Exists(bannedSymbolsPath).ShouldBeTrue($"{bannedSymbolsPath} is missing");
    string content = File.ReadAllText(bannedSymbolsPath);
    content.ShouldContain("P:System.DateTime.Now");
    content.ShouldContain("P:System.DateTime.UtcNow");
    content.ShouldContain("P:System.DateTimeOffset.Now");
    content.ShouldContain("P:System.DateTimeOffset.UtcNow");
  }

  private static string FindRepositoryRoot() {
    DirectoryInfo? directory = new(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DoseUp.slnx")))
      directory = directory.Parent;

    return directory?.FullName
      ?? throw new InvalidOperationException(
        "Repository root (DoseUp.slnx) not found above the test output directory."
      );
  }
}