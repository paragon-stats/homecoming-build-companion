// check-git-signing.cs - fail when git commit signing is not set up, so an unsigned commit never starts.
//
// Run by the Husky pre-commit group:  dotnet run scripts/dev/check-git-signing.cs
//
// The rules are in git-signing.cs, which bootstrap.cs --verify shares. Setup steps: CONTRIBUTING.md,
// "Sign your commits".
// CI tooling, not shipped product code: exempt from the solution-wide analyzers.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false
#:include git-signing.cs

string? problem = GitSigning.Problem();
if (problem is null)
{
    Console.WriteLine("[OK] git commit signing is configured.");
    return 0;
}

Console.Error.WriteLine($".git/config:1:1: error: H-SIGN-001: git commit signing is not configured - {problem}");
Console.Error.WriteLine("This repository requires every commit to be signed. Set it up as CONTRIBUTING.md, \"Sign your commits\", describes:");
Console.Error.WriteLine("  git config --global gpg.format ssh");
Console.Error.WriteLine("  git config --global user.signingkey ~/.ssh/id_ed25519_signing.pub");
Console.Error.WriteLine("  git config --global commit.gpgsign true");
return 1;
