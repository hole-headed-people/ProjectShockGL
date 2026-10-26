# Lifecycle checks

Run from the repository root:

```powershell
dotnet run --project tests/LifecycleChecks/LifecycleChecks.csproj
```

These executable regression checks require no test framework or OpenGL window.
An assertion failure exits with a nonzero status.

`World.Destroy` immediately destroys the object and its components. World update
removes destroyed objects without invoking destruction again. `World.Clear`
detaches and destroys both active objects and objects waiting for `Start`.
Objects spawned by destruction callbacks belong to the new scene.

Custom `Destroy` overrides must call `base.Destroy()`. Use `World.Destroy` for
repeated requests; directly calling an override repeatedly can repeat its own
side effects before it reaches the base implementation.
