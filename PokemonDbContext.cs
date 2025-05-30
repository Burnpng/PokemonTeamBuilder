using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokemonTeamBuilder
{
    public class PokemonDbContext : DbContext
    {
        public DbSet<Pokemon> Pokemon { get; set; }
        public DbSet<Game> Games { get; set; }
        public DbSet<PokemonAvailability> PokemonAvailability { get; set; }
        public DbSet<TypeEffectiveness> TypeEffectiveness { get; set; }
        public DbSet<ExclusivityGroup> ExclusivityGroups { get; set; }
        public DbSet<ExclusivityGroupMember> ExclusivityGroupMembers { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=pokemon.db");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PokemonAvailability>()
                .HasKey(pa => new { pa.GameId, pa.PokemonId });

            modelBuilder.Entity<PokemonAvailability>()
                .HasOne(pa => pa.Pokemon)
                .WithMany(p => p.PokemonAvailabilities)
                .HasForeignKey(pa => pa.PokemonId);

            modelBuilder.Entity<PokemonAvailability>()
                .HasOne(pa => pa.Game)
                .WithMany(g => g.PokemonAvailabilities)
                .HasForeignKey(pa => pa.GameId);

            modelBuilder.Entity<TypeEffectiveness>()
                .HasKey(te => new { te.AttackingType, te.DefendingType });

            modelBuilder.Entity<ExclusivityGroupMember>()
                .HasKey(egm => new { egm.GroupId, egm.PokemonId });

            base.OnModelCreating(modelBuilder);
        }

    }

}
