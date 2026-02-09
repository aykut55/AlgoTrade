using AlgoTrade.Core;
using AlgoTrade.Core.Trading;

AppSettings.EnsureDirectories();

Console.Clear();
Console.WriteLine("#######################################\n");

var trader = new AlgoTrader("MyStrategy");

trader.MessageReceived += message => Console.WriteLine(message);

trader.Start();
Thread.Sleep(100);
trader.Stop();

Console.WriteLine("\n#######################################\n");
Console.WriteLine("\nÇıkmak için bir tuşa basın...");
Console.ReadKey();
