using System.Collections;
using ProjectShockGL;
using ProjectShockGL.Core;
using Body = ProjectShockGL.Core.Object;
var checks = 0;
void Check(bool ok) { if (!ok) throw new Exception($"Check {checks+1} failed"); checks++; }
using var scope = new Scope();
var world = scope.World;
var obj = world.Spawn<Body>();
var value = 0;
IEnumerator Frames() { value++; yield return null; value++; }
var first = obj.StartCoroutine(Frames()); Check(value == 1);
world.Update(.1f); Check(value == 2 && first.IsCompleted && !first.IsCancelled);
IEnumerator Delay(bool unscaled) { yield return new WaitForSeconds(1, unscaled); value++; }
value = 0;
var game = obj.StartCoroutine(Delay(false)); var raw = obj.StartCoroutine(Delay(true));
world.Update(0, true, .5f); Check(value == 0);
world.Update(0, true, .5f); Check(value == 1 && raw.IsCompleted && !game.IsCompleted);
world.Update(.5f, true, 1); Check(value == 1);
world.Update(.5f, true, 1); Check(value == 2 && game.IsCompleted);
value = 0;
IEnumerator Nested() { yield return Frames(); value++; }
obj.StartCoroutine(Nested()); Check(value == 1);
world.Update(.1f); Check(value == 3);
value = 0;
var child = obj.StartCoroutine(Frames());
IEnumerator Parent() { yield return child; value++; }
obj.StartCoroutine(Parent()); world.Update(.1f); Check(value == 3);
var cleaned = 0;
IEnumerator Endless() { try { while(true) yield return null; } finally { cleaned++; } }
var endless = obj.StartCoroutine(Endless()); obj.StopCoroutine(endless);
Check(cleaned == 1 && endless.IsCancelled && endless.IsCompleted);
value = 0;
obj.StartCoroutine(Frames()); obj.SetActive(false); world.Update(1); Check(value == 1);
obj.SetActive(true); world.Update(1); Check(value == 2);
var dying = world.Spawn<Body>(); dying.StartCoroutine(Endless()); world.Destroy(dying); Check(cleaned == 2);
world.Update(1); Check(cleaned == 2);
var starter = world.Spawn<Starter>(); world.Update(.1f); Check(starter.Value == 1);
world.Update(.1f); Check(starter.Value == 2);
value = 0; var cancelled = obj.StartCoroutine(Frames()); world.Clear();
world.Update(1); Check(cancelled.IsCancelled && value == 1);
var another = world.Spawn<Body>();
IEnumerator Fail() { yield return null; throw new InvalidOperationException("Expected test failure"); }
var failure = another.StartCoroutine(Fail()); another.StartCoroutine(Frames()); world.Update(1);
Check(failure.IsCompleted && value == 3);
var self = world.Spawn<Body>();
IEnumerator SelfDestroy() { try { world.Destroy(self); yield return null; } finally { cleaned++; } }
var selfRoutine = self.StartCoroutine(SelfDestroy()); Check(selfRoutine.IsCompleted && cleaned == 3);
value = 0; another.StartCoroutine(Frames()); world.Update(1, false); Check(value == 1);
world.Update(1); Check(value == 2);
Console.WriteLine($"PASS: {checks} coroutine checks");
public sealed class Scope : IDisposable { public World World = new(); public void Dispose() => World.Clear(); }
public class Starter : Body
{
 public int Value;
 public override void Start() { base.Start(); StartCoroutine(Sequence()); }
 private IEnumerator Sequence() { Value++; yield return null; Value++; }
}
