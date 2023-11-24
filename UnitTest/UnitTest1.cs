namespace UnitTest;

public class UnitTest1
{
  [Fact]
  public void Short_Work()
  {
    var actual = "ABCDEFGHI";
    actual.Should()
      .StartWith("AB").And
      .EndWith("HI").And
      .Contain("EF").And
      .HaveLength(9);
  }
}
