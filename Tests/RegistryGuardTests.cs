using GenEvent;

namespace Tests;

[TestFixture]
public class RegistryGuardTests
{
    [Test]
    public void EndPublish_WithoutMatchingBegin_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GenEventRegistry<TestEventB, ReferenceEqualitySubscriberForB>.EndPublish());
    }
}
