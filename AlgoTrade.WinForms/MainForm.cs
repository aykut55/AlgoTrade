using AlgoTrade.Core;
using AlgoTrade.Core.Trading;

namespace AlgoTrade.WinForms;

public class MainForm : Form
{
    private readonly AlgoTrader _trader;
    private readonly ListBox _logList;
    private readonly Button _startButton;
    private readonly Button _stopButton;

    public MainForm()
    {
        Text = "AlgoTrade";
        Size = new System.Drawing.Size(800, 600);
        StartPosition = FormStartPosition.CenterScreen;

        _startButton = new Button { Text = "Start", Location = new(20, 20), Width = 100 };
        _stopButton = new Button { Text = "Stop", Location = new(130, 20), Width = 100, Enabled = false };
        _logList = new ListBox { Location = new(20, 60), Size = new(740, 480) };

        _startButton.Click += (_, _) => StartTrader();
        _stopButton.Click += (_, _) => StopTrader();

        Controls.AddRange([_startButton, _stopButton, _logList]);

        _trader = new AlgoTrader("MyStrategy");
        _trader.MessageReceived += message =>
        {
            if (InvokeRequired)
                Invoke(() => _logList.Items.Add(message));
            else
                _logList.Items.Add(message);
        };
    }

    private void StartTrader()
    {
        _trader.Start();
        _startButton.Enabled = false;
        _stopButton.Enabled = true;
    }

    private void StopTrader()
    {
        _trader.Stop();
        _startButton.Enabled = true;
        _stopButton.Enabled = false;
    }
}
