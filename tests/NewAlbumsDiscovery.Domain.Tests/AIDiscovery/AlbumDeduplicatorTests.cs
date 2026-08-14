using NewAlbumsDiscovery.Domain.AIDiscovery;

namespace NewAlbumsDiscovery.Domain.Tests.AIDiscovery;

public class AlbumDeduplicatorTests
{
    [Fact]
    public void SelectNew_WithEmptyCandidates_ReturnsEmptyAndLeavesExistingKeysUntouched()
    {
        var existingKeys = new HashSet<AlbumKey>();

        var result = AlbumDeduplicator.SelectNew([], existingKeys);

        Assert.Empty(result);
        Assert.Empty(existingKeys);
    }

    [Fact]
    public void SelectNew_WithAllNewCandidates_ReturnsAllAndAddsThemToExistingKeys()
    {
        var existingKeys = new HashSet<AlbumKey>();
        var candidates = new List<(string Artist, string Album)>
        {
            ("Daft Punk", "Discovery"),
            ("Justice", "Cross"),
        };

        var result = AlbumDeduplicator.SelectNew(candidates, existingKeys);

        Assert.Equal(candidates, result);
        Assert.Equal(2, existingKeys.Count);
        Assert.Contains(AlbumKey.From("Daft Punk", "Discovery"), existingKeys);
        Assert.Contains(AlbumKey.From("Justice", "Cross"), existingKeys);
    }

    [Fact]
    public void SelectNew_WithCandidateAlreadyInExistingKeys_RejectsIt()
    {
        var existingKeys = new HashSet<AlbumKey> { AlbumKey.From("Daft Punk", "Discovery") };
        var candidates = new List<(string Artist, string Album)> { ("daft punk", "  discovery ") };

        var result = AlbumDeduplicator.SelectNew(candidates, existingKeys);

        Assert.Empty(result);
        Assert.Single(existingKeys);
    }

    [Fact]
    public void SelectNew_WithInBatchDuplicate_KeepsOnlyFirstOccurrence()
    {
        var existingKeys = new HashSet<AlbumKey>();
        var candidates = new List<(string Artist, string Album)>
        {
            ("Daft Punk", "Discovery"),
            ("DAFT PUNK", "discovery"),
        };

        var result = AlbumDeduplicator.SelectNew(candidates, existingKeys);

        var only = Assert.Single(result);
        Assert.Equal(("Daft Punk", "Discovery"), only);
        Assert.Single(existingKeys);
    }
}
