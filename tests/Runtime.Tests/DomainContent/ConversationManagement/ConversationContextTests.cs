using Runtime.DomainContent.ConversationManagement;

namespace Runtime.Tests.DomainContent.ConversationManagement;

public class ConversationContextTests
{
    [Fact]
    public void New_ProducesEmptyStateDefaults()
    {
        var context = ConversationContext.New("conv-1");

        Assert.Equal("conv-1", context.ConversationId);
        Assert.Empty(context.AskedQuestions);
        Assert.Empty(context.AnsweredQuestions);
        Assert.Null(context.ActiveWorkflowId);
        Assert.False(context.PendingApproval);
        Assert.False(context.RepositoryContextAvailable);
    }
}
