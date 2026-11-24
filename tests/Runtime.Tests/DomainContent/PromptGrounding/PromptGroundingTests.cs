using Runtime.Context;
using Runtime.DomainContent.Onboarding;
using Runtime.DomainContent.PromptGrounding;
using Runtime.Execution;
using Runtime.Integration.CodeIntelligence;
using Runtime.Integration.RepositoryScanning;

namespace Runtime.Tests.DomainContent.PromptGrounding;

public class PromptGroundingTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("aef-grounding-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private sealed class FakeProvider : ICodeIntelligenceProvider
    {
        public string? UnavailableReason { get; set; }
        public Dictionary<string, CodeReference> Symbols { get; } = new();
        public List<CodeReference> Relevant { get; } = new();
        public Dictionary<string, string> Tests { get; } = new();

        private CodeLookupResult Wrap(IEnumerable<CodeReference> references) =>
            UnavailableReason is null ? CodeLookupResult.Found(references.ToList()) : CodeLookupResult.Unavailable(UnavailableReason);

        public CodeLookupResult FindRelevant(string repositoryRoot, string taskDescription, int maxResults) => Wrap(Relevant);

        public CodeLookupResult FindSymbol(string repositoryRoot, string name, int maxResults) =>
            Wrap(Symbols.TryGetValue(name, out var reference) ? new[] { reference } : Array.Empty<CodeReference>());

        public CodeLookupResult FindTests(string repositoryRoot, string file) =>
            Wrap(Tests.TryGetValue(file, out var test) ? new[] { new CodeReference(Path.GetFileName(test), "test-file", test, 0, null, "fake") } : Array.Empty<CodeReference>());

        public CodeLookupResult FindAffected(string repositoryRoot, IReadOnlyList<string> changedFiles) => Wrap(Array.Empty<CodeReference>());
    }

    private RepositoryExecutionContext Repository(params TechnologyEvidence[] technologies) =>
        new(_root, "repo", true, true, "main", true, technologies.Length == 0 ? null : new TechnologyProfileDraft("TPR-CTX-1", technologies));

    private GroundedBrief Execute(FakeProvider provider, string prompt, RepositoryExecutionContext? repository = null)
    {
        var context = new RuntimeExecutionContext(
            "grounding-test",
            new Tier0Context(Array.Empty<string>(), Array.Empty<string>()),
            "profile",
            "PromptGrounding",
            new Dictionary<string, object> { ["PromptGroundingRequest"] = new PromptGroundingRequest(prompt, repository ?? Repository()) });

        return Assert.IsType<GroundedBrief>(new PromptGroundingSkillBehavior(provider).Execute(context));
    }

    private static CodeReference Ref(string name, string file, int line, string kind = "class") => new(name, kind, file, line, null, "fake");

    [Fact]
    public void Extract_ReturnsBacktickedPathsAndCompoundIdentifiers_NotPlainWords()
    {
        var identifiers = PromptIdentifierExtractor.Extract("Fix the retry in `OrderService.Submit` and src/api/orders.ts please");

        Assert.Equal(new[] { "OrderService.Submit", "src/api/orders.ts" }, identifiers);
    }

    [Theory]
    [InlineData("update parse_order to log", "parse_order")]
    [InlineData("rename getUserName everywhere", "getUserName")]
    [InlineData("the README.md is stale", "README.md")]
    public void Extract_FindsCompoundIdentifiersAndFileNames(string prompt, string expected)
    {
        Assert.Equal(new[] { expected }, PromptIdentifierExtractor.Extract(prompt));
    }

    [Fact]
    public void Extract_PlainSentence_ReturnsNothing()
    {
        Assert.Empty(PromptIdentifierExtractor.Extract("Please make the login faster. Thanks."));
    }

    [Fact]
    public void Execute_IdentifierFound_BecomesVerifiedFactWithFileAndLine()
    {
        var provider = new FakeProvider();
        provider.Symbols["OrderService"] = Ref("OrderService", "src/OrderService.cs", 12);

        var brief = Execute(provider, "Add retries to OrderService");

        var fact = Assert.Single(brief.VerifiedFacts, f => f.Statement.Contains("OrderService"));
        Assert.Equal("src/OrderService.cs", fact.File);
        Assert.Equal(12, fact.Line);
    }

    [Fact]
    public void Execute_IdentifierNotFound_BecomesOpenQuestion_NotAFact()
    {
        var brief = Execute(new FakeProvider(), "Add retries to PaymentGateway");

        Assert.DoesNotContain(brief.VerifiedFacts, f => f.Statement.Contains("PaymentGateway"));
        Assert.Contains(brief.OpenQuestions, q => q == "The prompt mentions `PaymentGateway`, which was not found in the repository. Which file or symbol did you mean?");
    }

    [Fact]
    public void Execute_FileNamedInPromptExistsOnDisk_BecomesVerifiedFact_EvenWithoutCodeIntelligence()
    {
        Directory.CreateDirectory(Path.Combine(_root, "src"));
        File.WriteAllText(Path.Combine(_root, "src", "orders.ts"), "export {}");

        var brief = Execute(new FakeProvider { UnavailableReason = "tokensave is not installed or not on PATH." }, "tidy src/orders.ts");

        Assert.Contains(brief.VerifiedFacts, f => f.File == "src/orders.ts");
    }

    [Fact]
    public void Execute_ProviderUnavailable_ReturnsBriefWithAssumptionAndNoSymbolFacts()
    {
        var provider = new FakeProvider { UnavailableReason = "the repository has no tokensave index - run `tokensave init` in the repository root." };

        var brief = Execute(provider, "Add retries to OrderService");

        Assert.Empty(brief.VerifiedFacts);
        Assert.Empty(brief.WhereToWork);
        Assert.Contains(brief.Assumptions, a => a.StartsWith("Code intelligence unavailable: the repository has no tokensave index") && a.Contains("tokensave init"));
        Assert.Contains(brief.Assumptions, a => a.Contains("`OrderService`") && a.Contains("could not be checked"));
    }

    [Fact]
    public void Execute_EveryVerifiedFact_HasNonEmptyFile()
    {
        File.WriteAllText(Path.Combine(_root, "package.json"), "{}");
        var provider = new FakeProvider();
        provider.Symbols["OrderService"] = Ref("OrderService", "src/OrderService.cs", 12);

        var brief = Execute(provider, "Add retries to OrderService",
            Repository(new TechnologyEvidence("node", "package.json"), new TechnologyEvidence("python", "inferred from file extensions")));

        Assert.NotEmpty(brief.VerifiedFacts);
        Assert.All(brief.VerifiedFacts, f => Assert.False(string.IsNullOrWhiteSpace(f.File)));
        Assert.Contains(brief.VerifiedFacts, f => f.File == "package.json" && f.Statement.Contains("node"));
        Assert.Contains(brief.Assumptions, a => a.Contains("python"));
        Assert.Equal("npm test", brief.BuildAndTestCommand);
    }

    [Fact]
    public void Execute_MoreThanTwelveRelevant_CapsWhereToWorkAndCountsOmitted()
    {
        var provider = new FakeProvider();
        provider.Relevant.AddRange(Enumerable.Range(1, 15).Select(i => Ref("Symbol" + i, $"src/File{i}.cs", i)));

        var brief = Execute(provider, "make the login faster");

        Assert.Equal(12, brief.WhereToWork.Count);
        Assert.Equal(3, brief.OmittedCount);
    }

    [Fact]
    public void Execute_WhereToWork_ListsNamedIdentifiersFirst_AndTheirTests()
    {
        var provider = new FakeProvider();
        provider.Relevant.Add(Ref("Other", "src/Other.cs", 3));
        provider.Symbols["OrderService"] = Ref("OrderService", "src/OrderService.cs", 12);
        provider.Tests["src/OrderService.cs"] = "tests/OrderServiceTests.cs";

        var brief = Execute(provider, "Add retries to OrderService");

        Assert.Equal("src/OrderService.cs", brief.WhereToWork[0].File);
        Assert.Equal(new[] { "tests/OrderServiceTests.cs" }, brief.TestFiles);
        Assert.Contains("Tests in tests/OrderServiceTests.cs pass", brief.AcceptanceChecks);
    }

    [Theory]
    [InlineData("fix(strategies): refuse only recent gaps in the T4 window, record older ones on the run", new[] { "strategies", "T4" })]
    [InlineData("feat(settings): one Upstox form with QR import (camera or photo picker)", new[] { "settings", "Upstox", "QR" })]
    [InlineData("EPIC-138: the routing sentence, measured and rejected (#231)", new[] { "EPIC-138" })]
    [InlineData("Please make the login faster. Thanks.", new string[0])]
    [InlineData("Add retries to OrderService", new string[0])]
    public void ExtractSearchTerms_ReturnsScopeAcronymsAndProperNouns_NotSentenceStarts(string prompt, string[] expected)
    {
        Assert.Equal(expected, PromptIdentifierExtractor.ExtractSearchTerms(prompt));
    }

    [Fact]
    public void Execute_FileMatchedByMoreTerms_RanksFirst_ButOnlyNamedFilesAreEverConfirmed()
    {
        // Measured on 15 real commits: files matched by two search signals were the right file only 5 times in 30.
        var provider = new FakeProvider();
        provider.Relevant.Add(Ref("unrelated", "app/scheduler.py", 5, "function"));
        provider.Symbols["strategies"] = Ref("strategies", "app/strategies/picks.py", 1, "module");
        provider.Symbols["T4"] = Ref("t4_window", "app/strategies/picks.py", 40, "function");

        var brief = Execute(provider, "fix(strategies): refuse only recent gaps in the T4 window");

        Assert.Equal("app/strategies/picks.py", brief.WhereToWork[0].File);
        Assert.Empty(brief.StrongFiles!);
        var text = GroundedBriefRenderer.Render(brief);
        Assert.Contains("- app/strategies/picks.py:1 - module `strategies` (possible match, unconfirmed)", text);
        Assert.Contains("Candidate files (confirm which apply before editing): app/strategies/picks.py, app/scheduler.py.", text);
        Assert.DoesNotContain("Work in:", text);
    }

    [Fact]
    public void Execute_NamedFile_IsConfirmed_AndRefinedPromptSaysWorkInIt()
    {
        var provider = new FakeProvider();
        provider.Relevant.Add(Ref("unrelated", "app/scheduler.py", 5, "function"));
        provider.Symbols["OrderService"] = Ref("OrderService", "src/OrderService.cs", 12);

        var brief = Execute(provider, "Add retries to OrderService");

        Assert.Equal(new[] { "src/OrderService.cs" }, brief.StrongFiles);
        Assert.Contains("Work in: src/OrderService.cs.", GroundedBriefRenderer.Render(brief));
    }

    [Fact]
    public void Execute_NoStrongFiles_OmitsConfinementCheck_AndRefinedPromptAsksToConfirmCandidates()
    {
        var provider = new FakeProvider();
        provider.Relevant.Add(Ref("unrelated", "app/scheduler.py", 5, "function"));

        var brief = Execute(provider, "make the login faster");

        Assert.Empty(brief.StrongFiles!);
        Assert.DoesNotContain(brief.AcceptanceChecks, check => check.StartsWith("Changes are confined"));
        var text = GroundedBriefRenderer.Render(brief);
        Assert.Contains("Candidate files (confirm which apply before editing): app/scheduler.py.", text);
        Assert.DoesNotContain("Work in:", text);
    }

    [Fact]
    public void Execute_TestChecks_AreLimitedToThree()
    {
        var provider = new FakeProvider();
        foreach (var i in Enumerable.Range(1, 6))
        {
            provider.Relevant.Add(Ref("S" + i, $"src/F{i}.cs", i));
            provider.Tests[$"src/F{i}.cs"] = $"tests/F{i}Tests.cs";
        }

        var brief = Execute(provider, "make the login faster");

        Assert.Equal(3, brief.AcceptanceChecks.Count(check => check.StartsWith("Tests in ")));
    }

    [Fact]
    public void Execute_NothingLocated_AsksWhatPartOfTheRepository()
    {
        var brief = Execute(new FakeProvider(), "make it better");

        Assert.Contains("The prompt does not name a file, symbol or feature that could be located. What part of the repository is this about?", brief.OpenQuestions);
    }

    [Fact]
    public void Execute_NamedFile_IncludesConfinementCheck_AndOpenQuestionCheckIsAlwaysPresent()
    {
        var provider = new FakeProvider();
        provider.Symbols["OrderService"] = Ref("OrderService", "src/OrderService.cs", 12);

        Assert.Contains("Changes are confined to the files under Where to work, or each extra file is explained", Execute(provider, "Add retries to OrderService").AcceptanceChecks);
        Assert.Contains("Every open question above is answered before editing", Execute(new FakeProvider(), "make it better").AcceptanceChecks);
    }

    [Fact]
    public void Execute_ProviderUnavailableAndNothingNamed_DoesNotAskWhatPartOfTheRepository()
    {
        var brief = Execute(new FakeProvider { UnavailableReason = "tokensave is not installed or not on PATH." }, "make it better");

        Assert.DoesNotContain(brief.OpenQuestions, question => question.StartsWith("The prompt does not name"));
    }

    private static GroundedBrief SampleBrief(int whereToWork = 2) => new(
        "Add retries to OrderService",
        new[] { new EvidencedFact("`OrderService` is a class", "src/OrderService.cs", 12, "fake") },
        Enumerable.Range(1, whereToWork).Select(i => Ref("Symbol" + i, $"src/some/long/directory/name/File{i}.cs", i)).ToList(),
        new[] { "tests/OrderServiceTests.cs" },
        "dotnet test",
        new[] { "an assumption" },
        new[] { "a question?" },
        new[] { "`dotnet test` exits 0" },
        0);

    [Fact]
    public void Render_ContainsAllEightHeadingsInOrder_AndClosingLine()
    {
        var text = GroundedBriefRenderer.Render(SampleBrief());

        var headings = new[] { "## Goal", "## Verified facts", "## Where to work", "## How to build and test", "## Assumptions", "## Open questions", "## Acceptance checks", "## Refined prompt" };
        var positions = headings.Select(h => text.IndexOf(h, StringComparison.Ordinal)).ToList();
        Assert.All(positions, p => Assert.True(p >= 0));
        Assert.Equal(positions.OrderBy(p => p), positions);
        Assert.EndsWith("Treat Verified facts as ground truth. Ask the user the Open questions before editing.", text.TrimEnd());
    }

    [Fact]
    public void Render_FactLine_HasFileColonLine()
    {
        Assert.Contains("- `OrderService` is a class (src/OrderService.cs:12)", GroundedBriefRenderer.Render(SampleBrief()));
    }

    [Fact]
    public void Render_EmptySections_SayNone()
    {
        var brief = SampleBrief() with { Assumptions = Array.Empty<string>(), OpenQuestions = Array.Empty<string>() };

        var text = GroundedBriefRenderer.Render(brief);

        Assert.Contains("## Assumptions\n- None.", text);
        Assert.Contains("## Open questions\n- None.", text);
    }

    [Fact]
    public void Render_LargeBrief_StaysWithinMaxBriefCharacters_AndStatesOmittedCount()
    {
        var text = GroundedBriefRenderer.Render(SampleBrief(whereToWork: 400));

        Assert.True(text.Length <= GroundedBriefRenderer.MaxBriefCharacters, $"length {text.Length}");
        Assert.Matches(@"\(\d+ more omitted\)", text);
        Assert.Contains("## Acceptance checks", text);
    }
}
