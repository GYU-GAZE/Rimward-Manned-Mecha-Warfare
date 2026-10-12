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
FrameVisualSource[] source = { FrameVisualSource.North, FrameVisualSource.Sides,
    FrameVisualSource.South, FrameVisualSource.Sides };
for (int facing = 0; facing < 4; facing++)
{
    if (FrameVisualDirection.SourceFor(facing) != source[facing] ||
        FrameVisualDirection.MirrorFor(facing) != (facing == 3))
        throw new Exception("Frame direction regression at " + facing);
    assertions++;
}
Console.WriteLine($"PASS: {assertions} eligibility and Frame direction cases");
