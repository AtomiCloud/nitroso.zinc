using CSharp_Result;
using Domain;
using Domain.Withdrawal;

namespace App.Modules.Withdrawals.API.V1;

public record PreparedWithdrawalExport(
  WithdrawalSearch Search,
  IReadOnlyList<Withdrawal> FirstPage,
  IReadOnlyList<string> FirstReceiptLinks
);

public interface IWithdrawalExporter
{
  // Fetches the first page before HTTP headers/body are written, allowing a
  // repository or storage failure to remain a normal API error response.
  Task<Result<PreparedWithdrawalExport>> Prepare(
    WithdrawalSearch search,
    CancellationToken cancellationToken = default
  );

  // Writes one complete CSV stream. A failure after output begins is returned
  // to the controller so it can abort the connection rather than hand over a
  // plausible-but-truncated evidence file.
  Task<Result<Unit>> Write(
    PreparedWithdrawalExport export,
    TextWriter writer,
    CancellationToken cancellationToken = default
  );
}

public class WithdrawalExporter(
  IWithdrawalExportRepository repository,
  IWithdrawalStorage storage
) : IWithdrawalExporter
{
  public const int PageSize = 100;

  public async Task<Result<PreparedWithdrawalExport>> Prepare(
    WithdrawalSearch search,
    CancellationToken cancellationToken = default
  )
  {
    cancellationToken.ThrowIfCancellationRequested();
    var firstPage = await repository.SearchExport(
      search,
      PageSize,
      cursor: null,
      cancellationToken: cancellationToken
    );
    if (firstPage.IsFailure())
      return firstPage.FailureOrDefault();

    var receiptLinks = await this.GetReceiptLinks(firstPage.SuccessOrDefault());
    return receiptLinks.IsFailure()
      ? receiptLinks.FailureOrDefault()
      : new PreparedWithdrawalExport(
        search,
        firstPage.SuccessOrDefault(),
        receiptLinks.SuccessOrDefault()
      );
  }

  public async Task<Result<Unit>> Write(
    PreparedWithdrawalExport export,
    TextWriter writer,
    CancellationToken cancellationToken = default
  )
  {
    cancellationToken.ThrowIfCancellationRequested();
    await writer.WriteAsync(WithdrawalCsv.Bom);
    await writer.WriteAsync(WithdrawalCsv.HeaderLine);
    await writer.WriteAsync(WithdrawalCsv.LineEnding);

    var page = export.FirstPage;
    var receiptLinks = export.FirstReceiptLinks;
    while (true)
    {
      await this.WritePage(page, receiptLinks, writer, cancellationToken);

      await writer.FlushAsync();
      if (page.Count < PageSize)
        return new Unit();

      var cursor = new WithdrawalExportCursor(page[^1].Principal.CreatedAt, page[^1].Principal.Id);
      cancellationToken.ThrowIfCancellationRequested();
      var nextPage = await repository.SearchExport(
        export.Search,
        PageSize,
        cursor,
        cancellationToken
      );
      if (nextPage.IsFailure())
        return nextPage.FailureOrDefault();
      page = nextPage.SuccessOrDefault();
      var nextReceiptLinks = await this.GetReceiptLinks(page);
      if (nextReceiptLinks.IsFailure())
        return nextReceiptLinks.FailureOrDefault();
      receiptLinks = nextReceiptLinks.SuccessOrDefault();
    }
  }

  private async Task WritePage(
    IReadOnlyList<Withdrawal> page,
    IReadOnlyList<string> receiptLinks,
    TextWriter writer,
    CancellationToken cancellationToken
  )
  {
    cancellationToken.ThrowIfCancellationRequested();
    for (var i = 0; i < page.Count; i++)
    {
      cancellationToken.ThrowIfCancellationRequested();
      await writer.WriteAsync(WithdrawalCsv.Line(page[i].ToExportRow(receiptLinks[i])));
      await writer.WriteAsync(WithdrawalCsv.LineEnding);
    }
  }

  private async Task<Result<IReadOnlyList<string>>> GetReceiptLinks(IReadOnlyList<Withdrawal> page)
  {
    var links = await Task.WhenAll(page.Select(this.GetReceiptLink));
    foreach (var link in links)
      if (link.IsFailure())
        return link.FailureOrDefault();
    return links.Select(x => x.SuccessOrDefault()).ToArray();
  }

  // S3/MinIO cap presigned GET expiry at 7 days, so use the documented maximum
  // so a freshly exported link lasts as long as the object store allows. The
  // durable reference an accountant files the CSV against is the trailing
  // `receipt_key` column, which stays re-signable long after this link lapses.
  private static readonly TimeSpan ReceiptLinkExpiry = TimeSpan.FromDays(7);

  private Task<Result<string>> GetReceiptLink(Withdrawal withdrawal)
  {
    var receipt = withdrawal.Principal.Complete?.Receipt;
    return receipt is null
      ? Task.FromResult<Result<string>>("")
      : storage.Get(receipt, ReceiptLinkExpiry);
  }
}
