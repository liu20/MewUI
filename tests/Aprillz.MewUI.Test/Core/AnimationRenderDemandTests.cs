using Aprillz.MewUI;
using Aprillz.MewUI.Animation;
using Aprillz.MewUI.Controls;

namespace MewUI.Test.Core;

[TestClass]
[DoNotParallelize]
public sealed class AnimationRenderDemandTests
{
    [TestCleanup]
    public void Cleanup() => AnimationManager.Reset();

    [TestMethod]
    public void PausedClock_DoesNotKeepContinuousRenderDemandAlive()
    {
        AnimationManager.Reset();
        var clock = new AnimationClock(TimeSpan.FromSeconds(10));

        clock.Start();
        Assert.IsTrue(AnimationManager.Instance.HasRenderDemand);

        clock.Pause();
        Assert.IsFalse(AnimationManager.Instance.HasRenderDemand);

        clock.Resume();
        Assert.IsTrue(AnimationManager.Instance.HasRenderDemand);

        clock.Stop();
        Assert.IsFalse(AnimationManager.Instance.HasRenderDemand);
    }

    [TestMethod]
    public void OwnedClock_DemandsOnlyItsVisualRootWindow()
    {
        var first = new Window();
        var firstOwner = new Border();
        first.Content = firstOwner;

        var second = new Window
        {
            Content = new Border(),
        };

        var clock = new AnimationClock(TimeSpan.FromSeconds(10))
            .AttachTo(firstOwner);
        var settings = new RenderLoopSettings();

        clock.Start();
        var pulse = AnimationManager.Instance.BeginPulse(settings);

        Assert.IsFalse(pulse.HasApplicationRenderDemand);
        Assert.IsTrue(pulse.HasRenderDemand(first));
        Assert.IsFalse(pulse.HasRenderDemand(second));

        pulse.Dispose();
        Assert.IsFalse(pulse.HasRenderDemand(first));

        clock.Stop();
    }

    [TestMethod]
    public void UnownedPublicClock_RetainsApplicationWidePulseDemand()
    {
        var clock = new AnimationClock(TimeSpan.FromSeconds(10));
        var settings = new RenderLoopSettings();

        clock.Start();
        using var pulse = AnimationManager.Instance.BeginPulse(settings);

        Assert.IsTrue(pulse.HasApplicationRenderDemand);

        clock.Stop();
    }

    [TestMethod]
    public void PausedOwnedClock_DoesNotDemandItsWindow()
    {
        var window = new Window();
        var owner = new Border();
        window.Content = owner;

        var clock = new AnimationClock(TimeSpan.FromSeconds(10))
            .AttachTo(owner);
        var settings = new RenderLoopSettings();

        clock.Start();
        clock.Pause();
        using var pulse = AnimationManager.Instance.BeginPulse(settings);

        Assert.IsFalse(pulse.HasRenderDemand(window));
        Assert.IsFalse(pulse.HasApplicationRenderDemand);

        clock.Stop();
    }

    [TestMethod]
    public void Pulse_CentralizesApplicationWideAndWindowRenderPolicy()
    {
        var first = new Window();
        var owner = new Border();
        first.Content = owner;
        var second = new Window();
        var settings = new RenderLoopSettings();

        var clock = new AnimationClock(TimeSpan.FromSeconds(10))
            .AttachTo(owner);

        clock.Start();
        using (var pulse = AnimationManager.Instance.BeginPulse(settings))
        {
            Assert.IsTrue(pulse.ShouldRender(first, needsRender: false));
            Assert.IsFalse(pulse.ShouldRender(second, needsRender: false));
            Assert.IsTrue(pulse.ShouldRender(second, needsRender: true));

            settings.Continuous = true;
            Assert.IsFalse(pulse.ShouldRender(second, needsRender: false),
                "Render policy must remain stable for the lifetime of one pulse.");
        }

        using (var continuousPulse = AnimationManager.Instance.BeginPulse(settings))
        {
            Assert.IsTrue(continuousPulse.ShouldRender(second, needsRender: false));
        }

        settings.Continuous = false;
        settings.VSyncEnabled = false;
        using (var vsyncOffPulse = AnimationManager.Instance.BeginPulse(settings))
        {
            Assert.IsTrue(vsyncOffPulse.ShouldRender(second, needsRender: false));
        }

        clock.Stop();
    }
}
