using System.Text.Json;
using App.Error.V1;
using App.StartUp.Database;
using App.Utility;
using CSharp_Result;
using Domain.Exceptions;
using Domain.Invoice;
using EntityFramework.Exceptions.Common;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Invoices.Data;

public static class InvoiceDocumentDataMapper
{
  // The frozen JSON is written and read with EXPLICIT options rather than
  // the ambient default, because these bytes outlive the process that wrote
  // them. A future change to a global serializer setting must not silently
  // change how a settled invoice deserializes.
  public static readonly JsonSerializerOptions Frozen = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    WriteIndented = false,
  };

  public static InvoiceDocumentRecord ToRecord(this InvoiceDocumentData data) =>
    new()
    {
      PeriodMonth = data.PeriodMonth,
      Seq = data.Seq,
      Status = (InvoiceStatus)data.Status,
      TicketBasis = (InvoiceTicketBasis)data.TicketBasis,
      EngineVersion = data.EngineVersion,
      IssueDate = data.IssueDate,
      DueDate = data.DueDate,
    };

  public static InvoiceDocumentSummary ToSummary(this InvoiceDocumentData data) =>
    new()
    {
      Id = data.Id,
      Record = data.ToRecord(),
      NetProfit = data.NetProfit,
      PoolTotal = data.PoolTotal,
      CreatedAt = data.CreatedAt,
      IssuedAt = data.IssuedAt,
    };

  public static InvoiceDocument ToDomain(this InvoiceDocumentData data) =>
    new()
    {
      Id = data.Id,
      Record = data.ToRecord(),
      InputsJson = data.InputsJson,
      ComputedJson = data.ComputedJson,
      Figures = Figures(data),
      CreatedAt = data.CreatedAt,
      CreatedBy = data.CreatedBy,
      IssuedAt = data.IssuedAt,
      IssuedBy = data.IssuedBy,
      VoidedAt = data.VoidedAt,
      VoidedBy = data.VoidedBy,
      VoidReason = data.VoidReason,
    };

  // Reads the money back out of the stored document. Deserializing into
  // InvoiceComputed would couple every historical row to today's record
  // shape; this reads the dozen figures the drift check and the list page
  // need, and nothing else.
  private static InvoiceFigures Figures(InvoiceDocumentData data)
  {
    var c = JsonSerializer.Deserialize<InvoiceComputed>(data.ComputedJson, Frozen)
      ?? throw new ApplicationException($"Invoice {data.Id} has an unreadable ComputedJson");
    return InvoiceFigures.Of(c);
  }

  public static InvoiceMonthInput ToInputs(this InvoiceDocument doc) =>
    JsonSerializer.Deserialize<InvoiceMonthInput>(doc.InputsJson, Frozen)
    ?? throw new ApplicationException($"Invoice {doc.Id} has an unreadable InputsJson");

  public static InvoiceComputed ToComputed(this InvoiceDocument doc) =>
    JsonSerializer.Deserialize<InvoiceComputed>(doc.ComputedJson, Frozen)
    ?? throw new ApplicationException($"Invoice {doc.Id} has an unreadable ComputedJson");
}

// Stored partner invoices. See Domain/Invoice/InvoiceDocument.cs for why both
// halves are frozen rather than recomputed.
public class InvoiceDocumentRepository(MainDbContext db, ILogger<InvoiceDocumentRepository> logger)
  : IInvoiceDocumentRepository
{
  public async Task<Result<IEnumerable<InvoiceDocumentSummary>>> List()
  {
    try
    {
      // The JSON columns are projected away rather than loaded and dropped:
      // a year of invoices is a year of deep documents, and the list page
      // needs none of it.
      var rows = await db
        .InvoiceDocuments.OrderByDescending(x => x.PeriodMonth)
        .ThenByDescending(x => x.CreatedAt)
        .Select(x => new InvoiceDocumentData
        {
          Id = x.Id,
          PeriodMonth = x.PeriodMonth,
          Seq = x.Seq,
          Status = x.Status,
          TicketBasis = x.TicketBasis,
          EngineVersion = x.EngineVersion,
          IssueDate = x.IssueDate,
          DueDate = x.DueDate,
          NetProfit = x.NetProfit,
          PoolTotal = x.PoolTotal,
          CreatedAt = x.CreatedAt,
          IssuedAt = x.IssuedAt,
        })
        .ToArrayAsync();
      return rows.Select(x => x.ToSummary()).ToResult();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to list invoices");
      return e;
    }
  }

  public async Task<Result<InvoiceDocument?>> Get(Guid id)
  {
    try
    {
      var row = await db.InvoiceDocuments.FirstOrDefaultAsync(x => x.Id == id);
      return row?.ToDomain();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to get invoice {Id}", id);
      return e;
    }
  }

  public async Task<Result<InvoiceDocument?>> GetIssued(DateOnly periodMonth)
  {
    try
    {
      var row = await db.InvoiceDocuments.FirstOrDefaultAsync(x =>
        x.PeriodMonth == periodMonth && x.Status == (byte)InvoiceStatus.Issued
      );
      return row?.ToDomain();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to get the issued invoice for {Month}", periodMonth);
      return e;
    }
  }

  public async Task<Result<InvoiceDocument>> SaveDraft(InvoiceDocumentDraft draft, string? by)
  {
    try
    {
      // Refuse to draft over a month that is already settled. Without this
      // the operator can build a full draft, hit Issue, and only then be told
      // the month is closed — after the work, not before it.
      var issued = await db.InvoiceDocuments.AnyAsync(x =>
        x.PeriodMonth == draft.PeriodMonth && x.Status == (byte)InvoiceStatus.Issued
      );
      if (issued)
      {
        return new EntityConflict(
          $"Invoice for {draft.PeriodMonth:yyyy-MM} has already been issued; void it before drafting again",
          typeof(InvoiceDocument)
        ).ToException();
      }

      var computed = InvoiceCalculator.Compute(draft.Inputs);
      var now = DateTime.UtcNow;

      // A draft is scratch space, so re-drafting a month replaces it rather
      // than accumulating rows nobody will ever choose between.
      var existing = await db.InvoiceDocuments.FirstOrDefaultAsync(x =>
        x.PeriodMonth == draft.PeriodMonth && x.Status == (byte)InvoiceStatus.Draft
      );
      var data = existing ?? new InvoiceDocumentData { CreatedAt = now, CreatedBy = by };

      Apply(data, draft, computed, InvoiceStatus.Draft, InvoiceEngine.Version);

      if (existing is null)
        db.InvoiceDocuments.Add(data);

      await db.SaveChangesAsync();
      logger.LogInformation(
        "Saved invoice draft for {Month}: net profit {NetProfit}, pool {Pool}",
        draft.PeriodMonth,
        data.NetProfit,
        data.PoolTotal
      );
      return data.ToDomain();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to save the invoice draft for {Month}", draft.PeriodMonth);
      return e;
    }
  }

  public async Task<Result<InvoiceDocument>> Issue(Guid id, string? by)
  {
    try
    {
      var data = await db.InvoiceDocuments.FirstOrDefaultAsync(x => x.Id == id);
      if (data is null)
        return new NotFoundException($"Invoice {id} not found", typeof(InvoiceDocument), id.ToString());

      if (data.Status != (byte)InvoiceStatus.Draft)
      {
        return new EntityConflict(
          $"Invoice {id} is {(InvoiceStatus)data.Status}, only a Draft can be issued",
          typeof(InvoiceDocument)
        ).ToException();
      }

      // The figures are NOT recomputed here. Issuing freezes what the
      // operator reviewed; recomputing at this moment would mean the numbers
      // they approved and the numbers that were sent could differ if anything
      // moved in between.
      data.Status = (byte)InvoiceStatus.Issued;
      data.IssuedAt = DateTime.UtcNow;
      data.IssuedBy = by;

      await db.SaveChangesAsync();
      logger.LogInformation(
        "Issued invoice {Id} for {Month}: pool {Pool}",
        id,
        data.PeriodMonth,
        data.PoolTotal
      );
      return data.ToDomain();
    }
    // The partial unique index is the real guard: two concurrent issues of the
    // same month both pass the status check above and one loses here.
    catch (UniqueConstraintException e)
    {
      logger.LogError(e, "Failed to issue invoice {Id}: the month already has one", id);
      return new EntityConflict(
        $"Another invoice for this month has already been issued",
        typeof(InvoiceDocument)
      ).ToException();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to issue invoice {Id}", id);
      return e;
    }
  }

  public async Task<Result<InvoiceDocument>> Void(Guid id, string reason, string? by)
  {
    try
    {
      var data = await db.InvoiceDocuments.FirstOrDefaultAsync(x => x.Id == id);
      if (data is null)
        return new NotFoundException($"Invoice {id} not found", typeof(InvoiceDocument), id.ToString());

      if (data.Status != (byte)InvoiceStatus.Issued)
      {
        return new EntityConflict(
          $"Invoice {id} is {(InvoiceStatus)data.Status}, only an Issued invoice can be voided",
          typeof(InvoiceDocument)
        ).ToException();
      }

      // The row and both frozen halves stay exactly as they are. An invoice
      // that was sent and then retracted is part of the record, and so is
      // what it said.
      data.Status = (byte)InvoiceStatus.Void;
      data.VoidedAt = DateTime.UtcNow;
      data.VoidedBy = by;
      data.VoidReason = reason;

      await db.SaveChangesAsync();
      logger.LogWarning(
        "Voided invoice {Id} for {Month}: {Reason}",
        id,
        data.PeriodMonth,
        reason
      );
      return data.ToDomain();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to void invoice {Id}", id);
      return e;
    }
  }

  public async Task<Result<InvoiceDocument>> Transcribe(
    InvoiceDocumentDraft draft,
    InvoiceComputed computed,
    DateTime issuedAt,
    string? by
  )
  {
    try
    {
      var already = await db.InvoiceDocuments.AnyAsync(x =>
        x.PeriodMonth == draft.PeriodMonth && x.Status == (byte)InvoiceStatus.Issued
      );
      if (already)
      {
        return new EntityConflict(
          $"Invoice for {draft.PeriodMonth:yyyy-MM} has already been issued",
          typeof(InvoiceDocument)
        ).ToException();
      }

      var data = new InvoiceDocumentData
      {
        CreatedAt = DateTime.UtcNow,
        CreatedBy = by,
        IssuedAt = issuedAt,
        IssuedBy = by,
      };

      // EngineVersion 0 means "produced before this engine existed". The
      // figures are the issued PDF's, and a drift report against them is
      // expected to be non-empty — that is information, not a fault.
      Apply(data, draft, computed, InvoiceStatus.Issued, 0);

      db.InvoiceDocuments.Add(data);
      await db.SaveChangesAsync();
      logger.LogInformation(
        "Transcribed the issued invoice for {Month}: pool {Pool}",
        draft.PeriodMonth,
        data.PoolTotal
      );
      return data.ToDomain();
    }
    catch (UniqueConstraintException e)
    {
      logger.LogError(e, "Failed to transcribe {Month}: already issued", draft.PeriodMonth);
      return new EntityConflict(
        $"Invoice for {draft.PeriodMonth:yyyy-MM} has already been issued",
        typeof(InvoiceDocument)
      ).ToException();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to transcribe the invoice for {Month}", draft.PeriodMonth);
      return e;
    }
  }

  private static void Apply(
    InvoiceDocumentData data,
    InvoiceDocumentDraft draft,
    InvoiceComputed computed,
    InvoiceStatus status,
    int engineVersion
  )
  {
    data.PeriodMonth = draft.PeriodMonth;
    data.Seq = draft.Seq;
    data.Status = (byte)status;
    data.TicketBasis = (byte)draft.TicketBasis;
    data.EngineVersion = engineVersion;
    data.IssueDate = draft.IssueDate;
    data.DueDate = draft.DueDate;
    data.InputsJson = JsonSerializer.Serialize(
      draft.Inputs,
      InvoiceDocumentDataMapper.Frozen
    );
    data.ComputedJson = JsonSerializer.Serialize(computed, InvoiceDocumentDataMapper.Frozen);
    data.NetProfit = computed.Result.NetProfit;
    data.PoolTotal = computed.Result.MarketingSharePool;
  }
}
