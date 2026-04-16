#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;

namespace WileyWidget.Data;

/// <summary>
/// Repository implementation for vendor/supplier management
/// </summary>
public class VendorRepository : IVendorRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILogger<VendorRepository> _logger;

    public VendorRepository(IDbContextFactory<AppDbContext> contextFactory, ILogger<VendorRepository> logger)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public async Task<IReadOnlyList<Vendor>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("VendorRepository: Getting all vendors");
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Vendors
            .AsNoTracking()
            .OrderBy(v => v.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Vendor>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("VendorRepository: Getting active vendors");
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Vendors
            .AsNoTracking()
            .Where(v => v.IsActive)
            .OrderBy(v => v.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Vendor?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("VendorRepository: Getting vendor by ID {Id}", id);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Vendors
            .AsNoTracking()
            .OrderByDescending(v => v.Id)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<Vendor?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var normalized = name.Trim();
        _logger.LogDebug("VendorRepository: Getting vendor by name {Name}", normalized);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var vendors = await context.Vendors
            .AsNoTracking()
            .OrderByDescending(v => v.Id)
            .ToListAsync(cancellationToken);

        return vendors.FirstOrDefault(v => string.Equals(v.Name, normalized, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<Vendor>> SearchByNameAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllAsync(cancellationToken);
        }

        var normalized = searchTerm.Trim();
        _logger.LogDebug("VendorRepository: Searching vendors by term {SearchTerm}", normalized);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Vendors
            .AsNoTracking()
            .Where(v => EF.Functions.Like(v.Name, $"%{normalized}%"))
            .OrderBy(v => v.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Vendor> AddAsync(Vendor vendor, CancellationToken cancellationToken = default)
    {
        if (vendor == null)
        {
            throw new ArgumentNullException(nameof(vendor));
        }

        vendor.Name = vendor.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(vendor.Name))
        {
            throw new ArgumentException("Vendor name is required", nameof(vendor));
        }

        vendor.ContactInfo = NormalizeOptional(vendor.ContactInfo);
        vendor.Email = NormalizeOptional(vendor.Email);
        vendor.Phone = NormalizeOptional(vendor.Phone);
        vendor.MailingAddressLine1 = NormalizeOptional(vendor.MailingAddressLine1);
        vendor.MailingAddressLine2 = NormalizeOptional(vendor.MailingAddressLine2);
        vendor.MailingAddressCity = NormalizeOptional(vendor.MailingAddressCity);
        vendor.MailingAddressState = NormalizeOptional(vendor.MailingAddressState);
        vendor.MailingAddressPostalCode = NormalizeOptional(vendor.MailingAddressPostalCode);
        vendor.MailingAddressCountry = NormalizeOptional(vendor.MailingAddressCountry);
        vendor.QuickBooksId = NormalizeOptional(vendor.QuickBooksId);

        var existing = await GetByNameAsync(vendor.Name, cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException($"Vendor '{vendor.Name}' already exists");
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        context.Vendors.Add(vendor);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("VendorRepository: Vendor {Id} added successfully", vendor.Id);
        return vendor;
    }

    public async Task UpdateAsync(Vendor vendor, CancellationToken cancellationToken = default)
    {
        if (vendor == null)
        {
            throw new ArgumentNullException(nameof(vendor));
        }

        vendor.Name = vendor.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(vendor.Name))
        {
            throw new ArgumentException("Vendor name is required", nameof(vendor));
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await context.Vendors.FindAsync(new object[] { vendor.Id }, cancellationToken);
        if (existing == null)
        {
            throw new InvalidOperationException($"Vendor {vendor.Id} not found");
        }

        if (!string.Equals(existing.Name, vendor.Name, StringComparison.OrdinalIgnoreCase))
        {
            var duplicate = await GetByNameAsync(vendor.Name, cancellationToken);
            if (duplicate != null && duplicate.Id != vendor.Id)
            {
                throw new InvalidOperationException($"Vendor '{vendor.Name}' already exists");
            }
        }

        existing.Name = vendor.Name;
        existing.ContactInfo = NormalizeOptional(vendor.ContactInfo);
        existing.Email = NormalizeOptional(vendor.Email);
        existing.Phone = NormalizeOptional(vendor.Phone);
        existing.MailingAddressLine1 = NormalizeOptional(vendor.MailingAddressLine1);
        existing.MailingAddressLine2 = NormalizeOptional(vendor.MailingAddressLine2);
        existing.MailingAddressCity = NormalizeOptional(vendor.MailingAddressCity);
        existing.MailingAddressState = NormalizeOptional(vendor.MailingAddressState);
        existing.MailingAddressPostalCode = NormalizeOptional(vendor.MailingAddressPostalCode);
        existing.MailingAddressCountry = NormalizeOptional(vendor.MailingAddressCountry);
        existing.QuickBooksId = NormalizeOptional(vendor.QuickBooksId);
        existing.IsActive = vendor.IsActive;

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var vendor = await context.Vendors.FindAsync(new object[] { id }, cancellationToken);
        if (vendor == null)
        {
            throw new InvalidOperationException($"Vendor {id} not found");
        }

        context.Vendors.Remove(vendor);
        await context.SaveChangesAsync(cancellationToken);
    }
}
