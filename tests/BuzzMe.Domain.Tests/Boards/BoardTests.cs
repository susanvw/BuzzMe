using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Boards.Events;

namespace BuzzMe.Domain.Tests.Boards;

public sealed class BoardTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_MakesTheCreatorTheOwnerAndAMember()
    {
        var creatorUserId = Guid.CreateVersion7();

        var board = Board.Create(new BoardId(Guid.CreateVersion7()), new BoardName("Family"), creatorUserId, Now);

        Assert.Single(board.Memberships);
        Assert.Equal(creatorUserId, board.OwnerUserId);
        Assert.True(board.HasMember(creatorUserId));
        Assert.Equal(MembershipRole.Owner, board.Memberships.Single().Role);
    }

    [Fact]
    public void Create_StampsTheGivenNameAndCreationTime()
    {
        var board = Board.Create(new BoardId(Guid.CreateVersion7()), new BoardName("CrossFit"), Guid.CreateVersion7(), Now);

        Assert.Equal("CrossFit", board.Name.Value);
        Assert.Equal(Now, board.CreatedAt);
    }

    [Fact]
    public void Create_RaisesBoardCreatedAndMembershipGranted()
    {
        var boardId = new BoardId(Guid.CreateVersion7());
        var creatorUserId = Guid.CreateVersion7();

        var board = Board.Create(boardId, new BoardName("Family"), creatorUserId, Now);

        var boardCreated = Assert.Single(board.DomainEvents.OfType<BoardCreated>());
        Assert.Equal(boardId, boardCreated.BoardId);
        Assert.Equal("Family", boardCreated.Name.Value);

        var membershipGranted = Assert.Single(board.DomainEvents.OfType<MembershipGranted>());
        Assert.Equal(boardId, membershipGranted.BoardId);
        Assert.Equal(creatorUserId, membershipGranted.UserId);
        Assert.Equal(MembershipRole.Owner, membershipGranted.Role);
    }

    [Fact]
    public void HasMember_ReturnsFalseForAnyoneNotOnTheBoard()
    {
        var board = Board.Create(new BoardId(Guid.CreateVersion7()), new BoardName("Family"), Guid.CreateVersion7(), Now);

        Assert.False(board.HasMember(Guid.CreateVersion7()));
    }

    [Fact]
    public void GrantMembership_AddsANewMemberWithTheMemberRole()
    {
        var board = Board.Create(new BoardId(Guid.CreateVersion7()), new BoardName("Family"), Guid.CreateVersion7(), Now);
        var newMemberUserId = Guid.CreateVersion7();

        board.GrantMembership(newMemberUserId, Now);

        Assert.Equal(2, board.Memberships.Count);
        Assert.True(board.HasMember(newMemberUserId));
        Assert.Equal(MembershipRole.Member, board.Memberships.Single(m => m.UserId == newMemberUserId).Role);
    }

    [Fact]
    public void GrantMembership_RaisesMembershipGranted()
    {
        var boardId = new BoardId(Guid.CreateVersion7());
        var board = Board.Create(boardId, new BoardName("Family"), Guid.CreateVersion7(), Now);
        var newMemberUserId = Guid.CreateVersion7();

        board.GrantMembership(newMemberUserId, Now);

        var granted = board.DomainEvents.OfType<MembershipGranted>().Single(e => e.UserId == newMemberUserId);
        Assert.Equal(boardId, granted.BoardId);
        Assert.Equal(MembershipRole.Member, granted.Role);
    }

    [Fact]
    public void GrantMembership_IsIdempotentForAnExistingMember()
    {
        var creatorUserId = Guid.CreateVersion7();
        var board = Board.Create(new BoardId(Guid.CreateVersion7()), new BoardName("Family"), creatorUserId, Now);

        board.GrantMembership(creatorUserId, Now);

        Assert.Single(board.Memberships);
        Assert.Single(board.DomainEvents.OfType<MembershipGranted>());
    }

    [Fact]
    public void MuteBoard_SetsMutedOnTheGivenMembersOwnMembership()
    {
        var ownerUserId = Guid.CreateVersion7();
        var board = Board.Create(new BoardId(Guid.CreateVersion7()), new BoardName("Family"), ownerUserId, Now);
        var otherMemberUserId = Guid.CreateVersion7();
        board.GrantMembership(otherMemberUserId, Now);

        board.MuteBoard(otherMemberUserId, Now);

        Assert.True(board.Memberships.Single(m => m.UserId == otherMemberUserId).Muted);
        Assert.False(board.Memberships.Single(m => m.UserId == ownerUserId).Muted);
    }

    [Fact]
    public void MuteBoard_RaisesBoardMuted()
    {
        var boardId = new BoardId(Guid.CreateVersion7());
        var ownerUserId = Guid.CreateVersion7();
        var board = Board.Create(boardId, new BoardName("Family"), ownerUserId, Now);

        board.MuteBoard(ownerUserId, Now);

        var raised = Assert.Single(board.DomainEvents.OfType<BoardMuted>());
        Assert.Equal(boardId, raised.BoardId);
        Assert.Equal(ownerUserId, raised.UserId);
    }

    [Fact]
    public void MuteBoard_IsIdempotentWhenAlreadyMuted()
    {
        var ownerUserId = Guid.CreateVersion7();
        var board = Board.Create(new BoardId(Guid.CreateVersion7()), new BoardName("Family"), ownerUserId, Now);
        board.MuteBoard(ownerUserId, Now);

        board.MuteBoard(ownerUserId, Now);

        Assert.Single(board.DomainEvents.OfType<BoardMuted>());
    }

    [Fact]
    public void MuteBoard_ThrowsForSomeoneWhoIsNotAMember()
    {
        var board = Board.Create(new BoardId(Guid.CreateVersion7()), new BoardName("Family"), Guid.CreateVersion7(), Now);

        Assert.Throws<InvalidOperationException>(() => board.MuteBoard(Guid.CreateVersion7(), Now));
    }

    [Fact]
    public void UnmuteBoard_ClearsMutedOnTheGivenMembersOwnMembership()
    {
        var ownerUserId = Guid.CreateVersion7();
        var board = Board.Create(new BoardId(Guid.CreateVersion7()), new BoardName("Family"), ownerUserId, Now);
        board.MuteBoard(ownerUserId, Now);

        board.UnmuteBoard(ownerUserId, Now);

        Assert.False(board.Memberships.Single(m => m.UserId == ownerUserId).Muted);
    }

    [Fact]
    public void UnmuteBoard_RaisesBoardUnmuted()
    {
        var boardId = new BoardId(Guid.CreateVersion7());
        var ownerUserId = Guid.CreateVersion7();
        var board = Board.Create(boardId, new BoardName("Family"), ownerUserId, Now);
        board.MuteBoard(ownerUserId, Now);

        board.UnmuteBoard(ownerUserId, Now);

        var raised = Assert.Single(board.DomainEvents.OfType<BoardUnmuted>());
        Assert.Equal(boardId, raised.BoardId);
        Assert.Equal(ownerUserId, raised.UserId);
    }

    [Fact]
    public void UnmuteBoard_IsIdempotentWhenNotCurrentlyMuted()
    {
        var ownerUserId = Guid.CreateVersion7();
        var board = Board.Create(new BoardId(Guid.CreateVersion7()), new BoardName("Family"), ownerUserId, Now);

        board.UnmuteBoard(ownerUserId, Now);

        Assert.Empty(board.DomainEvents.OfType<BoardUnmuted>());
    }

    [Fact]
    public void UnmuteBoard_ThrowsForSomeoneWhoIsNotAMember()
    {
        var board = Board.Create(new BoardId(Guid.CreateVersion7()), new BoardName("Family"), Guid.CreateVersion7(), Now);

        Assert.Throws<InvalidOperationException>(() => board.UnmuteBoard(Guid.CreateVersion7(), Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BoardName_RejectsAnEmptyValue(string invalidName)
    {
        Assert.Throws<ArgumentException>(() => new BoardName(invalidName));
    }

    [Fact]
    public void BoardName_TrimsSurroundingWhitespace()
    {
        var name = new BoardName("  Family  ");

        Assert.Equal("Family", name.Value);
    }
}
