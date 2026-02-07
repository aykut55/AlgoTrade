using AlgoTrade.Core;

namespace AlgoTrade.WinForms;

public class MainForm : Form
{
    public MainForm()
    {
        Text = "AlgoTrade";
        Size = new System.Drawing.Size(800, 600);
        StartPosition = FormStartPosition.CenterScreen;

        var label = new Label
        {
            Text = $"Inputs: {AppSettings.InputsDir}\nOutputs: {AppSettings.OutputsDir}",
            AutoSize = true,
            Location = new System.Drawing.Point(20, 20)
        };
        Controls.Add(label);
    }
}
