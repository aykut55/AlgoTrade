using AlgoTrade.Core;

AppSettings.EnsureDirectories();

Console.WriteLine("AlgoTrade Console");
Console.WriteLine($"Inputs : {AppSettings.InputsDir}");
Console.WriteLine($"Outputs: {AppSettings.OutputsDir}");
