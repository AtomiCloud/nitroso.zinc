namespace App.Modules.Timings.API.V1;

// REQ
public record TrainDirectionReq(string Direction);

public record TimingReq(string[] Timings);

// RESP
public record TimingPrincipalRes(string Direction, string[] Timings);

public record TimingRes(TimingPrincipalRes Principal);
