#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Runtime.CompilerServices;

namespace WileyWidget.Models;

public enum EnterpriseStatus
{
    Active,
    Inactive,
    Suspended
}

/// <summary>
/// Represents a municipal enterprise (Water, Sewer, Trash, Apartments)
/// Implements audit tracking and soft delete for compliance
/// </summary>
public partial class Enterprise : ISoftDeletable
{
    /// <summary>
    /// <summary>
    /// Preserved for setter compatibility.
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
    }

    /// <summary>
    /// Raises PropertyChanged for a specific property
    /// </summary>
    protected void OnPropertyChanged(params string[] propertyNames)
    {
        if (propertyNames == null) throw new ArgumentNullException(nameof(propertyNames));

        foreach (var propertyName in propertyNames)
        {
            OnPropertyChanged(propertyName);
        }
    }

    /// <summary>
    /// Default constructor
    /// </summary>
    public Enterprise()
    {
        _name = string.Empty;
        _type = string.Empty;
        _notes = string.Empty;
    }

    [Key]
    [GridDisplay(99, 80, Visible = true)]
    public int Id { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    private string _name;

    [Required(ErrorMessage = "Enterprise name is required")]
    [StringLength(100, ErrorMessage = "Enterprise name cannot exceed 100 characters")]
    [GridDisplay(1, 150)]
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
    }

    private string? _description;

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description
    {
        get => _description;
        set
        {
            var sanitized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (!string.Equals(_description, sanitized, StringComparison.Ordinal))
            {
                _description = sanitized;
                OnPropertyChanged(nameof(Description));
            }
        }
    }

    private decimal _currentRate;

    [Required(ErrorMessage = "Current rate is required")]
    [Range(0.01, 9999.99, ErrorMessage = "Rate must be between 0.01 and 9999.99")]
    [Column(TypeName = "decimal(18,2)")]
    [GridDisplay(3, 100, DecimalDigits = 2)]
    public decimal CurrentRate
    {
        get => _currentRate;
        set
        {
            if (_currentRate != value)
            {
                _currentRate = value;
                OnPropertyChanged(nameof(CurrentRate), nameof(MonthlyRevenue), nameof(MonthlyBalance), nameof(BreakEvenRate), nameof(RevenuePerCustomer), nameof(BalancePerCustomer));
            }
        }
    }

    private decimal _monthlyExpenses;

    [Required(ErrorMessage = "Monthly expenses are required")]
    [Range(0, double.MaxValue, ErrorMessage = "Monthly expenses cannot be negative")]
    [Column(TypeName = "decimal(18,2)")]
    [GridDisplay(5, 120, DecimalDigits = 2)]
    public decimal MonthlyExpenses
    {
        get => _monthlyExpenses;
        set
        {
            if (_monthlyExpenses != value)
            {
                _monthlyExpenses = value;
                OnPropertyChanged(nameof(MonthlyExpenses), nameof(MonthlyBalance));
            }
        }
    }

    [NotMapped]
    [GridDisplay(6, 120, DecimalDigits = 2)]
    public decimal MonthlyRevenue => string.Equals(Type, "Apartments", StringComparison.OrdinalIgnoreCase)
        ? ApartmentUnitTypes.Sum(unitType => unitType.MonthlyRevenue)
        : CitizenCount * CurrentRate;

    [NotMapped]
    [GridDisplay(6, 120, DecimalDigits = 2)]
    public decimal RevenuePerCustomer => EffectiveCustomerCount > 0 ? MonthlyRevenue / EffectiveCustomerCount : 0m;

    private int _citizenCount;

    [Required(ErrorMessage = "Citizen count is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Citizen count must be at least 1")]
    [GridDisplay(4, 80, DecimalDigits = 0)]
    public int CitizenCount
    {
        get => _citizenCount;
        set
        {
            if (_citizenCount != value)
            {
                _citizenCount = value;
                OnPropertyChanged(nameof(CitizenCount), nameof(MonthlyRevenue), nameof(MonthlyBalance), nameof(BreakEvenRate), nameof(EffectiveCustomerCount), nameof(RevenuePerCustomer), nameof(BalancePerCustomer));
            }
        }
    }

    [NotMapped]
    [GridDisplay(7, 120, DecimalDigits = 2)]
    public decimal BalancePerCustomer => EffectiveCustomerCount > 0 ? MonthlyBalance / EffectiveCustomerCount : 0m;

    private decimal _totalBudget;

    [Column(TypeName = "decimal(18,2)")]
    [GridDisplay(8, 120, DecimalDigits = 2)]
    public decimal TotalBudget
    {
        get => _totalBudget;
        set
        {
            if (_totalBudget != value)
            {
                _totalBudget = value;
                OnPropertyChanged(nameof(TotalBudget));
            }
        }
    }

    private decimal _budgetAmount;

    [Column(TypeName = "decimal(18,2)")]
    public decimal BudgetAmount
    {
        get => _budgetAmount;
        set
        {
            if (_budgetAmount != value)
            {
                _budgetAmount = value;
                OnPropertyChanged(nameof(BudgetAmount));
            }
        }
    }

    public DateTime? LastModified { get; set; }

    private string? _type;

    [StringLength(50, ErrorMessage = "Type cannot exceed 50 characters")]
    public string? Type
    {
        get => _type;
        set
        {
            var sanitized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            if (!string.Equals(_type, sanitized, StringComparison.Ordinal))
            {
                _type = sanitized;
                OnPropertyChanged(nameof(Type));
            }
        }
    }

    private string? _notes;

    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
    [GridDisplay(9, 200)]
    public string? Notes
    {
        get => _notes;
        set
        {
            var sanitized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            if (!string.Equals(_notes, sanitized, StringComparison.Ordinal))
            {
                _notes = sanitized;
                OnPropertyChanged(nameof(Notes));
            }
        }
    }

    private EnterpriseStatus _status = EnterpriseStatus.Active;

    [GridDisplay(7, 100)]
    public EnterpriseStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }
    }

    [NotMapped]
    public DateTime LastUpdated => DateTime.Now;

    public virtual ICollection<BudgetInteraction> BudgetInteractions { get; set; } = new List<BudgetInteraction>();

    [NotMapped]
    [GridDisplay(7, 120, DecimalDigits = 2)]
    public decimal MonthlyBalance => MonthlyRevenue - MonthlyExpenses;

    [NotMapped]
    public decimal BreakEvenRate => EffectiveCustomerCount > 0 ? MonthlyExpenses / EffectiveCustomerCount : 0;

    [NotMapped]
    public string UnitLabel => string.Equals(Type, "Apartments", StringComparison.OrdinalIgnoreCase) ? "Units" : "Customers";

    [NotMapped]
    public decimal EffectiveCustomerCount
    {
        get
        {
            if (string.Equals(Type, "Apartments", StringComparison.OrdinalIgnoreCase))
            {
                return ApartmentUnitTypes.Sum(unitType => unitType.UnitCount * unitType.BedroomCount);
            }

            return CitizenCount;
        }
    }

    public virtual ICollection<ApartmentUnitType> ApartmentUnitTypes { get; set; } = new List<ApartmentUnitType>();

    [NotMapped]
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
    }
    private bool _isSelected;

    private decimal? _meterReading;

    [Column(TypeName = "decimal(18,2)")]
    [GridDisplay(10, 100, DecimalDigits = 2)]
    public decimal? MeterReading
    {
        get => _meterReading;
        set
        {
            if (_meterReading != value)
            {
                _meterReading = value;
                OnPropertyChanged(nameof(MeterReading), nameof(WaterConsumption));
            }
        }
    }

    private DateTime? _meterReadDate;

    [GridDisplay(11, 120)]
    public DateTime? MeterReadDate
    {
        get => _meterReadDate;
        set
        {
            if (_meterReadDate != value)
            {
                _meterReadDate = value;
                OnPropertyChanged(nameof(MeterReadDate));
            }
        }
    }

    private decimal? _previousMeterReading;

    [Column(TypeName = "decimal(18,2)")]
    [GridDisplay(12, 100, DecimalDigits = 2)]
    public decimal? PreviousMeterReading
    {
        get => _previousMeterReading;
        set
        {
            if (_previousMeterReading != value)
            {
                _previousMeterReading = value;
                OnPropertyChanged(nameof(PreviousMeterReading), nameof(WaterConsumption));
            }
        }
    }

    private DateTime? _previousMeterReadDate;

    [GridDisplay(13, 120)]
    public DateTime? PreviousMeterReadDate
    {
        get => _previousMeterReadDate;
        set
        {
            if (_previousMeterReadDate != value)
            {
                _previousMeterReadDate = value;
                OnPropertyChanged(nameof(PreviousMeterReadDate));
            }
        }
    }

    [NotMapped]
    [GridDisplay(14, 100, DecimalDigits = 2)]
    public decimal? WaterConsumption =>
        MeterReading.HasValue && PreviousMeterReading.HasValue
            ? MeterReading.Value - PreviousMeterReading.Value
            : null;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime? ModifiedDate { get; set; }

    public string? CreatedBy { get; set; }

    public string? ModifiedBy { get; set; }

    public DateTime CreatedAt { get => CreatedDate; set => CreatedDate = value; }

    public DateTime? UpdatedAt { get => ModifiedDate; set => ModifiedDate = value; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedDate { get; set; }

    public string? DeletedBy { get; set; }

    public bool IsProfitable() => MonthlyBalance > 0;

    public decimal CalculateBreakEvenVariance() => MonthlyRevenue - MonthlyExpenses;

    public decimal CalculateRateAdjustmentForTarget(decimal targetBalance)
    {
        if (CitizenCount == 0)
            return 0;

        var targetRevenue = MonthlyExpenses + targetBalance;
        var targetRate = targetRevenue / CitizenCount;
        return targetRate - CurrentRate;
    }

    public bool ValidateRateChange(decimal proposedRate, out string? errorMessage)
    {
        if (proposedRate < 0)
        {
            errorMessage = "Rate cannot be negative";
            return false;
        }

        if (proposedRate > 9999.99m)
        {
            errorMessage = "Rate exceeds maximum allowed value";
            return false;
        }

        var percentIncrease = CurrentRate > 0 ? ((proposedRate - CurrentRate) / CurrentRate) * 100 : 0;
        if (percentIncrease > 50)
        {
            errorMessage = $"Warning: Rate increase of {percentIncrease:F1}% exceeds typical adjustment range";
            return true;
        }

        errorMessage = null;
        return true;
    }
}