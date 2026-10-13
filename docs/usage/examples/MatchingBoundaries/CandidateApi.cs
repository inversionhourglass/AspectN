namespace BoundaryExamples;

internal static class CandidateApi
{
    public static string Echo(string value) => value;
    public static string? MaybeNull(string? value) => value;
    public static void Update(ref int value) => value++;
    public static void TryGet(out int value) => value = 42;
    public static int Sum(params int[] values) => values.Sum();
    public static int Read(scoped ref int value) => value;
}
