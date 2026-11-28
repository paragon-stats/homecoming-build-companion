namespace CohEngine.Analyzers.Tests.Ci;

/// <summary>What counts as a step that runs Python, on small workflow texts.</summary>
public sealed class PythonStepTests
{
    [Theory]
    [InlineData("python -m scripts.x")]
    [InlineData("python3 x.py")]
    [InlineData("python3.14 x.py")]
    [InlineData("/usr/bin/python3 x.py")]
    [InlineData("C:\\Python315\\python.exe x.py")]
    [InlineData("py -3 x.py")]
    [InlineData("pip install x")]
    [InlineData("pip3 install x")]
    [InlineData("pipx run x")]
    [InlineData("uv run x")]
    [InlineData("echo a && python x.py")]
    [InlineData("dotnet build\n  pip install x")]
    public void A_run_line_that_calls_python_or_its_tools_is_found(string run)
    {
        Assert.NotEmpty(Workflows.PythonSteps(Workflows.Parse(Workflow($"run: '{run.Replace("'", "''", StringComparison.Ordinal)}'"))));
    }

    [Theory]
    [InlineData("echo pipeline")]
    [InlineData("# python-semantic-release runs inside the release action")]
    [InlineData("dotnet run x.cs # then pip, later")]
    [InlineData("echo python-semantic-release")]
    [InlineData("dotnet run scripts/ci/check-release-policy.cs -- scripts/git/commit-types.txt pyproject.toml")]
    [InlineData("echo \"pip\"")]
    [InlineData("uvx-free --pipx-less")]
    public void A_run_line_that_only_mentions_python_is_not_found(string run)
    {
        Assert.Empty(Workflows.PythonSteps(Workflows.Parse(Workflow($"run: '{run.Replace("'", "''", StringComparison.Ordinal)}'"))));
    }

    [Theory]
    [InlineData("uses: actions/setup-python@0000000000000000000000000000000000000000")]
    [InlineData("uses: astral-sh/setup-uv@0000000000000000000000000000000000000000")]
    [InlineData("shell: python\n        run: print(1)")]
    [InlineData("shell: python3 {0}\n        run: print(1)")]
    public void A_step_that_sets_up_python_or_runs_under_it_is_found(string step)
    {
        Assert.NotEmpty(Workflows.PythonSteps(Workflows.Parse(Workflow(step))));
    }

    [Theory]
    [InlineData("defaults:\n  run:\n    shell: python\n")]
    [InlineData("")]
    public void A_default_python_shell_makes_every_run_step_python(string workflowDefaults)
    {
        string job = workflowDefaults.Length == 0 ? "    defaults:\n      run:\n        shell: python\n" : string.Empty;
        string text = $"{workflowDefaults}jobs:\n  j:\n    runs-on: x\n{job}    steps:\n      - run: print(1)\n      - uses: actions/checkout@0000000000000000000000000000000000000000\n";

        Assert.Equal(["print(1)"], Workflows.PythonSteps(Workflows.Parse(text)));
    }

    [Fact]
    public void A_composite_action_step_is_read_too()
    {
        Assert.NotEmpty(Workflows.PythonSteps(Workflows.Parse("runs:\n  using: composite\n  steps:\n    - shell: bash\n      run: pip install x\n")));
    }

    private static string Workflow(string step) => $"jobs:\n  j:\n    runs-on: x\n    steps:\n      - {step}\n";
}
