namespace ProjectShockGL;

// Complete every release step, then report failures to the caller.
internal static class Cleanup
{
    public static void Run(params Action[] actions)
    {
        List<Exception> errors = [];
        foreach (var action in actions)
        {
            try { action(); }
            catch (Exception error) { errors.Add(error); }
        }
        if (errors.Count > 0) throw new AggregateException("Cleanup failed.", errors);
    }
}
