using System;
using ZenithFrame;

int assertions = 0;
for (int mask = 0; mask < 128; mask++)
{
    bool B(int bit) => (mask & (1 << bit)) != 0;
    bool actual = PilotRules.CanBoard(B(0), B(1), B(2), B(3), B(4), B(5), B(6));
    if (actual != (mask == 94)) throw new Exception("Board eligibility regression at " + mask);
    assertions++;
}
for (int mask = 0; mask < 16; mask++)
{
    bool B(int bit) => (mask & (1 << bit)) != 0;
    if (PilotRules.CanOperate(B(0), B(1), B(2), B(3)) != (mask == 15)) throw new Exception("Operation eligibility regression");
    assertions++;
}
Console.WriteLine($"PASS: {assertions} exhaustive eligibility cases");
