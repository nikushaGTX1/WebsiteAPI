using Website_API.Controllers;
using Xunit;
namespace Website_API.Tests;
public class MyHomeBrowserTests
{
    [Fact] public void EvidenceNeedsMatchingIdentityAndPublicUrl() {
        var evidence = new BrowserEvidence { MyHomeId = "12345678", DetectedId = "12345678", PageUrl = "https://www.myhome.ge/ka/pr/12345678", Status = "ACTIVE", Evidence = "HYDRATED_STATEMENT" };
        Assert.Equal("ACTIVE", MyHomeBrowserController.Classify(evidence));
        evidence.DetectedId = "87654321"; Assert.Equal("UNCERTAIN", MyHomeBrowserController.Classify(evidence));
        evidence.DetectedId = "12345678"; evidence.PageUrl = "https://myhome.ge.evil.com/ka/pr/12345678";
        Assert.Equal("UNCERTAIN", MyHomeBrowserController.Classify(evidence));
    }
    [Fact] public void OnlyExplicitDeletedMessageCanBeRemoved() {
        var evidence = new BrowserEvidence { MyHomeId = "12345678", PageUrl = "https://www.myhome.ge/ka/pr/12345678", Status = "REMOVED", Evidence = "CHALLENGE" };
        Assert.Equal("UNCERTAIN", MyHomeBrowserController.Classify(evidence));
        evidence.Evidence = "DELETED_MESSAGE"; Assert.Equal("UNCERTAIN", MyHomeBrowserController.Classify(evidence));
        evidence.VisibleExcerpt = "განცხადება რომელსაც ეძებდით წაშლილი ან არააქტიურია";
        Assert.Equal("REMOVED", MyHomeBrowserController.Classify(evidence));
    }
}
