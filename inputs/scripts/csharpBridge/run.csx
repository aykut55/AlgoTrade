// Enabled: 1 = calistir, 0 = atla. Run from Signal, Signal adimindan baslar.
return new[]
{
    (Step: "Read Data",           Enabled: 1),
    (Step: "Signal",              Enabled: 1),
    (Step: "Filter",              Enabled: 1),
    (Step: "Calculate",           Enabled: 1),
    (Step: "Plot",                Enabled: 1),
    (Step: "Write To Disk",       Enabled: 1),
    (Step: "Create Bundle Files", Enabled: 1)
};
