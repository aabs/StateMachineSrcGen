using StateMachineSrcGen.Playground;

Console.WriteLine("StateMachineSrcGen Playground");
Console.WriteLine("=============================");
Console.WriteLine();

var result = await OrderMachine.HandleAsync(new OrderEvent("confirm"));
Console.WriteLine($"Transition result: {result}");
