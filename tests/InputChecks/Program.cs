using ProjectShockGL;
using ProjectShockGL.Core;
using System.Globalization;
using System.Reflection;

var count = 0;
void Check(bool ok) { if (!ok) throw new Exception($"Check {count+1} failed"); count++; }
var target = new Receiver();
InputInvoker.Invoke(target, "Number", "9", 2.5f); Check(target.Number == 2.5f);
InputInvoker.Invoke(target, "Number", "9"); Check(target.Number == 9);
InputInvoker.Invoke(target, "Flag", "true", 2.5f); Check(target.Flag);
InputInvoker.Invoke(target, "Ping", "unused", 2.5f); Check(target.Pings == 1);
InputInvoker.Invoke(target, "Pair", "3,4", 2.5f); Check(target.Sum == 7);
var culture = CultureInfo.CurrentCulture;
try {
 CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
 InputInvoker.Invoke(target, "Text", "fallback", 2.5f); Check(target.Text == "2.5");
} finally { CultureInfo.CurrentCulture = culture; }
InputInvoker.Invoke(target, "Number", "8", new object()); Check(target.Number == 8);
InputInvoker.Invoke(target, "Text", "fallback", null); Check(target.Text == null);
InputInvoker.Invoke(target, "Number", "6", null); Check(target.Number == 6);
InputInvoker.Invoke(target, "Double", "0", 2.5f); Check(target.Double == 2.5);
var game = new Game(); var world = new World();
typeof(Game).GetProperty("Instance")!.SetValue(null, game);
typeof(Game).GetProperty("World")!.SetValue(game, world);
try {
 var receiver = world.Spawn<Receiver>(); receiver.Name = "receiver";
 var timer = world.Spawn<ProjectShockGL.Placeables.TimerEvent>();
 timer.Outputs.Add(new OutputData { Event = "OnValueChanged", TargetName = "receiver", Input = "Number", Parameter = "99" });
 timer.Outputs.Add(new OutputData { Event = "OnFinished", TargetName = "receiver", Input = "Ping" });
 timer.Run(2); Check(receiver.Number == 2);
 TimerManager.Create().Update(.5f, .5f); Check(receiver.Number == 1.5f);
 TimerManager.Create().Update(2, 2); Check(receiver.Number == 0 && receiver.Pings == 1);
 timer.Run(3); world.Clear(); TimerManager.Create().Update(4, 4);
 Check(receiver.Number == 3);
} finally { world.Clear(); }
Console.WriteLine($"PASS: {count} Input/Output checks");

public class Receiver : Entity
{
 public float Number; public bool Flag; public int Pings; public int Sum; public string? Text; public double Double;
 [Input("Number")] public void SetNumber(float value) => Number = value;
 [Input("Flag")] public void SetFlag(bool value) => Flag = value;
 [Input("Ping")] public void Ping() => Pings++;
 [Input("Pair")] public void Pair(int a, int b) => Sum = a+b;
 [Input("Text")] public void SetText(string? value) => Text = value;
 [Input("Double")] public void SetDouble(double value) => Double = value;
}
