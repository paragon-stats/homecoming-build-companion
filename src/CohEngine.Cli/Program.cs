using CohEngine.Core;
using CohEngine.Core.Chain;

return args is ["chain", .. var chainArgs] ? ChainCommand.Run(chainArgs, Console.Out, Console.Error) : CliRunner.Run(args, Console.Out, Console.Error);
