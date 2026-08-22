using System.Text.Json;

namespace TheKrystalShip.KGSM.Tests.Models;

/// <summary>
/// The observed-memory fields, read off the shape the engine actually emits.
/// </summary>
/// <remarks>
/// <c>instances info --json</c> serialises every config key as a <em>string</em>, so these read through
/// <c>KgsmJson.ExecutorOptions</c> — the same options the executor hands every response —
/// over the source-generated resolver, which is what proves they survive Native AOT, where there is no
/// reflection to fall back on.
/// </remarks>
public class InstanceObservedFieldsTests
{
    private static Instance Parse(string json) =>
        JsonSerializer.Deserialize<Instance>(json, KgsmJson.ExecutorOptions)!;

    [Fact]
    public void An_agreed_figure_is_read_from_the_engines_string_form()
    {
        Instance instance = Parse(
            """
            {
              "name": "romestead",
              "observed_ram_mb": "2908",
              "observed_ram_peak_mb": "2908",
              "observed_window_days": "30",
              "observed_updated_at": "2026-08-22T18:22:11Z"
            }
            """);

        Assert.Equal(2908, instance.ObservedRamMb);
        Assert.Equal(2908, instance.ObservedRamPeakMb);
        Assert.Equal(30, instance.ObservedWindowDays);
        Assert.Equal(new DateTime(2026, 8, 22, 18, 22, 11, DateTimeKind.Utc), instance.ObservedUpdatedAt);
    }

    [Fact]
    public void An_instance_that_has_never_been_measured_reads_null_not_zero()
    {
        // The template ships these keys empty, so this is the state EVERY instance starts in. A zero
        // here would report the instance as measured to need no memory.
        Instance instance = Parse(
            """
            {
              "name": "factorio-01",
              "observed_ram_mb": "",
              "observed_ram_peak_mb": "",
              "observed_window_days": "",
              "observed_updated_at": ""
            }
            """);

        Assert.Null(instance.ObservedRamMb);
        Assert.Null(instance.ObservedRamPeakMb);
        Assert.Null(instance.ObservedWindowDays);
        Assert.Null(instance.ObservedUpdatedAt);
    }

    [Fact]
    public void Keys_absent_altogether_read_null()
    {
        // An instance installed before these keys existed carries none of them, and never gains them
        // until a figure is agreed.
        Instance instance = Parse("""{"name": "Ketchup"}""");

        Assert.Null(instance.ObservedRamMb);
        Assert.Null(instance.ObservedWindowDays);
        Assert.Null(instance.ObservedUpdatedAt);
    }

    [Fact]
    public void The_agreed_figure_is_independent_of_the_cap()
    {
        // The two describe different things and neither is derived from the other: a record of what an
        // instance was measured holding, and a ceiling the watchdog writes to its cgroup.
        Instance instance = Parse(
            """{"name": "Ketchup", "memory_cap_mb": "0", "observed_ram_mb": "5400"}""");

        Assert.Equal(0, instance.MemoryCapMb);
        Assert.Equal(5400, instance.ObservedRamMb);
    }

    [Fact]
    public void An_empty_observed_figure_is_null_where_an_empty_cap_is_zero()
    {
        // Both keys are empty and they answer differently, which is the point of the per-property
        // converter: 0 is a real answer for a cap (KGSM's spelling of "uncapped") and a fabricated one
        // for a measurement. The per-property attribute is what wins over the executor's global
        // string-to-int coercion.
        Instance instance = Parse(
            """{"name": "Ketchup", "memory_cap_mb": "", "observed_ram_mb": ""}""");

        Assert.Equal(0, instance.MemoryCapMb);
        Assert.Null(instance.ObservedRamMb);
    }

    [Fact]
    public void A_garbled_figure_loses_only_itself()
    {
        Instance instance = Parse(
            """{"name": "necesse", "observed_ram_mb": "about 1.7 gigs", "observed_window_days": "25"}""");

        Assert.Null(instance.ObservedRamMb);
        Assert.Equal(25, instance.ObservedWindowDays);
        Assert.Equal("necesse", instance.Name);
    }

    [Fact]
    public void An_agreement_timestamp_without_a_timezone_is_null_rather_than_guessed()
    {
        Instance instance = Parse("""{"name": "necesse", "observed_updated_at": "2026-08-22 18:22:11"}""");
        Assert.Null(instance.ObservedUpdatedAt);
    }
}
