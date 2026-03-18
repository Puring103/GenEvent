using System.Threading.Tasks;
using GenEvent;
using GenEvent.Interface;

namespace Tests;

/// <summary>
/// Regression tests for Issue #1:
/// Derived classes that inherit or override a base [OnEvent] method without
/// re-declaring [OnEvent] themselves should still receive events via virtual dispatch.
///
/// Four combinations are tested, mirroring the exact classes reported in the issue:
///   GameManager  – base class with virtual [OnEvent]
///   GameManager1 – override + [OnEvent] re-declared  (was already working)
///   GameManager2 – pure inheritance, no override       (fixed)
///   GameManager3 – override without [OnEvent]          (fixed)
/// </summary>
[TestFixture]
public class Issue1_InheritedOnEventTests
{
    [SetUp]
    public void SetUp()
    {
        GenEventBootstrap.Init();
    }

    public struct PlayerDeathEvent : IGenEvent<PlayerDeathEvent>
    {
        public int PlayerId;
    }

    public class GameManager
    {
        public int ReceiveCount;

        [OnEvent]
        public virtual void OnPlayerDeath(PlayerDeathEvent e)
        {
            ReceiveCount++;
        }
    }

    /// <summary>Override with [OnEvent] re-declared — was already working before the fix.</summary>
    public class GameManager1 : GameManager
    {
        [OnEvent]
        public override void OnPlayerDeath(PlayerDeathEvent e)
        {
            ReceiveCount++;
        }
    }

    /// <summary>Pure inheritance — no override, no [OnEvent]. Fixed by Issue #1.</summary>
    public class GameManager2 : GameManager { }

    /// <summary>Override without [OnEvent]. Fixed by Issue #1.</summary>
    public class GameManager3 : GameManager
    {
        public override void OnPlayerDeath(PlayerDeathEvent e)
        {
            ReceiveCount++;
        }
    }

    // ------------------------------------------------------------------
    // Core receive
    // ------------------------------------------------------------------

    [Test]
    public void AllFourVariants_ReceiveEvent_AfterStartListening()
    {
        var gm = new GameManager();
        var gm1 = new GameManager1();
        var gm2 = new GameManager2();
        var gm3 = new GameManager3();

        using var h0 = gm.StartListening();
        using var h1 = gm1.StartListening();
        using var h2 = gm2.StartListening();
        using var h3 = gm3.StartListening();

        new PlayerDeathEvent { PlayerId = 1 }.Publish();

        Assert.That(gm.ReceiveCount, Is.EqualTo(1), "Base type with [OnEvent] must receive.");
        Assert.That(gm1.ReceiveCount, Is.EqualTo(1), "Override + [OnEvent] must receive.");
        Assert.That(gm2.ReceiveCount, Is.EqualTo(1), "Pure inheritance (no override) must receive via virtual dispatch.");
        Assert.That(gm3.ReceiveCount, Is.EqualTo(1), "Override without [OnEvent] must receive via virtual dispatch.");
    }

    // ------------------------------------------------------------------
    // StopListening / Dispose
    // ------------------------------------------------------------------

    [Test]
    public void GameManager2_StopListening_StopsReceiving()
    {
        var gm2 = new GameManager2();
        gm2.StartListening();
        new PlayerDeathEvent { PlayerId = 1 }.Publish();
        Assert.That(gm2.ReceiveCount, Is.EqualTo(1));

        gm2.StopListening();
        new PlayerDeathEvent { PlayerId = 2 }.Publish();
        Assert.That(gm2.ReceiveCount, Is.EqualTo(1), "Must not receive after StopListening.");
    }

    [Test]
    public void GameManager3_DisposeHandle_StopsReceiving()
    {
        var gm3 = new GameManager3();
        var handle = gm3.StartListening();
        new PlayerDeathEvent { PlayerId = 1 }.Publish();
        Assert.That(gm3.ReceiveCount, Is.EqualTo(1));

        handle.Dispose();
        new PlayerDeathEvent { PlayerId = 2 }.Publish();
        Assert.That(gm3.ReceiveCount, Is.EqualTo(1), "Must not receive after Dispose.");
    }

    // ------------------------------------------------------------------
    // Correct method body is dispatched (virtual dispatch verification)
    // ------------------------------------------------------------------

    [Test]
    public void GameManager3_Override_BodyIsInvoked_NotBase()
    {
        // GameManager3 increments ReceiveCount in its own override.
        // If virtual dispatch works correctly the count must match exactly one call.
        var gm3 = new GameManager3();
        using var h = gm3.StartListening();

        new PlayerDeathEvent { PlayerId = 7 }.Publish();

        Assert.That(gm3.ReceiveCount, Is.EqualTo(1));
    }

    // ------------------------------------------------------------------
    // Three-level inheritance
    // ------------------------------------------------------------------

    public class GameManager2Child : GameManager2 { }

    [Test]
    public void ThreeLevelInheritance_GrandChild_ReceivesEvent()
    {
        var child = new GameManager2Child();
        using var h = child.StartListening();

        new PlayerDeathEvent { PlayerId = 3 }.Publish();

        Assert.That(child.ReceiveCount, Is.EqualTo(1), "Three-level inheritance must also work.");
    }

    // ------------------------------------------------------------------
    // Isolation between instances
    // ------------------------------------------------------------------

    [Test]
    public void MultipleInstances_IndependentSubscription()
    {
        var a = new GameManager2();
        var b = new GameManager2();
        a.StartListening();
        b.StartListening();

        new PlayerDeathEvent { PlayerId = 1 }.Publish();
        Assert.That(a.ReceiveCount, Is.EqualTo(1));
        Assert.That(b.ReceiveCount, Is.EqualTo(1));

        a.StopListening();
        new PlayerDeathEvent { PlayerId = 2 }.Publish();
        Assert.That(a.ReceiveCount, Is.EqualTo(1), "a stopped");
        Assert.That(b.ReceiveCount, Is.EqualTo(2), "b still active");

        b.StopListening();
    }
}
