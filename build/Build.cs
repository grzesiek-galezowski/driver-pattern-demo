using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;

[GitHubActions(
  "continuous",
  GitHubActionsImage.UbuntuLatest,
  On = new[] { GitHubActionsTrigger.Push },
  InvokedTargets = new[] { nameof(Test) })]
class Build : NukeBuild
{
    public static int Main () => Execute<Build>(x => x.Test);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution]
    readonly Solution Solution;


    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
          DotNetTasks.DotNetClean(s => s.SetConfiguration(Configuration.Debug));
          DotNetTasks.DotNetClean(s => s.SetConfiguration(Configuration.Release));
        });

    Target Restore => _ => _
        .Executes(() =>
        {
          DotNetTasks.DotNetRestore(s => s.SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
          DotNetTasks.DotNetBuild(s => s
            .SetProjectFile(Solution)
            .SetConfiguration(Configuration.Release)
            .EnableNoRestore());
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
          DotNetTasks.DotNetTest(s => s
            .SetProjectFile(Solution)
            .SetConfiguration(Configuration.Release)
            .EnableNoRestore());
        });
}
