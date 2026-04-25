#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WileyWidget.Models;

/// <summary>
/// Represents one apartment unit type within the Apartments enterprise.
/// </summary>
public class ApartmentUnitType : ISoftDeletable
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int EnterpriseId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(1, 20)]
    public int BedroomCount { get; set; }

    [Range(0, int.MaxValue)]
    public int UnitCount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal MonthlyRent { get; set; }

    [NotMapped]
    public decimal MonthlyRevenue => UnitCount * MonthlyRent;

    [NotMapped]
    public decimal EffectiveCustomerCount => UnitCount * BedroomCount;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime? ModifiedDate { get; set; }

    public string? CreatedBy { get; set; }

    public string? ModifiedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedDate { get; set; }

    public string? DeletedBy { get; set; }

    public virtual Enterprise? Enterprise { get; set; }
}