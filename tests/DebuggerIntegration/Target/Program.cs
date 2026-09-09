using System;
using System.Threading;
int value = 40;
value = AddTwo(value);
Console.WriteLine($"RESULT={value}");
for (int i = 0; i < 3; i++)
{
    Console.WriteLine($"TICK={i}");
    Thread.Sleep(1500);
}
static int AddTwo(int input)
{
    int result = input + 2;
    return result;
}
