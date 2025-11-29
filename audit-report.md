# Solution audit report

Generated: Fri Nov 28 19:02:46 UTC 2025

## Solution projects
Project(s)
----------
src/Core.Diagnostics/MetWorks.Core.Diagnostics.csproj
src/Core.Models/MetWorks.Core.Models.csproj
src/Validation.Exchange/MetWorks.Validation.Exchange.csproj
tests/Validation.Tests/MetWorks.Validation.Tests.csproj

## Project summary

| Project file | SDK / TargetFramework | Type hints | Has Program/Main | Has Controllers | Is Test Project |
|---|---:|---|---:|---:|---:|
| ./src/Validation.Exchange/MetWorks.Validation.Exchange.csproj | net8.0 | Microsoft.NET.Sdk  | no | no | no |
| ./src/Core.Diagnostics/MetWorks.Core.Diagnostics.csproj | net8.0 | Microsoft.NET.Sdk  | no | no | no |
| ./src/Core.Models/MetWorks.Core.Models.csproj | net8.0 | Microsoft.NET.Sdk  | no | no | no |
| ./tests/Validation.Tests/MetWorks.Validation.Tests.csproj | net8.0 | Microsoft.NET.Sdk  | no | no | yes |

## Projects with Program/Main or WebApplication usage (quick scan)

## Test projects and test frameworks

## Large directories (top 20)
16K	./scripts
24K	./.github
40K	./.git/worktrees
40K	./docs/decisions
44K	./.git/refs
52K	./.git/logs
52K	./docs
68K	./.git/hooks
336K	./src/Core.Diagnostics
340K	./src/Core.Models
348K	./.vs/MetWorks
452K	./src/Validation.Exchange
472K	./.vs/ProjectEvaluation
832K	./.vs
1.2M	./src
10M	./tests/Validation.Tests
11M	./tests
46M	./.git/objects
47M	./.git
59M	.

## Notes and suggested next steps
- Review projects marked as 'web-sdk' or containing ASP.NET packages for actual runtime code.
- Open projects with 'maybe' for Program/Main and inspect the files listed above.
- Check test projects for meaningful tests; run   Determining projects to restore...
  All projects are up-to-date for restore.
  MetWorks.Core.Diagnostics -> /srv/repos/MetWorks/src/Core.Diagnostics/bin/Debug/net8.0/MetWorks.Core.Diagnostics.dll
  MetWorks.Core.Models -> /srv/repos/MetWorks/src/Core.Models/bin/Debug/net8.0/MetWorks.Core.Models.dll
  MetWorks.Validation.Exchange -> /srv/repos/MetWorks/src/Validation.Exchange/bin/Debug/net8.0/MetWorks.Validation.Exchange.dll
  MetWorks.Validation.Tests -> /srv/repos/MetWorks/tests/Validation.Tests/bin/Debug/net8.0/MetWorks.Validation.Tests.dll
Test run for /srv/repos/MetWorks/tests/Validation.Tests/bin/Debug/net8.0/MetWorks.Validation.Tests.dll (.NETCoreApp,Version=v8.0)
Microsoft (R) Test Execution Command Line Tool Version 17.8.0 (x64)
Copyright (c) Microsoft Corporation.  All rights reserved.

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:     7, Skipped:     0, Total:     7, Duration: 8 ms - MetWorks.Validation.Tests.dll (net8.0) to see pass/fail.
