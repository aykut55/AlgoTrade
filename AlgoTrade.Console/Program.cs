using AlgoTrade.Core;
using AlgoTrade.Core.Trading;

AppSettings.EnsureDirectories();

var trader = new AlgoTrader("MyStrategy");
trader.MessageReceived += message => Console.WriteLine(message);

trader.Start();

Console.WriteLine("Çıkmak için bir tuşa basın...");
Console.ReadKey();

trader.Stop();
