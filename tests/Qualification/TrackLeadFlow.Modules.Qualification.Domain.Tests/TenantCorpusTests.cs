using TrackLeadFlow.Modules.Qualification.Domain;
using Xunit;

namespace TrackLeadFlow.Modules.Qualification.Domain.Tests;

public class TenantCorpusTests
{
    [Fact]
    public void Compose_uses_only_the_asking_tenants_entries()
    {
        var tenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var entries = new[]
        {
            new CorpusEntry(tenantA, "roteiro de A"),
            new CorpusEntry(tenantB, "segredo de B"),
        };

        var corpus = TenantCorpus.Compose(tenantA, entries);

        Assert.Contains("roteiro de A", corpus);
        Assert.DoesNotContain("segredo de B", corpus);
    }

    [Fact]
    public void Compose_picks_up_an_edit_on_the_next_call()
    {
        var tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var before = new[] { new CorpusEntry(tenant, "roteiro antigo") };
        var after = new[] { new CorpusEntry(tenant, "roteiro novo") };

        Assert.Equal("roteiro antigo", TenantCorpus.Compose(tenant, before));
        Assert.Equal("roteiro novo", TenantCorpus.Compose(tenant, after));
    }
}
