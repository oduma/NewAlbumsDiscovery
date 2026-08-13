using NewAlbumsDiscovery.Domain.AIDiscovery;

namespace NewAlbumsDiscovery.Domain.Tests.AIDiscovery;

public class AlbumDeduplicatorTests
{
    [Fact]
    public void SelectNew_WithEmptyCandidates_ReturnsEmptyList()
    {
        var result = AlbumDeduplicator.SelectNew([], new HashSet<AlbumKey>());

        Assert.Empty(result);
    }

    [Fact]
    public void SelectNew_WithNoOverlap_ReturnsAllCandidates()
    {
        var candidates = new List<(string Artist, string Album)>
        {
            ("Artist A", "Album A"),
            ("Artist B", "Album B"),
        };

        var result = AlbumDeduplicator.SelectNew(candidates, new HashSet<AlbumKey>());

        Assert.Equal(candidates, result);
    }

    [Fact]
    public void SelectNew_WithFullOverlap_ReturnsEmptyList()
    {
        var candidates = new List<(string Artist, string Album)> { ("Artist A", "Album A") };
        var existing = new HashSet<AlbumKey> { AlbumKey.From("Artist A", "Album A") };

        var result = AlbumDeduplicator.SelectNew(candidates, existing);

        Assert.Empty(result);
    }

    [Fact]
    public void SelectNew_WithPartialOverlap_ReturnsOnlyNonMatchingCandidates()
    {
        var candidates = new List<(string Artist, string Album)>
        {
            ("Artist A", "Album A"),
            ("Artist B", "Album B"),
        };
        var existing = new HashSet<AlbumKey> { AlbumKey.From("Artist A", "Album A") };

        var result = AlbumDeduplicator.SelectNew(candidates, existing);

        var kept = Assert.Single(result);
        Assert.Equal(("Artist B", "Album B"), kept);
    }

    [Fact]
    public void SelectNew_MatchIsCaseAndWhitespaceInsensitive()
    {
        var candidates = new List<(string Artist, string Album)> { ("  ARTIST A  ", "  album a  ") };
        var existing = new HashSet<AlbumKey> { AlbumKey.From("Artist A", "Album A") };

        var result = AlbumDeduplicator.SelectNew(candidates, existing);

        Assert.Empty(result);
    }

    [Fact]
    public void SelectNew_WithDuplicateCandidatesInSameBatch_KeepsOnlyFirstOccurrence()
    {
        var candidates = new List<(string Artist, string Album)>
        {
            ("Artist A", "Album A"),
            ("artist a", "album a"),
        };

        var result = AlbumDeduplicator.SelectNew(candidates, new HashSet<AlbumKey>());

        var kept = Assert.Single(result);
        Assert.Equal(("Artist A", "Album A"), kept);
    }

    [Fact]
    public void SelectNew_AddsSelectedKeysToExistingKeysSet_SoSubsequentCallsSeeThem()
    {
        var existing = new HashSet<AlbumKey>();

        var firstBatch = AlbumDeduplicator.SelectNew([("Artist A", "Album A")], existing);
        var secondBatch = AlbumDeduplicator.SelectNew([("Artist A", "Album A")], existing);

        Assert.Single(firstBatch);
        Assert.Empty(secondBatch);
    }
}
