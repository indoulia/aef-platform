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
        public bool Outage { get; set; } = true;
        public Dictionary<string, CodeReference> Symbols { get; } = new();
        public Dictionary<string, List<CodeReference>> ManySymbols { get; } = new();
        public int Calls { get; private set; }
        public List<CodeReference> Relevant { get; } = new();
        public Dictionary<string, string> Tests { get; } = new();

        private CodeLookupResult Wrap(IEnumerable<CodeReference> references)
        {
            Calls++;
            return UnavailableReason is null ? CodeLookupResult.Found(references.ToList()) : Outage ? CodeLookupResult.Unavailable(UnavailableReason) : CodeLookupResult.Unreadable(UnavailableReason);
        }

        public CodeLookupResult FindRelevant(string repositoryRoot, string taskDescription, int maxResults) => Wrap(Relevant);

        public CodeLookupResult FindSymbol(string repositoryRoot, string name, int maxResults) =>
            Wrap(ManySymbols.TryGetValue(name, out var many) ? many : Symbols.TryGetValue(name, out var reference) ? new[] { reference } : Array.Empty<CodeReference>());

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

    // Creates the cited file, as a current index implies: a symbol fact is only kept when its file is on disk.
    private CodeReference Ref(string name, string file, int line, string kind = "class")
    {
        var full = Path.Combine(_root, file);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "// " + name);
        return new CodeReference(name, kind, file, line, null, "fake");
    }

    private static CodeReference RefNoFile(string name, string file, int line, string kind = "class") => new(name, kind, file, line, null, "fake");

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

    // Real-repository sweep (marksy-os): ".gitignore" named in a prompt was dropped because the leading dot was trimmed.
    [Fact]
    public void Extract_KeepsDotFilesAndDotDirectories()
    {
        Assert.Equal(
            new[] { ".gitignore", ".github/workflows/ci.yml", ".env.example" },
            PromptIdentifierExtractor.Extract("Update .gitignore, .github/workflows/ci.yml and .env.example."));
    }

    [Fact]
    public void Execute_DotFileNamedInPromptExistsOnDisk_BecomesVerifiedFact()
    {
        File.WriteAllText(Path.Combine(_root, ".gitignore"), "bin/");

        var brief = Execute(new FakeProvider(), "keep the QR out of the repo in .gitignore");

        Assert.Contains(brief.VerifiedFacts, f => f.File == ".gitignore");
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
        Assert.DoesNotContain(brief.Assumptions, a => a.Contains("python")); // an unevidenced stack is not worth a line
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

    // Final review findings 1-4 and 9: ways an unconfirmed location could be presented as confirmed.
    [Fact]
    public void Execute_DottedIdentifier_WhoseTypeDoesNotExist_IsAnOpenQuestion_NotConfirmedByASameNamedMember()
    {
        var provider = new FakeProvider();
        provider.Symbols["Submit"] = Ref("Submit", "src/PaymentService.cs", 9, "method");

        var brief = Execute(provider, "Fix retry in `OrderService.Submit`");

        Assert.Empty(brief.VerifiedFacts);
        Assert.Empty(brief.StrongFiles!);
        Assert.Contains(brief.OpenQuestions, q => q.Contains("`OrderService.Submit`") && q.Contains("src/PaymentService.cs:9"));
    }

    [Fact]
    public void Execute_DottedIdentifier_TypeAndMemberInTheSameFile_IsConfirmed()
    {
        var provider = new FakeProvider();
        provider.Symbols["OrderService"] = Ref("OrderService", "src/OrderService.cs", 3);
        provider.Symbols["Submit"] = Ref("Submit", "src/OrderService.cs", 20, "method");

        var brief = Execute(provider, "Fix retry in `OrderService.Submit`");

        Assert.Contains(brief.VerifiedFacts, f => f.File == "src/OrderService.cs" && f.Line == 20);
        Assert.Equal(new[] { "src/OrderService.cs" }, brief.StrongFiles);
    }

    [Fact]
    public void Execute_NameDefinedInSeveralFiles_IsAnOpenQuestionListingThem_NotConfirmed()
    {
        var provider = new FakeProvider();
        provider.ManySymbols["parse_order"] = new List<CodeReference> { Ref("parse_order", "app/a.py", 4, "function"), Ref("parse_order", "app/b.py", 8, "function") };

        var brief = Execute(provider, "update parse_order to log");

        Assert.Empty(brief.VerifiedFacts);
        Assert.Empty(brief.StrongFiles!);
        Assert.Contains(brief.OpenQuestions, q => q.Contains("`parse_order`") && q.Contains("app/a.py:4") && q.Contains("app/b.py:8"));
    }

    [Fact]
    public void Execute_BareFileNameMatchingTwoFiles_IsAnOpenQuestionListingBoth_NotConfirmed()
    {
        Directory.CreateDirectory(Path.Combine(_root, "a"));
        Directory.CreateDirectory(Path.Combine(_root, "b"));
        File.WriteAllText(Path.Combine(_root, "a", "Program.cs"), "// a");
        File.WriteAllText(Path.Combine(_root, "b", "Program.cs"), "// b");

        var brief = Execute(new FakeProvider(), "fix startup in Program.cs");

        Assert.Empty(brief.VerifiedFacts);
        Assert.Empty(brief.StrongFiles!);
        Assert.Contains(brief.OpenQuestions, q => q.Contains("`Program.cs`") && q.Contains("a/Program.cs") && q.Contains("b/Program.cs"));
    }

    [Fact]
    public void Execute_SlashWordsProductNamesAndDomains_AreNotTurnedIntoQuestions()
    {
        var brief = Execute(new FakeProvider(), "support read/write and/or TCP/IP in Node.js and ASP.NET via example.com");

        Assert.DoesNotContain(brief.OpenQuestions, q => q.StartsWith("The prompt mentions"));
    }

    [Fact]
    public void Extract_IgnoresBacktickedPhrasesThatAreCommands()
    {
        Assert.Equal(new[] { "OrderService" }, PromptIdentifierExtractor.Extract("run `npm test` then fix `OrderService`"));
    }

    [Fact]
    public void Execute_AfterTheFirstUnavailableAnswer_StopsCallingTheProvider()
    {
        var provider = new FakeProvider { UnavailableReason = "tokensave timed out after 30 seconds." };

        Execute(provider, "wire OrderService, PaymentGateway, InvoiceBuilder, TaxEngine and ShippingQuote for the QR and T4 flows");

        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public void Execute_OneUnreadableAnswer_DoesNotStopLaterLookups_OrClaimCodeIntelligenceIsUnavailable()
    {
        var provider = new FakeProvider { UnavailableReason = "tokensave returned output that could not be read.", Outage = false };

        var brief = Execute(provider, "wire OrderService and PaymentGateway");

        Assert.True(provider.Calls > 1);
        Assert.DoesNotContain(brief.Assumptions, a => a.StartsWith("Code intelligence unavailable"));
    }

    [Fact]
    public void Execute_SameFileNamedTwoWays_IsOneFact()
    {
        Directory.CreateDirectory(Path.Combine(_root, "app"));
        File.WriteAllText(Path.Combine(_root, "app", "picks.py"), "# picks");

        var brief = Execute(new FakeProvider(), "fix picks.py, that is app/picks.py");

        Assert.Single(brief.VerifiedFacts, f => f.File == "app/picks.py");
    }

    [Fact]
    public void Execute_SymbolWhoseFileIsNoLongerOnDisk_IsNotAFact()
    {
        var provider = new FakeProvider();
        provider.Symbols["OrderService"] = RefNoFile("OrderService", "src/Deleted.cs", 12);

        var brief = Execute(provider, "Add retries to OrderService");

        Assert.Empty(brief.VerifiedFacts);
        Assert.Contains(brief.Assumptions, a => a.Contains("src/Deleted.cs") && a.Contains("index may be stale"));
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
        Enumerable.Range(1, whereToWork).Select(i => RefNoFile("Symbol" + i, $"src/some/long/directory/name/File{i}.cs", i)).ToList(),
        new[] { "tests/OrderServiceTests.cs" },
        "dotnet test",
        new[] { "an assumption" },
        new[] { "a question?" },
        new[] { "`dotnet test` exits 0" },
        0);

    // Cost: the brief is read by the agent on every call, so it carries only what adds information.
    [Fact]
    public void Render_ShowsSectionsInOrder_WithoutGoalOrRefinedPromptDuplication()
    {
        var text = GroundedBriefRenderer.Render(SampleBrief() with { StrongFiles = new[] { "src/OrderService.cs" } });

        var headings = new[] { "## Verified facts", "## Open questions", "## Where to work", "## Build and test", "## Assumptions", "## Done when" };
        var positions = headings.Select(h => text.IndexOf(h, StringComparison.Ordinal)).ToList();
        Assert.All(positions, p => Assert.True(p >= 0));
        Assert.Equal(positions.OrderBy(p => p), positions);
        Assert.DoesNotContain("## Goal", text);
        Assert.DoesNotContain("## Refined prompt", text);
        Assert.DoesNotContain("Add retries to OrderService", text);
        Assert.EndsWith("Verified facts are checked against the repository. Ask the user the open questions before editing.", text.TrimEnd());
    }

    [Fact]
    public void Render_FactLine_HasFileColonLine()
    {
        Assert.Contains("- `OrderService` is a class (src/OrderService.cs:12)", GroundedBriefRenderer.Render(SampleBrief()));
    }

    [Fact]
    public void Render_OmitsEmptySections()
    {
        var brief = SampleBrief() with { Assumptions = Array.Empty<string>(), OpenQuestions = Array.Empty<string>() };

        var text = GroundedBriefRenderer.Render(brief);

        Assert.DoesNotContain("## Assumptions", text);
        Assert.DoesNotContain("## Open questions", text);
        Assert.DoesNotContain("None.", text);
    }

    [Fact]
    public void Render_NothingToAdd_IsOneShortLinePlusTheBuildCommand()
    {
        var brief = new GroundedBrief("make it better", Array.Empty<EvidencedFact>(), Array.Empty<CodeReference>(), Array.Empty<string>(), "dotnet test",
            Array.Empty<string>(), Array.Empty<string>(), new[] { "`dotnet test` exits 0" }, 0);

        var text = GroundedBriefRenderer.Render(brief);

        Assert.StartsWith("No repository facts to add for this request.", text);
        Assert.Contains("`dotnet test`", text);
        Assert.True(text.Length < 200, $"length {text.Length}");
    }

    [Fact]
    public void Render_Candidates_AreOneLineOfAtMostFiveFiles_WithACountOfTheRest()
    {
        var text = GroundedBriefRenderer.Render(SampleBrief(whereToWork: 400));

        Assert.True(text.Length < 1200, $"length {text.Length}");
        Assert.Contains("File1.cs", text);
        Assert.Contains("File5.cs", text);
        Assert.DoesNotContain("File6.cs", text);
        Assert.Contains("(+395 more)", text);
        Assert.Contains("Covering tests: tests/OrderServiceTests.cs.", text);
    }

    [Fact]
    public void Render_TypicalBrief_IsUnderAThousandCharacters()
    {
        var text = GroundedBriefRenderer.Render(SampleBrief(whereToWork: 12) with { StrongFiles = new[] { "src/some/long/directory/name/File1.cs" } });

        Assert.True(text.Length < 1000, $"length {text.Length}");
    }
}
