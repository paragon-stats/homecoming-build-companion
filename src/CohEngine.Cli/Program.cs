using CohEngine.Core;
using CohEngine.Core.BuildFile;

return CliRunner.Run(args, Console.Out, Console.Error, new MbdBuildSource(MbdBuildSource.LocateDefaultMidsDirectory(AppContext.BaseDirectory)));
