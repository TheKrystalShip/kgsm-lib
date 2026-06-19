namespace TheKrystalShip.KGSM.Tests.Models;

/// <summary>
/// The two pure helpers over the canonical structured port form: <see cref="PortMappingExtensions.Expand"/>
/// (range → individual (port, protocol) pairs, what the watchdog feeds and a per-port
/// conflict scan iterates) and <see cref="PortMappingExtensions.ToUfwSpec"/> (the inverse render to the
/// legacy UFW string).
/// </summary>
public class PortMappingTests
{
    [Fact]
    public void Expand_unrolls_a_range_into_individual_ports()
    {
        List<PortMapping> mappings = [new() { Start = 27015, End = 27017, Protocol = "udp" }];

        (int Port, string Protocol)[] expanded = [.. mappings.Expand()];

        Assert.Equal([(27015, "udp"), (27016, "udp"), (27017, "udp")], expanded);
    }

    [Fact]
    public void Expand_keeps_a_single_port_as_one_pair()
    {
        List<PortMapping> mappings =
        [
            new() { Start = 34197, End = 34197, Protocol = "tcp" },
            new() { Start = 34197, End = 34197, Protocol = "udp" },
        ];

        Assert.Equal([(34197, "tcp"), (34197, "udp")], mappings.Expand().ToArray());
    }

    [Fact]
    public void Expand_skips_inverted_ranges_defensively()
    {
        List<PortMapping> mappings = [new() { Start = 100, End = 99, Protocol = "tcp" }];

        Assert.Empty(mappings.Expand());
    }

    [Fact]
    public void Expand_of_empty_is_empty()
    {
        Assert.Empty(new List<PortMapping>().Expand());
    }

    [Fact]
    public void ToUfwSpec_renders_single_and_range_entries_pipe_joined()
    {
        List<PortMapping> mappings =
        [
            new() { Start = 27015, End = 27020, Protocol = "udp" },
            new() { Start = 27016, End = 27016, Protocol = "tcp" },
        ];

        // range -> start:end/proto ; single -> port/proto ; '|'-joined
        Assert.Equal("27015:27020/udp|27016/tcp", mappings.ToUfwSpec());
    }

    [Fact]
    public void ToUfwSpec_of_empty_is_empty_string()
    {
        Assert.Equal(string.Empty, new List<PortMapping>().ToUfwSpec());
    }

    [Fact]
    public void FromUfwSpec_parses_single_and_range_entries()
    {
        List<PortMapping> parsed = PortMappingExtensions.FromUfwSpec("26900:26903/tcp|27015/udp");

        Assert.Equal(2, parsed.Count);
        Assert.Equal(new PortMapping { Start = 26900, End = 26903, Protocol = "tcp" }, parsed[0]);
        Assert.Equal(new PortMapping { Start = 27015, End = 27015, Protocol = "udp" }, parsed[1]);
    }

    [Fact]
    public void FromUfwSpec_expands_a_protocol_less_entry_to_tcp_and_udp()
    {
        // KGSM writes a UFW entry with no protocol as both transports.
        List<PortMapping> parsed = PortMappingExtensions.FromUfwSpec("34197");

        Assert.Equal(
            [
                new PortMapping { Start = 34197, End = 34197, Protocol = "tcp" },
                new PortMapping { Start = 34197, End = 34197, Protocol = "udp" },
            ],
            parsed);
    }

    [Fact]
    public void FromUfwSpec_is_the_inverse_of_ToUfwSpec()
    {
        List<PortMapping> original =
        [
            new() { Start = 27015, End = 27020, Protocol = "udp" },
            new() { Start = 27016, End = 27016, Protocol = "tcp" },
        ];

        Assert.Equal(original, PortMappingExtensions.FromUfwSpec(original.ToUfwSpec()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromUfwSpec_of_null_or_blank_is_empty(string? spec)
    {
        Assert.Empty(PortMappingExtensions.FromUfwSpec(spec));
    }

    [Fact]
    public void FromUfwSpec_skips_malformed_entries_without_fabricating()
    {
        // garbage port, unknown protocol, inverted range — each dropped; the one good entry survives.
        List<PortMapping> parsed = PortMappingExtensions.FromUfwSpec("abc/tcp|80/sctp|100:99/udp|443/tcp");

        Assert.Equal([new PortMapping { Start = 443, End = 443, Protocol = "tcp" }], parsed);
    }

    [Fact]
    public void FromUfwSpec_parses_a_real_blueprint_dual_protocol_spec()
    {
        // 7dtd's declared blueprint Ports string.
        List<PortMapping> parsed = PortMappingExtensions.FromUfwSpec("26900:26903/tcp|26900:26903/udp");

        Assert.Equal(
            [
                new PortMapping { Start = 26900, End = 26903, Protocol = "tcp" },
                new PortMapping { Start = 26900, End = 26903, Protocol = "udp" },
            ],
            parsed);
    }
}
