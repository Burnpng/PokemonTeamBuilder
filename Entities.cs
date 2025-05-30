using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public record Game([property: Key] int GameId, [property: Required] string Name, [property: Required] int Generation)
{
    public ICollection<PokemonAvailability> PokemonAvailabilities { get; set; } = [];
}

public class Pokemon
{
    [Key]
    public int PokemonId { get; set; }

    [Required]
    public int DexID { get; set; }

    [Required]
    public required string Name { get; set; } // Added 'required' modifier

    [Required]
    public int? BaseHP { get; set; }

    [Required]
    public int? BaseAttack { get; set; }

    [Required]
    public int? BaseDefense { get; set; }

    [Required]
    public int? BaseSpAttack { get; set; }

    [Required]
    public int? BaseSpDefense { get; set; }

    [Required]
    public int? BaseSpeed { get; set; }

    [Required]
    public required string Type1 { get; set; } // Added 'required' modifier

    public string? Type2 { get; set; }

    [Required]
    public bool IsFinalEvolution { get; set; }

    public bool IsLegendary { get; set; } = false;

    public bool IsMythical { get; set; } = false;

    public int? FinalEvoFinalGen { get; set; }

    public ICollection<PokemonAvailability> PokemonAvailabilities { get; set; } = [];
}

public class PokemonAvailability
{
    [Key, Column(Order = 0)]
    public int GameId { get; set; }

    [Key, Column(Order = 1)]
    public int PokemonId { get; set; }

    [Required]
    public bool IsAvailable { get; set; }

    [Required]
    public required Game Game { get; set; } // Added 'required' modifier

    [Required]
    public required Pokemon Pokemon { get; set; } // Added 'required' modifier
}

public class TypeEffectiveness
{
    [Key, Column(Order = 0)]
    public required string AttackingType { get; set; } // Added 'required' modifier

    [Key, Column(Order = 1)]
    public required string DefendingType { get; set; } // Added 'required' modifier

    [Required]
    public float EffectivenessMultiplier { get; set; }
}

public class ExclusivityGroup
{
    [Key]
    public int GroupId { get; set; }

    [Required]
    public int GameId { get; set; }

    public string? Description { get; set; }
}

public class ExclusivityGroupMember
{
    [Key, Column(Order = 0)]
    public int GroupId { get; set; }

    [Key, Column(Order = 1)]
    public int PokemonId { get; set; }
}
