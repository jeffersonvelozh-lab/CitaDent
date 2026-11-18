using Domain.Common;
using Xunit;

namespace Domain.Tests.Common;

public class EntityTests
{
    private sealed class Dummy(Guid id) : Entity<Guid>(id);

    private sealed class OtherDummy(Guid id) : Entity<Guid>(id);

    [Fact]
    public void SameTypeAndSameId_AreEqual()
    {
        var id = Guid.NewGuid();

        Assert.Equal(new Dummy(id), new Dummy(id));
        Assert.True(new Dummy(id) == new Dummy(id));
        Assert.Equal(new Dummy(id).GetHashCode(), new Dummy(id).GetHashCode());
    }

    [Fact]
    public void SameTypeAndDifferentId_AreNotEqual()
    {
        Assert.NotEqual(new Dummy(Guid.NewGuid()), new Dummy(Guid.NewGuid()));
        Assert.True(new Dummy(Guid.NewGuid()) != new Dummy(Guid.NewGuid()));
    }

    [Fact]
    public void DifferentTypeAndSameId_AreNotEqual()
    {
        var id = Guid.NewGuid();

        Assert.False(new Dummy(id).Equals(new OtherDummy(id)));
    }

    [Fact]
    public void ComparingWithNull_IsNotEqual()
    {
        Dummy? missing = null;

        Assert.False(new Dummy(Guid.NewGuid()) == missing);
        Assert.True(missing == null);
    }
}