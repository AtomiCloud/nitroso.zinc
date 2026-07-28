using App.Modules.Common;
using App.Modules.Withdrawals.Data;
using CSharp_Result;
using Domain;
using FluentAssertions;

namespace UnitTest.Withdrawals;

public class WithdrawalStorageTests
{
  [Theory]
  [InlineData(-1)]
  [InlineData(0)]
  [InlineData(0.5)]
  [InlineData(604800.001)]
  public async Task Explicit_expiry_rejects_values_outside_the_minio_presign_range(double seconds)
  {
    var file = new FakeFileRepository();
    var storage = new WithdrawalStorage(file);

    var result = await storage.Get("receipt-key", TimeSpan.FromSeconds(seconds));

    result.IsFailure().Should().BeTrue();
    result.FailureOrDefault().Should().BeOfType<ArgumentOutOfRangeException>();
    file.SignedLinkRequests.Should().BeEmpty("invalid expiry must not reach the MinIO client");
  }

  [Theory]
  [InlineData(1, 1)]
  [InlineData(604800, 604800)]
  public async Task Explicit_expiry_accepts_the_inclusive_minio_boundaries(
    double seconds,
    int expectedSeconds
  )
  {
    var file = new FakeFileRepository();
    var storage = new WithdrawalStorage(file);

    var result = await storage.Get("receipt-key", TimeSpan.FromSeconds(seconds));

    result.IsSuccess().Should().BeTrue();
    result.SuccessOrDefault().Should().Be("signed-url");
    var request = file.SignedLinkRequests.Should().ContainSingle().Which;
    request.Key.Should().Be("receipt-key");
    request.Seconds.Should().Be(expectedSeconds);
  }

  [Fact]
  public async Task Interactive_link_keeps_the_one_hour_expiry()
  {
    var file = new FakeFileRepository();
    var storage = new WithdrawalStorage(file);

    var result = await storage.Get("receipt-key");

    result.IsSuccess().Should().BeTrue();
    file.SignedLinkRequests.Should().ContainSingle().Which.Seconds.Should().Be(3600);
  }

  private sealed class FakeFileRepository : IFileRepository
  {
    public List<(string Store, string Key, int Seconds)> SignedLinkRequests { get; } = [];

    public Task<Result<string>> SignedLink(string store, string key, int seconds)
    {
      this.SignedLinkRequests.Add((store, key, seconds));
      return Task.FromResult((Result<string>)"signed-url");
    }

    public Task<Result<string>> Save(
      string store,
      string dir,
      string name,
      byte[] content,
      bool appendExt
    ) => throw new NotImplementedException();

    public Task<Result<string>> Save(
      string store,
      string dir,
      string name,
      string content,
      bool appendExt
    ) => throw new NotImplementedException();

    public Task<Result<string>> Save(
      string store,
      string dir,
      string name,
      Stream content,
      bool appendExt
    ) => throw new NotImplementedException();

    public Task<Result<string>> Link(string store, string key) =>
      throw new NotImplementedException();

    public Task<Result<bool>> Exists(string store, string key) =>
      throw new NotImplementedException();

    public Task<Result<Unit>> Remove(string store, string key) =>
      throw new NotImplementedException();
  }
}
