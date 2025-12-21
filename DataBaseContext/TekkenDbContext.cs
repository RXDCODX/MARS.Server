using MARS.Server.Services.Framedata.Entitys;
using MARS.Server.Services.Framedata.Entitys.Pending;
using MARS.Server.Services.TekkenClans.Entities;

namespace MARS.Server.DataBaseContext;

public sealed partial class AppDbContext
{
    //framedata
    public DbSet<TekkenCharacter> TekkenCharacters { get; set; } = null!;
    public DbSet<Move> TekkenMoves { get; set; } = null!;
    public DbSet<TekkenCharacterPending> TekkenCharactersPending { get; set; } = null!;
    public DbSet<MovePending> TekkenMovesPending { get; set; } = null!;

    //clans
    public DbSet<TekkenPlayer> TekkenPlayers { get; set; } = null!;
    public DbSet<TekkenPlayer> TekkenClans { get; set; } = null!;
    public DbSet<TekkenBodyBanner>
}
