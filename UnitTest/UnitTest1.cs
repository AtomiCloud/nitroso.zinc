namespace UnitTest;

public class UnitTest1
{
  [Fact]
  public void Short_Work()
  {
    var date = new DateOnly(2023, 1, 23);
    var time = new TimeOnly(12, 55, 22);

    var dateTime = date.ToDateTime(time);

    var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Singapore");

    // var f = dateTime.ToUniversalTime();

    var t = TimeZoneInfo.ConvertTimeToUtc(dateTime, timeZone);
    // Convert DateTime to DateTimeOffset considering the timezone
    t.ToString().Should().Be("23/1/2023 4:55:22 AM");

  }
}
