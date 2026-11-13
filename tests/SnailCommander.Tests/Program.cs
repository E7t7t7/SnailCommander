using SnailCommander.Tests;

int assertions = 0;
int failures = 0;
void Assert(bool condition, string name)
{
    assertions++;
    if (!condition) { failures++; Console.Error.WriteLine("FAIL: " + name); }
}

foreach (var suite in new (string Name, Action<Action<bool, string>> Run)[]
{
    ("Devices", DeviceTests.Run), ("Observation", ObservationTests.Run),
    ("Minions", MinionTests.Run), ("Updates", UpdateTests.Run),
    ("Account evidence", AccountEvidenceTests.Run), ("Automation", AutomationTests.Run), ("Game viewport", GameViewportTests.Run)
})
{
    try { suite.Run(Assert); Console.WriteLine($"{suite.Name}: completed"); }
    catch (Exception error) { failures++; Console.Error.WriteLine($"{suite.Name}: {error}"); }
}
Console.WriteLine($"{assertions} assertions, {failures} failures");
return failures == 0 ? 0 : 1;
