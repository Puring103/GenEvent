using GenEvent;

namespace Tests;

[TestFixture]
public class RegistryGuardTests
{
    [SetUp]
    public void SetUp()
    {
        TestRuntimeState.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        TestRuntimeState.Reset();
    }

    [Test]
    public void EndPublish_WithoutMatchingBegin_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GenEventRegistry<TestEventB, ReferenceEqualitySubscriberForB>.EndPublish());
    }
}
