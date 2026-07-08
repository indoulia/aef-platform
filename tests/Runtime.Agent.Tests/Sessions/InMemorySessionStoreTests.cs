using Runtime.Agent.Sessions;

namespace Runtime.Agent.Tests.Sessions;

public class InMemorySessionStoreTests
{
    [Fact]
    public void Create_ProducesEmptySessionState()
    {
        var store = new InMemorySessionStore();

        var session = store.Create("default-profile");

        Assert.Equal("default-profile", session.LastProjectProfileId);
        Assert.Null(session.LastPayload);
        Assert.Equal(0, session.TurnCount);
        Assert.Equal(session.CreatedAt, session.LastActivityAt);
    }

    [Fact]
    public void Get_UnknownSessionId_ReturnsNull()
    {
        var store = new InMemorySessionStore();

        Assert.Null(store.Get("does-not-exist"));
    }

    [Fact]
    public void Touch_UpdatesLastActivityTurnCountAndMergesPayload()
    {
        var store = new InMemorySessionStore();
        var created = store.Create("default-profile");
        var payload = new Dictionary<string, object> { ["Goal"] = "ship it" };

        var touched = store.Touch(created.SessionId, "default-profile", payload);

        Assert.Equal(1, touched.TurnCount);
        Assert.True(touched.LastActivityAt >= created.LastActivityAt);
        Assert.Same(payload, touched.LastPayload);
        Assert.Equal(touched, store.Get(created.SessionId));
    }

    [Fact]
    public void Touch_OmittedPayload_RetainsPriorPayload()
    {
        var store = new InMemorySessionStore();
        var created = store.Create("default-profile");
        var payload = new Dictionary<string, object> { ["Goal"] = "ship it" };
        store.Touch(created.SessionId, "default-profile", payload);

        var touchedAgain = store.Touch(created.SessionId, "default-profile", null);

        Assert.Same(payload, touchedAgain.LastPayload);
        Assert.Equal(2, touchedAgain.TurnCount);
    }

    [Fact]
    public void Touch_UnknownSessionId_Throws()
    {
        var store = new InMemorySessionStore();

        Assert.Throws<KeyNotFoundException>(() => store.Touch("does-not-exist", null, null));
    }
}
